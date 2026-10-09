// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SSAConstruction.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System.Runtime.CompilerServices;
using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Performs a unified SSA construction transformation that converts memory operations
/// into SSA form using phi nodes.
/// </summary>
/// <param name="args">The transformation args.</param>
/// <remarks>
/// This transformation handles:
/// - Simple local allocations (Alloca): Converted to SSA variables
/// - Array local allocations (Alloca): Converted to structure values
/// - Global malloc allocations: Converted to SSA variables (only if used in single method)
/// - Field accesses: Tracked through LoadFieldAddress chains
/// - Element accesses: Converted to field accesses in structures
///
/// It replaces:
/// - Alloca/Malloc instructions with SSA variable definitions
/// - Store instructions with SSA value assignments
/// - Load instructions with SSA value uses
/// - LoadElementAddress with structure field accesses (for arrays)
/// - GetViewLength with constants (for arrays)
///
/// IMPORTANT: Globals are only converted if used in a single method to avoid
/// race conditions during parallel transformation.
///
/// Phi nodes are automatically inserted at control-flow merge points as needed.
/// After SSA construction, run <see cref="SSACleanup"/> to remove trivial phi values.
/// </remarks>
sealed class SSAConstruction(TransformationArgs args) :
    Transformation<SSALocalAllocations>(args)
{
    private readonly SSAGlobalAllocations _global =
        SSAGlobalAllocations.Create(args.Module);

    /// <summary>
    /// Computes local method-wide allocations.
    /// </summary>
    protected override SSALocalAllocations CreateIntermediate(
        ModuleTransform transform,
        Method method) =>
        SSALocalAllocations.Create(method);

    /// <summary>
    /// Performs the SSA construction transformation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override void OnTransform(MethodTransform transform)
    {
        // Create SSA builder
        var ssaBuilder = SSABuilder<FieldRef>.Create(
            transform,
            transform.OldMethod.Blocks,
            transform.RewriteAs<BasicBlock>);

        // Process all blocks in reverse post-order with ProcessAndSeal pattern
        var local = GetIntermediate(transform);
        var joined = new SSAAllocations<SSALocalAllocations, SSAGlobalAllocations>(
            local,
            _global);
        var entryBlock = transform.OldMethod.EntryBlock;

        // Initialize SSA values for global allocations in the entry block.
        // Globals are module-level values that don't appear in the block's
        // BasicBlockValue iteration, so ConvertAllocation (which handles
        // Alloca) never fires for them. We must seed their initial null
        // value in the entry block so that GetValueRecursive doesn't try
        // to recurse through predecessors of a 0-predecessor entry block.
        InitializeGlobalAllocations(
            transform, ssaBuilder, joined, entryBlock);

        foreach (var block in transform.OldMethod.Blocks)
        {
            ssaBuilder.ProcessAndSeal(block);

            // Skip unreachable blocks (no predecessors and not the entry
            // block). SSA's GetValueRecursive requires predecessors to
            // resolve definitions. Unreachable blocks have no reaching
            // definitions and should not be processed.
            if (block != entryBlock && block.Predecessors.Length == 0)
            {
                ssaBuilder.TrySealSuccessors(block);
                continue;
            }

            var blockTransform = transform.GetBasicBlockTransform(block);

            // Convert values in this block
            ProcessBlock(transform, blockTransform, ssaBuilder, block, joined);

            ssaBuilder.TrySealSuccessors(block);
        }

        // Seal remaining blocks and verify
        ssaBuilder.SealRemainingBlocks();
        ssaBuilder.AssertAllSealed();

        // Cleanup derived pure values
        CleanupDerivedPureValues(transform, joined);
    }

    /// <summary>
    /// Processes a single block during SSA construction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessBlock<TAllocations>(
        MethodTransform transform,
        BasicBlockTransform blockTransform,
        SSABuilder<FieldRef> ssaBuilder,
        BasicBlock block,
        TAllocations allocations)
        where TAllocations : ISSAAllocations
    {
        // Convert basic block values
        foreach (BasicBlockValue bbValue in block)
        {
            switch (bbValue)
            {
                case Alloca alloca when
                    allocations.TryGetValue(alloca, out var allocInfo):
                    ConvertAllocation(
                        transform,
                        ssaBuilder,
                        blockTransform,
                        alloca,
                        allocInfo);
                    break;

                case Load load when allocations.TryGetRef(load.Source, out var loadRef):
                    ConvertLoad(transform, ssaBuilder, blockTransform, load, loadRef);
                    break;

                case Store store when
                    allocations.TryGetRef(store.Target, out var storeRef):
                    ConvertStore(transform, ssaBuilder, blockTransform, store, storeRef);
                    break;
            }
        }

        // Convert GetViewLength (it's a PureValue).
        // Two cases: (1) source is a direct allocation → use static array length,
        // (2) source is a NewView from SSA promotion → extract the NewView's
        //     length operand (this fold is essential for SupportsViews backends
        //     where LowerViews doesn't run and the NewView will be cleaned up).
        block.ForEachValue<GetViewLength>(getLength =>
        {
            if (allocations.TryGetRef(getLength.Source, out var lengthRef))
            {
                // For array-to-structure conversions, replace with constant array length
                var lengthValue = blockTransform.CreatePrimitiveValue(
                    getLength.Location,
                    lengthRef.AllocInfo.ArrayLength);
                transform.Replace(getLength, lengthValue);
            }
            else if (getLength.Source is NewView newView)
            {
                // NewView(ptr, len).Length → len (with type conversion)
                var lengthValue = blockTransform.CreateConvert(
                    getLength.Location,
                    newView.Length,
                    getLength.LengthType);
                if (lengthValue != null)
                    transform.Replace(getLength, lengthValue);
            }
        });
    }

    /// <summary>
    /// Recursively removes pure values that were derived from transformed allocations.
    /// </summary>
    private static void CleanupDerivedPureValues<TAllocations>(
        MethodTransform transform,
        TAllocations allocations)
        where TAllocations : ISSAAllocations
    {
        transform.OldMethod.Blocks.ForEachValue<PureValue>(value =>
        {
            if (!allocations.TryGetRef(value, out _))
                return;
            switch (value)
            {
                case LoadFieldAddress:
                case LoadElementAddress:
                case AddressSpaceCast:
                    transform.Replace(value, null);
                    break;
                case NewView nv:
                    if (!nv.Uses.HasAny)
                        transform.Replace(value, null);
                    break;
            }
        });
    }

    /// <summary>
    /// Initializes SSA values for global allocations in the entry block.
    /// Globals are module-level values that don't appear in block iteration,
    /// so they need explicit initialization before block processing begins.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void InitializeGlobalAllocations<TAllocations>(
        MethodTransform transform,
        SSABuilder<FieldRef> ssaBuilder,
        TAllocations allocations,
        BasicBlock entryBlock)
        where TAllocations : ISSAAllocations
    {
        var module = transform.OldMethod.Module;
        foreach (var global in module.Globals)
        {
            if (allocations.TryGetValue(global, out var allocInfo) &&
                allocInfo.UsedInMethod == transform.OldMethod)
            {
                var blockTransform = transform.GetBasicBlockTransform(entryBlock);
                var fieldRef = allocInfo.BaseFieldRef;

                // Rewrite AllocType to the current generation before creating
                // the structure type. GetInitializedType uses old-gen AllocType
                // directly, which causes type mismatches during CreateSetField.
                TypeValue initType;
                if (allocInfo.IsArrayToStructure)
                {
                    var rewrittenAllocType = transform.Rewrite(allocInfo.AllocType);
                    var structBuilder = transform.ModuleBuilder
                        .CreateStructureType(allocInfo.TotalStructFields);
                    for (int i = 0; i < allocInfo.ArrayLength; ++i)
                        structBuilder.Add(rewrittenAllocType);
                    initType = structBuilder.Seal()
                        .AsNotNullCast<StructureType>();
                }
                else
                {
                    initType = transform.Rewrite(allocInfo.AllocType);
                }

                var initValue = blockTransform.CreateNull(
                    global.Location, initType);
                ssaBuilder.SetValue(
                    blockTransform.OldBasicBlock, fieldRef, initValue);
                // Do NOT replace the global with null here. Unlike Alloca
                // (which is a local instruction), globals are module-level
                // values referenced by other code (e.g. ilgpu.array.new).
                // Only the SSA-tracked loads/stores are converted.
            }
        }
    }

    /// <summary>
    /// Converts an allocation (Alloca or Malloc) to its initial SSA value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ConvertAllocation(
        MethodTransform methodTransform,
        SSABuilder<FieldRef> ssaBuilder,
        BasicBlockTransform blockTransform,
        Value allocation,
        SSAAllocationInfo allocInfo)
    {
        var fieldRef = allocInfo.BaseFieldRef;

        // Rewrite AllocType to the current generation before creating
        // the initial value. GetInitializedType returns old-gen AllocType
        // directly for non-array allocations, causing stale type references.
        var initType = methodTransform.Rewrite(allocInfo.AllocType);
        var initValue = blockTransform.CreateNull(allocation.Location, initType);

        // Set the SSA value and remove the allocation
        ssaBuilder.SetValue(blockTransform.OldBasicBlock, fieldRef, initValue);
        methodTransform.Replace(allocation, null);
    }

    /// <summary>
    /// Converts a load to an SSA value use.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ConvertLoad(
        MethodTransform methodTransform,
        SSABuilder<FieldRef> ssaBuilder,
        BasicBlockTransform blockTransform,
        Load load,
        SSAValueFieldRef valueFieldRef)
    {
        var fieldRef = valueFieldRef.FieldRef;

        if (!fieldRef.IsDirect)
        {
            // Look up the base FieldRef using the allocation's BaseFieldRef,
            // which matches the key used by ConvertAllocation. For array
            // allocations, BaseFieldRef includes a FieldSpan covering all
            // fields, so new FieldRef(source) would be a key mismatch.
            var baseFieldRef = valueFieldRef.AllocInfo.BaseFieldRef;

            // When LoadElementAddress(ptr, 0) is folded to the source pointer,
            // the load targets the base allocation directly. The field span
            // then equals the base span (all elements) instead of a single-
            // element span. Map this back to element 0.
            var effectiveSpan = fieldRef.FieldSpan;
            if (valueFieldRef.AllocInfo.IsArrayToStructure
                && effectiveSpan.Equals(baseFieldRef.FieldSpan))
            {
                effectiveSpan = new FieldSpan(
                    0, valueFieldRef.AllocInfo.NumElementFields);
            }

            var ssaValue = ssaBuilder.GetValue(
                blockTransform.OldBasicBlock,
                baseFieldRef);
            ssaValue = blockTransform.CreateGetField(
                load.Location,
                ssaValue,
                effectiveSpan);
            methodTransform.Replace(load, ssaValue);
        }
        else
        {
            // Direct load from the allocation
            var ssaValue = ssaBuilder.GetValue(
                blockTransform.OldBasicBlock,
                fieldRef);
            methodTransform.Replace(load, ssaValue);
        }
    }

    /// <summary>
    /// Converts a store to an SSA value assignment.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ConvertStore(
        MethodTransform methodTransform,
        SSABuilder<FieldRef> ssaBuilder,
        BasicBlockTransform blockTransform,
        Store store,
        SSAValueFieldRef valueFieldRef)
    {
        var fieldRef = valueFieldRef.FieldRef;

        // Map the stored value - should never be null for a valid store
        var storeValue = methodTransform.Rewrite(store.Value)!;

        // If we're storing to a field, we need to update the structure
        if (!fieldRef.IsDirect)
        {
            // Get the base FieldRef using the allocation's BaseFieldRef,
            // which matches the key used by ConvertAllocation. For array
            // allocations, BaseFieldRef includes a FieldSpan covering all
            // fields, so new FieldRef(source) would be a key mismatch.
            var baseFieldRef = valueFieldRef.AllocInfo.BaseFieldRef;

            // When LoadElementAddress(ptr, 0) is folded to just the source
            // pointer, the store targets the base allocation directly. The
            // field span then equals the base span (covering all elements)
            // instead of a single-element span. For array-to-structure
            // conversions, map this back to element 0 so CreateSetField
            // receives a span matching the stored value's type.
            var effectiveSpan = fieldRef.FieldSpan;
            if (valueFieldRef.AllocInfo.IsArrayToStructure
                && effectiveSpan.Equals(baseFieldRef.FieldSpan))
            {
                effectiveSpan = new FieldSpan(
                    0, valueFieldRef.AllocInfo.NumElementFields);
            }

            var currentValue = ssaBuilder.GetValue(
                blockTransform.OldBasicBlock,
                baseFieldRef);
            var ssaValue = blockTransform.CreateSetField(
                store.Location,
                currentValue,
                effectiveSpan,
                storeValue).AsNotNull();

            // Update the base structure
            ssaBuilder.SetValue(blockTransform.OldBasicBlock, baseFieldRef, ssaValue);
        }
        else
        {
            // Direct store to the allocation
            ssaBuilder.SetValue(blockTransform.OldBasicBlock, fieldRef, storeValue);
        }

        methodTransform.Replace(store, null);
    }
}
