// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Uncomment this line to or define a preprocessor symbol to enable detailed Velocity
// accelerator debugging:
// #define DEBUG_VELOCITY

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.Backends.Velocity.Analyses;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// Static helper class for Velocity code generation.
    /// </summary>
    static class VelocityCodeGenerator
    {
        #region Constants

        /// <summary>
        /// The parameter index of all execution contexts.
        /// </summary>
        public const int ExecutionContextIndex = 0;

        /// <summary>
        /// The parameter index of the current global index.
        /// </summary>
        public const int GlobalIndexScalar = 1;

        /// <summary>
        /// The parameter index of the current group dimension.
        /// </summary>
        public const int GroupDimIndexScalar = 2;
        /// <summary>
        ///
        /// The parameter index of the current grid dimension.
        /// </summary>
        public const int GridDimIndexScalar = 3;

        /// <summary>
        /// The parameter index of all masks.
        /// </summary>
        public const int MaskParameterIndex = 4;

        /// <summary>
        /// The method parameter offset for all parameters.
        /// </summary>
        public const int MethodParameterOffset = 5;

        #endregion
    }

    /// <summary>
    /// Generates vectorized MSIL instructions out of IR values.
    /// </summary>
    /// <typeparam name="TILEmitter">The IL emitter type.</typeparam>
    /// <remarks>The code needs to be prepared for this code generator.</remarks>
    abstract partial class VelocityCodeGenerator<TILEmitter> :
        IBackendCodeGenerator<object>
        where TILEmitter : struct, IILEmitter
    {
        #region Nested Types

        /// <summary>
        /// Creation arguments passed to a constructor.
        /// </summary>
        /// <param name="Specializer">The current target specializer</param>
        /// <param name="Module">The current generation module.</param>
        /// <param name="EntryPoint">The current entry point.</param>
        public readonly record struct GeneratorArgs(
            VelocityTargetSpecializer Specializer,
            VelocityGenerationModule Module,
            EntryPoint EntryPoint);

        #endregion

        #region Instance

        /// <summary>
        /// Maps blocks to labels.
        /// </summary>
        private readonly Dictionary<BasicBlock, ILLabel> blockLookup =
            new(new BasicBlock.Comparer());

        /// <summary>
        /// The masks analysis holding information about the masks being required.
        /// </summary>
        private readonly VelocityMasks<TILEmitter> masks;

        /// <summary>
        /// A dictionary mapping values to IL locals.
        /// </summary>
        private readonly Dictionary<Value, ILLocal> locals = new();

        /// <summary>
        /// Temporary locals for initialization.
        /// </summary>
        private readonly Dictionary<TypeNode, ILLocal> nullLocals = new();

        private readonly PhiBindings phiBindings;
        private readonly Dictionary<Value, ILLocal> intermediatePhis;

        /// <summary>
        /// Constructs a new IL code generator.
        /// </summary>
        /// <param name="args">The generator args to use.</param>
        /// <param name="method">The current method to generate code for.</param>
        /// <param name="allocas">All allocations of the current method.</param>
        protected VelocityCodeGenerator(
            in GeneratorArgs args,
            Method method,
            Allocas allocas)
        {
            Module = args.Module;
            Specializer = args.Specializer;

            // Creates a new IL emitter
            Method = method;
            Allocas = allocas;
            if (typeof(TILEmitter) == typeof(DebugILEmitter))
            {
                Emitter = (TILEmitter)Activator.CreateInstance(
                    typeof(DebugILEmitter),
                    Console.Out).AsNotNull();
            }
            else
            {
                Emitter = (TILEmitter)Activator.CreateInstance(
                    typeof(TILEmitter),
                    Module.GetILGenerator(method)).AsNotNull();
            }

            // Create a new vector masks analysis instance
            masks = new(method.Blocks, Emitter, Specializer);

            // Determine all phi bindings
            phiBindings = PhiBindings.Create(
                Method.Blocks,
                (_, phiValue) => Declare(phiValue));
            intermediatePhis = new Dictionary<Value, ILLocal>(
                phiBindings.MaxNumIntermediatePhis);

            // Declare a label for each block
            foreach (var block in method.Blocks)
                blockLookup[block] = Emitter.DeclareLabel();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current generation module.
        /// </summary>
        public VelocityGenerationModule Module { get; }

        /// <summary>
        /// Returns the current type generator being used.
        /// </summary>
        public VelocityTypeGenerator TypeGenerator => Module.TypeGenerator;

        /// <summary>
        /// Returns the current method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Returns all allocations.
        /// </summary>
        public Allocas Allocas { get; }

        /// <summary>
        /// Returns the current emitter.
        /// </summary>
        public TILEmitter Emitter { get; }

        /// <summary>
        /// Returns the underlying target specializer.
        /// </summary>
        public VelocityTargetSpecializer Specializer { get; }

        #endregion

        #region IBackendCodeGenerator

        /// <summary>
        /// Perform no operation.
        /// </summary>
        public void GenerateHeader(object builder)
        {
            // We do not need to generate any headers
        }

        /// <summary>
        /// Generates an MSIL runtime method.
        /// </summary>
        public abstract void GenerateCode();

        /// <summary>
        /// Perform no operation.
        /// </summary>
        public void GenerateConstants(object builder)
        {
            // We do not need to emit any constants
        }

        /// <inheritdoc/>
        public void Merge(object builder)
        {
            // We do not need to perform any action
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a branch builder to wire masks and build branches.
        /// </summary>
        /// <param name="currentBlock">The current block.</param>
        protected BranchBuilder CreateBranchBuilder(BasicBlock currentBlock) =>
            new(this, currentBlock);

        /// <summary>
        /// Resets the block mask for the given block to no lanes at all.
        /// </summary>
        protected void TryResetBlockLanes(BasicBlock basicBlock)
        {
            if (masks.NeedsToRefreshMask(basicBlock))
                DisableLanesOf(basicBlock);
        }

        /// <summary>
        /// Resets the block mask for the given block by disabling the lanes from the
        /// specified mask local.
        /// </summary>
        protected void DisableLanesOf(BasicBlock basicBlock)
        {
            Specializer.PushNoLanesMask32(Emitter);
            Emitter.Emit(LocalOperation.Store, GetBlockMask(basicBlock));
        }

        /// <summary>
        /// Resets the block mask for the given block by disabling the lanes from the
        /// specified mask local.
        /// </summary>
        protected void DisableSpecificLanes(ILLocal target)
        {
            Specializer.NegateMask32(Emitter);
            Emitter.Emit(LocalOperation.Load, target);
            Specializer.IntersectMask32(Emitter);
            Emitter.Emit(LocalOperation.Store, target);
        }

        /// <summary>
        /// Returns the block mask for the given basic block.
        /// </summary>
        /// <param name="block">The block to lookup.</param>
        /// <returns>The block mask to use.</returns>
        protected ILLocal GetBlockMask(BasicBlock block) => masks.GetBlockMask(block);

        /// <summary>
        /// Intersects the current mask with the mask on the top of the stack.
        /// </summary>
        private void IntersectWithMask(ILLocal current)
        {
            // Intersect with the current mask
            Emitter.Emit(LocalOperation.Load, current);
            Specializer.IntersectMask32(Emitter);
        }

        /// <summary>
        /// Unifies the target mask with the mask on the top of the stack and stores
        /// the result.
        /// </summary>
        private void UnifyWithMaskOf(BasicBlock target, bool keepOnStack = false)
        {
            var targetMask = GetBlockMask(target);
            Emitter.Emit(LocalOperation.Load, targetMask);
            Specializer.UnifyMask32(Emitter);
            if (keepOnStack)
                Emitter.Emit(OpCodes.Dup);
            Emitter.Emit(LocalOperation.Store, targetMask);
        }

#if DEBUG_VELOCITY
        private void DumpAllMasks(string source)
        {
            if (!string.IsNullOrWhiteSpace(source))
                Emitter.EmitWriteLine(source);
            masks.DumpAllMasks(Emitter, Specializer);
            VelocityTargetSpecializer.DebuggerBreak(Emitter);
        }
#endif

        /// <summary>
        /// Generates code for all blocks.
        /// </summary>
        protected void GenerateCodeInternal()
        {
#if DEBUG_VELOCITY
            Method.DumpToConsole();
#endif

            // Init all possible phi values
            foreach (var phiValue in phiBindings.PhiValues)
                Emitter.LoadNull(GetLocal(phiValue));

            // Init all allocations
            BindAllocations();

            // Disable all lanes
            masks.DisableAllLanes(Method, Emitter, Specializer);

            // Emit code for each block
            foreach (var block in Method.Blocks)
            {
                // Mark the current label
                Emitter.MarkLabel(blockLookup[block]);

#if DEBUG_VELOCITY
                Console.WriteLine($"Generating code for: {block.ToReferenceString()}");
                Emitter.EmitWriteLine("Entering: " + block.ToReferenceString());
                DumpAllMasks("");
#endif

                // Generate code for all values
                foreach (var value in block)
                    this.GenerateCodeFor(value);

                // Reset all intermediate phis
                intermediatePhis.Clear();

                // Build terminator
                this.GenerateCodeFor(block.Terminator.AsNotNull());
            }
        }

        /// <summary>
        /// Binds all shared and local memory allocations.
        /// </summary>
        private void BindAllocations()
        {
            // Bind shared allocations
            foreach (var allocation in Allocas.SharedAllocations)
            {
                Specializer.GetSharedMemoryFromPool(
                    Emitter,
                    TypeGenerator.GetLinearizedScalarType(allocation.ElementType),
                    allocation.ArraySize);
                Store(allocation.Alloca);
            }

            // Bind dynamic shared memory allocations (we can treat the separately from
            // static allocations, as this will also be the case for all other
            // accelerator types in the future
            foreach (var allocation in Allocas.DynamicSharedAllocations)
            {
                Specializer.GetDynamicSharedMemory(Emitter);
                Store(allocation.Alloca);
            }

            // Bind local allocations
            foreach (var allocation in Allocas.LocalAllocations)
            {
                // Get unified pointer which needs further adjustments
                int lengthInBytesPerThread =
                    allocation.ElementType.Size * allocation.ArraySize;

                // Compute allocation stride per thread:
                // offset[laneIdx] = laneIdx * lengthInBytes
                Emitter.EmitConstant(lengthInBytesPerThread);
                Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.U);
                Specializer.LoadLaneIndexVector32(Emitter);
                Specializer.BinaryOperation32(
                    Emitter,
                    BinaryArithmeticKind.Mul,
                    VelocityWarpOperationMode.U);
                Specializer.Convert32To64(Emitter, VelocityWarpOperationMode.U);

                // Get a unified base pointer for all threads in all lanes
                Specializer.GetUnifiedLocalMemoryFromPool(
                    Emitter,
                    lengthInBytesPerThread);

                // Adjust local base pointer to refer to the right memory region
                Specializer.BinaryOperation64(
                    Emitter,
                    BinaryArithmeticKind.Add,
                    VelocityWarpOperationMode.U);

                Store(allocation.Alloca);
            }
        }

        /// <summary>
        /// Binds all phi values of the current block flowing through an edge to the
        /// target block. Note that the current mask instance is assumed to be pushed
        /// onto the evaluation stack.
        /// </summary>
        private void BindPhis(
            PhiBindings.PhiBindingCollection bindings,
            BasicBlock target,
            Action passMask)
        {
            foreach (var (phiValue, value) in bindings)
            {
                // Reject phis not flowing to the target edge
                if (phiValue.BasicBlock != target)
                    continue;

                // Check for an intermediate phi value
                if (bindings.IsIntermediate(phiValue))
                {
                    // Declare a new intermediate local variable
                    var intermediateLocal = DeclareVectorizedTemporary(phiValue.PhiType);
                    intermediatePhis.Add(phiValue, intermediateLocal);

                    // Move this phi value into a temporary register for reuse
                    Load(phiValue);
                    Emitter.Emit(LocalOperation.Store, intermediateLocal);
                }

                // Determine the source value from which we need to copy from
                var sourceLocal = intermediatePhis
                    .TryGetValue(value, out var tempLocal)
                    ? tempLocal
                    : GetLocal(value);

                // Move contents while merging our information
                var phiLocal = GetLocal(phiValue);
                var intermediateTempLocal = EmitMerge(phiValue,
                    () =>
                    {
                        Emitter.Emit(LocalOperation.Load, phiLocal);
                        return phiLocal.VariableType;
                    },
                    () =>
                    {
                        Emitter.Emit(LocalOperation.Load, sourceLocal);
                        return sourceLocal.VariableType;
                    },
                    passMask,
                    _ => phiLocal);
                // Store the value to the phi local explicitly
                if (!intermediateTempLocal.HasValue)
                    Emitter.Emit(LocalOperation.Store, phiLocal);

#if DEBUG_VELOCITY
                // Dump phi locals
                string phiReference = $"Phi {phiValue.ToReferenceString()}: ";
                if (phiValue.Type is PrimitiveType)
                {
                    Emitter.Emit(LocalOperation.Load, phiLocal);
                    if (phiValue.BasicValueType.IsTreatedAs32Bit())
                        Specializer.DumpWarp32(Emitter, phiReference);
                    else
                        Specializer.DumpWarp64(Emitter, phiReference);
                }
                else
                {
                    Emitter.EmitWriteLine($"{phiReference}: complex value type");
                }
#endif
            }
        }

        /// <summary>
        /// Loads a local variable that has been associated with the given value.
        /// </summary>
        /// <param name="value">The value to load.</param>
        /// <returns>The loaded variable.</returns>
        private ILLocal GetLocal(Value value)
        {
            // Load the local
            value.Assert(locals.ContainsKey(value));
            return locals[value];
        }

        /// <summary>
        /// Loads the given value onto the evaluation stack.
        /// </summary>
        /// <param name="value">The value to load.</param>
        public void Load(Value value)
        {
            var local = GetLocal(value);
            Emitter.Emit(LocalOperation.Load, local);
            // Note that we assume that all locals have already been converted to
            // their vector counterparts
        }

        /// <summary>
        /// Loads the given value onto the evaluation stack.
        /// </summary>
        /// <param name="value">The value to load.</param>
        /// <param name="type">The loaded managed type.</param>
        public void LoadVectorized(Value value, out Type type)
        {
            Load(value);
            type = GetVectorizedType(value.Type);
        }

        /// <summary>
        /// Loads a reference to the given value onto the evaluation stack.
        /// </summary>
        /// <param name="value">The value to load.</param>
        public void LoadRef(Value value)
        {
            // Load address of local variable
            var local = GetLocal(value);
            Emitter.Emit(LocalOperation.LoadAddress, local);
        }

        /// <summary>
        /// Loads a reference to the given value onto the evaluation stack.
        /// </summary>
        /// <param name="value">The value to load.</param>
        /// <param name="type">The loaded managed type.</param>
        public void LoadRefAndType(Value value, out Type type)
        {
            LoadRef(value);
            type = GetVectorizedType(value.Type);
        }

        /// <summary>
        /// Declares a new phi value.
        /// </summary>
        /// <param name="phiValue">The phi value to declare.</param>
        public void Declare(PhiValue phiValue)
        {
            var local = DeclareVectorizedTemporary(phiValue.PhiType);
            locals.Add(phiValue, local);
        }

        /// <summary>
        /// Declares a new vectorized temporary variable.
        /// </summary>
        /// <param name="typeNode">The type of the variable to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public ILLocal DeclareVectorizedTemporary(TypeNode typeNode) =>
            Emitter.DeclareLocal(GetVectorizedType(typeNode));

        /// <summary>
        /// Stores the given value by popping its value from the evaluation stack.
        /// </summary>
        /// <param name="value">The value to store.</param>
        public void Store(Value value)
        {
            value.Assert(!locals.ContainsKey(value));
            if (!value.Uses.HasAny)
            {
                if (!value.Type.IsVoidType)
                    Emitter.Emit(OpCodes.Pop);
                return;
            }

            var local = Emitter.DeclareLocal(GetVectorizedType(value.Type));
            locals.Add(value, local);
            Emitter.Emit(LocalOperation.Store, local);
        }

        /// <summary>
        /// Aliases the given value with the specified local.
        /// </summary>
        /// <param name="value">The value to register an alias for.</param>
        /// <param name="local">The local variable alias.</param>
        public void Alias(Value value, ILLocal local)
        {
            value.Assert(!locals.ContainsKey(value));
            locals.Add(value, local);
        }

        /// <summary>
        /// Loads the vectorized managed type that corresponds to the given IR type.
        /// </summary>
        /// <param name="type">The IR type to convert</param>
        /// <returns>The vectorized managed type.</returns>
        private Type GetVectorizedType(TypeNode type) =>
            TypeGenerator.GetVectorizedType(type);

        #endregion
    }
}
