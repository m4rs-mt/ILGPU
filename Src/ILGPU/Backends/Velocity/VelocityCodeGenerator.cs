// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Runtime.Velocity;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// Generates vectorized MSIL instructions out of IR values.
    /// </summary>
    /// <typeparam name="TILEmitter">The IL emitter type.</typeparam>
    /// <typeparam name="TVerifier">The view generator type.</typeparam>
    /// <remarks>The code needs to be prepared for this code generator.</remarks>
    abstract partial class VelocityCodeGenerator<TILEmitter, TVerifier> :
        IBackendCodeGenerator<object>
        where TILEmitter : struct, IILEmitter
        where TVerifier : IVelocityWarpVerifier, new()
    {
        #region Constants

        public const int MaskParameterIndex = 0;

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a specialized phi binding allocator.
        /// </summary>
        private readonly struct PhiBindingAllocator : IPhiBindingAllocator
        {
            /// <summary>
            /// Constructs a new phi binding allocator.
            /// </summary>
            /// <param name="parent">The parent code generator.</param>
            public PhiBindingAllocator(
                VelocityCodeGenerator<TILEmitter, TVerifier> parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// Returns the parent code generator.
            /// </summary>
            public VelocityCodeGenerator<TILEmitter, TVerifier> Parent { get; }

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            public void Process(BasicBlock block, Phis phis) { }

            /// <summary>
            /// Allocates a new phi value in the dominator block.
            /// </summary>
            public void Allocate(BasicBlock block, PhiValue phiValue) =>
                Parent.Declare(phiValue);
        }

        /// <summary>
        /// Helps building branches by taking branch targets and masks into account.
        /// </summary>
        private struct BranchBuilder
        {
            private readonly VelocityCodeGenerator<TILEmitter, TVerifier> codeGenerator;

            private readonly bool isBackEdgeBlock;
            private InlineList<(BasicBlock BasicBlock, Action PassMask)> headers;

            public BranchBuilder(
                VelocityCodeGenerator<TILEmitter, TVerifier> parent,
                BasicBlock currentBlock)
            {
                codeGenerator = parent;
                isBackEdgeBlock = parent.backEdges.Contains(currentBlock);
                headers = InlineList<(BasicBlock, Action)>.Create(2);
            }

            /// <summary>
            /// Returns the current emitter.
            /// </summary>
            public TILEmitter Emitter => codeGenerator.Emitter;

            /// <summary>
            /// Returns the header map.
            /// </summary>
            public BasicBlockMap<
                BasicBlockCollection<ReversePostOrder, Forwards>> Headers =>
                codeGenerator.headersBodyMap;

            /// <summary>
            /// Returns the parent dominators.
            /// </summary>
            public Dominators<Forwards> Dominators => codeGenerator.dominators;

            /// <summary>
            /// Returns the parent instructions
            /// </summary>
            public VelocityInstructions Instructions => codeGenerator.Instructions;
            public bool NeedsBranch => headers.Count > 0;

            /// <summary>
            /// Records a branch target.
            /// </summary>
            /// <param name="target">The target block to branch to.</param>
            /// <param name="passMask">The pass mask action.</param>
            public void RecordBranchTarget(BasicBlock target, Action passMask)
            {
                // Check for a jump backwards
                if (isBackEdgeBlock && Headers.Contains(target))
                {
                    // We need to intersect with the mask of the target block
                    headers.Add((target, passMask));
                }
                else
                {
                    // Pass the current mask
                    passMask();

                    // We are branching forwards and need to pass the mask while unifying
                    // all lanes
                    codeGenerator.UnifyWithMaskOf(target);
                }
            }

            /// <summary>
            /// Emits a branch if required.
            /// </summary>
            public void EmitBranch()
            {
                // If we don't need a branch, we can safely return here
                if (!NeedsBranch)
                    return;

                // Intersect with all target masks
                foreach (var (target, passMask) in headers)
                {
                    passMask();
                    codeGenerator.IntersectWithMaskOf(target);
                }

                // TODO: Find the "top-most" header
                // TODO: support for multiple header jumps
                headers[0].BasicBlock.Assert(headers.Count == 1);

                // Disable all lanes in all loop bodies
                foreach (var (header, _) in headers)
                {
                    foreach (var block in Headers[header])
                    {
                        if (block == header)
                            continue;

                        var blockMask = codeGenerator.GetBlockMask(block);
                        codeGenerator.DisableAllLanes(blockMask);
                    }
                }

                // Check for any active lane and jump in the case a lane requires
                // further processing
                var (targetHeader, _) = headers[0];
                Emitter.Emit(
                    LocalOperation.Load,
                    codeGenerator.GetBlockMask(targetHeader));
                Emitter.EmitCall(Instructions.MaskHasActiveLanes);

                // Branch to the actual loop header
                var blockLabel = codeGenerator.blockLookup[targetHeader];
                Emitter.Emit(OpCodes.Brtrue, blockLabel);
            }
        }

        public readonly struct GeneratorArgs
        {
            internal GeneratorArgs(
                VelocityInstructions instructions,
                VelocityGenerationModule module,
                int warpSize,
                EntryPoint entryPoint)
            {
                Instructions = instructions;
                Module = module;
                WarpSize = warpSize;
                EntryPoint = entryPoint;
            }

            /// <summary>
            /// Returns the current instruction instance.
            /// </summary>
            public VelocityInstructions Instructions { get; }

            /// <summary>
            /// Returns the current generation module.
            /// </summary>
            public VelocityGenerationModule Module { get; }

            /// <summary>
            /// Returns the current warp size to be used.
            /// </summary>
            public int WarpSize { get; }

            /// <summary>
            /// Returns the current entry point.
            /// </summary>
            public EntryPoint EntryPoint { get; }
        }

        #endregion

        #region Static

        /// <summary>
        /// The current verifier type.
        /// </summary>
        private static readonly Type VerifierType = typeof(TVerifier);

        #endregion

        #region Instance

        /// <summary>
        /// Maps blocks to their input masks.
        /// </summary>
        private readonly BasicBlockMap<ILLocal> blockMasks;

        /// <summary>
        /// Maps blocks to labels.
        /// </summary>
        private readonly Dictionary<BasicBlock, ILLabel> blockLookup =
            new Dictionary<BasicBlock, ILLabel>();

        /// <summary>
        /// The set of all back edge source blocks.
        /// </summary>
        private readonly BasicBlockSet backEdges;

        /// <summary>
        /// The set of all loop headers.
        /// </summary>
        private readonly BasicBlockMap<
            BasicBlockCollection<ReversePostOrder, Forwards>> headersBodyMap;

        /// <summary>
        /// The current dominators.
        /// </summary>
        private readonly Dominators<Forwards> dominators;

        private readonly Dictionary<Value, ILLocal> locals =
            new Dictionary<Value, ILLocal>();

        /// <summary>
        /// Temporary locals for initialization.
        /// </summary>
        private readonly Dictionary<TypeNode, ILLocal> nullLocals =
            new Dictionary<TypeNode, ILLocal>();

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
            Instructions = args.Instructions;
            Module = args.Module;
            WarpSize = args.WarpSize;

            // Creates a new IL emitter
            Method = method;
            Allocas = allocas;
            Emitter = (TILEmitter)Activator.CreateInstance(
                typeof(TILEmitter),
                Module.GetILGenerator(method));

            blockMasks = method.Blocks.CreateMap<ILLocal>();
            headersBodyMap = method.Blocks.CreateMap<
                BasicBlockCollection<ReversePostOrder, Forwards>>();
            backEdges = method.Blocks.CreateSet();

            // Determine CFG, dominators and loops
            var cfg = method.Blocks.CreateCFG();
            dominators = cfg.CreateDominators();
            var loops = cfg.CreateLoops();
            foreach (var loop in loops)
            {
                // Determine all body blocks
                var bodyBlocks = loop.ComputeOrderedBlocks(0);

                // Register all loop headers
                foreach (var header in loop.Headers)
                    headersBodyMap.Add(header, bodyBlocks);

                // Register all back edges
                foreach (var backEdge in loop.BackEdges)
                    backEdges.Add(backEdge);
            }

            // Allocate local masks and initialize all of them
            foreach (var block in method.Blocks)
            {
                // Create a local variable to store the entry mask for this block
                var blockMask = Emitter.DeclareLocal(typeof(VelocityLaneMask));
                blockMasks[block] = blockMask;

                // Declare a label for each block
                blockLookup[block] = Emitter.DeclareLabel();
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current instruction instance.
        /// </summary>
        public VelocityInstructions Instructions { get; }

        /// <summary>
        /// Returns the current generation module.
        /// </summary>
        public VelocityGenerationModule Module { get; }

        /// <summary>
        /// Returns the current warp size to be used.
        /// </summary>
        public int WarpSize { get; }

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
        /// Resets the block mask for the given block to all lanes.
        /// </summary>
        protected void EnableAllLanes(ILLocal local)
        {
            Emitter.Emit(OpCodes.Ldsfld, Instructions.AllLanesMask);
            Emitter.Emit(LocalOperation.Store, local);
        }

        /// <summary>
        /// Resets the block mask for the given block to no lanes at all.
        /// </summary>
        protected void DisableAllLanes(ILLocal local)
        {
            Emitter.Emit(OpCodes.Ldsfld, Instructions.NoLanesMask);
            Emitter.Emit(LocalOperation.Store, local);
        }

        /// <summary>
        /// Returns the block mask for the given basic block.
        /// </summary>
        /// <param name="block">The block to lookup.</param>
        /// <returns>The block mask to use.</returns>
        protected ILLocal GetBlockMask(BasicBlock block) => blockMasks[block];

        private BranchBuilder CreateBranchBuilder(BasicBlock current) =>
            new(this, current);

        /// <summary>
        /// Intersects the current mask with the mask on the top of the stack.
        /// </summary>
        private void IntersectWithMaskOf(BasicBlock current)
        {
            // Intersect with the current mask
            var currentMask = GetBlockMask(current);
            IntersectWithMask(currentMask);
            Emitter.Emit(LocalOperation.Store, currentMask);
        }

        /// <summary>
        /// Intersects the current mask with the mask on the top of the stack.
        /// </summary>
        private void IntersectWithMask(ILLocal current)
        {
            // Intersect with the current mask
            Emitter.Emit(LocalOperation.Load, current);
            Emitter.EmitCall(Instructions.IntersectLanesMask);
        }

        /// <summary>
        /// Unifies the target mask with the mask on the top of the stack and stores
        /// the result.
        /// </summary>
        private void UnifyWithMaskOf(BasicBlock target)
        {
            var targetMask = blockMasks[target];
            UnifyWithMask(targetMask);
        }

        /// <summary>
        /// Unifies the target mask with the mask on the top of the stack and stores
        /// the result.
        /// </summary>
        private void UnifyWithMask(ILLocal targetMask)
        {
            Emitter.Emit(LocalOperation.Load, targetMask);
            Emitter.EmitCall(Instructions.UnifyLanesMask);
            Emitter.Emit(LocalOperation.Store, targetMask);
        }

        /// <summary>
        /// Disables all internal lanes.
        /// </summary>
        private void DisableAllLanes()
        {
            foreach (var (basicBlock, blockMask) in blockMasks)
            {
                if (basicBlock == Method.EntryBlock)
                    continue;
                DisableAllLanes(blockMask);
            }
        }

        /// <summary>
        /// Generates code for all blocks.
        /// </summary>
        protected void GenerateCodeInternal()
        {
            Method.DumpToConsole();

            // Setup phi values
            var bindingAllocator = new PhiBindingAllocator(this);
            var phiBindings = PhiBindings.Create(Method.Blocks, bindingAllocator);
            var intermediatePhis = new Dictionary<Value, ILLocal>(
                phiBindings.MaxNumIntermediatePhis);

            // Init all possible phi values
            foreach (var phiValue in phiBindings.PhiValues)
            {
                var nullValue = Emitter.LoadNull(GetVectorizedType(phiValue.PhiType));
                Emitter.Emit(LocalOperation.Load, nullValue);
                Emitter.Emit(LocalOperation.Store, GetLocal(phiValue));
            }

            // Init all allocations
            BindAllocations();

            // Disable all lanes
            DisableAllLanes();

            // Emit code for each block
            foreach (var block in Method.Blocks)
            {
                // Mark the current label
                Emitter.MarkLabel(blockLookup[block]);

                // Generate code for all values
                foreach (var value in block)
                    this.GenerateCodeFor(value);

                // Wire phi nodes
                if (phiBindings.TryGetBindings(block, out var bindings))
                {
                    // Assign all phi values
                    BindPhis(bindings, intermediatePhis, block);
                }

                // Build terminator
                this.GenerateCodeFor(block.Terminator);

                // Reset all intermediate phis
                intermediatePhis.Clear();
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
                var allocationMethod = VelocityMultiprocessor.GetSharedMemoryMethodInfo
                    .MakeGenericMethod(new Type[]
                    {
                        TypeGenerator.GetLinearizedScalarType(allocation.ElementType)
                    });
                Emitter.LoadIntegerConstant(allocation.ArraySize);
                Emitter.EmitCall(allocationMethod);
                Store(allocation.Alloca);
            }

            // Bind local allocations
            foreach (var allocation in Allocas.LocalAllocations)
            {
                var allocationMethod = VelocityMultiprocessor.GetLocalMemoryMethodInfo
                    .MakeGenericMethod(new Type[]
                    {
                        TypeGenerator.GetLinearizedScalarType(allocation.ElementType)
                    });
                Emitter.LoadIntegerConstant(allocation.ArraySize);
                Emitter.EmitCall(allocationMethod);
                Store(allocation.Alloca);
            }

            // Dynamic shared memory allocations are not supported at the moment
            if (Allocas.DynamicSharedAllocations.Length > 0)
            {
                throw Method.GetNotSupportedException(
                    ErrorMessages.NotSupportedDynamicSharedMemoryAllocations);
            }
        }

        /// <summary>
        /// Binds all phi values of the current block.
        /// </summary>
        private void BindPhis(
            PhiBindings.PhiBindingCollection bindings,
            Dictionary<Value, ILLocal> intermediatePhis,
            BasicBlock block)
        {
            foreach (var (phiValue, value) in bindings)
            {
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
                var phiBlockMask = blockMasks[block];
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
                    () => Emitter.Emit(LocalOperation.Load, phiBlockMask),
                    () => Emitter.EmitCall(Instructions.MergeWithMaskOperation32),
                    () => Emitter.EmitCall(Instructions.MergeWithMaskOperation64),
                    _ => phiLocal);
                // Store the value to the phi local explicitly
                if (!intermediateTempLocal.HasValue)
                    Emitter.Emit(LocalOperation.Store, phiLocal);
            }
        }

        /// <summary>
        /// Converts the value on the top of the stack to a full-featured velocity warp
        /// vector either consisting of 32bit or 64bit values.
        /// </summary>
        /// <param name="is32Bit">
        /// True, if the current value is considered a 32bit value.
        /// </param>
        /// <param name="mode">The current operation mode.</param>
        public void ToWarpValue(bool is32Bit, VelocityWarpOperationMode mode)
        {
            // Determine whether the current value on the stack is a 32bit value or not
            var operation = is32Bit
                ? Instructions.GetConstValueOperation32(mode)
                : Instructions.GetConstValueOperation64(mode);
            Emitter.EmitCall(operation);
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
                return;

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
