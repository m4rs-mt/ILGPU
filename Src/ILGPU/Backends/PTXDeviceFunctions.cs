// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXDeviceFunctions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.Compiler.Intrinsic;
using ILGPU.Resources;
using ILGPU.Util;
using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Backends
{
    /// <summary>
    /// The implementation of PTX-specific device functions.
    /// </summary>
    sealed partial class PTXDeviceFunctions : CompilerDeviceFunctions
    {
        #region Static

        /// <summary>
        /// Represents the Debug.Assert wrapper to realize debug assertion.
        /// </summary>
        private static readonly MethodInfo DebugAssertWrapper = typeof(PTXDeviceFunctions).GetMethod(
                nameof(AssertWrapper),
                BindingFlags.NonPublic | BindingFlags.Static);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Caching of compiler-known functions")]
        static PTXDeviceFunctions()
        {
            // Register custom Debug.Assert wrapper for *failed* assertions.
            DeviceFunctionHandlers.Add(
                typeof(PTXDeviceFunctions).GetMethod(
                    nameof(AssertFailedWrapper),
                    BindingFlags.NonPublic | BindingFlags.Static),
                (deviceFunctions, context) => (deviceFunctions as PTXDeviceFunctions).MakeAssertFailed(
                    context));
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX-device-functions instance.
        /// </summary>
        /// <param name="unit">The targeted compile unit.</param>
        public PTXDeviceFunctions(CompileUnit unit)
            : base(unit)
        {
            var context = unit.LLVMContext;
            var module = unit.LLVMModule;

            InitAssertions(context, module, unit.NativeIntPtrType);
            InitAtomics(context, module);
            InitGroups(context, module);
            InitMemoryFences(context, module);
            InitRegisters(context, module);
            InitShuffles(context, module);
            InitWarps(context, module);
        }

        private void InitRegisters(LLVMContextRef context, LLVMModuleRef module)
        {
            GetThreadIdxX = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.tid.x", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));
            GetThreadIdxY = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.tid.y", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));
            GetThreadIdxZ = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.tid.z", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));

            GetBlockIdxX = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.ctaid.x", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));
            GetBlockIdxY = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.ctaid.y", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));
            GetBlockIdxZ = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.ctaid.z", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));

            GetBlockDimX = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.ntid.x", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));
            GetBlockDimY = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.ntid.y", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));
            GetBlockDimZ = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.ntid.z", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));

            GetGridDimX = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.nctaid.x", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));
            GetGridDimY = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.nctaid.y", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));
            GetGridDimZ = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.nctaid.z", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false)));

            GetGridDimensions = new Lazy<LLVMValueRef>[] { GetGridDimX, GetGridDimY, GetGridDimZ };
            GetBlockDimensions = new Lazy<LLVMValueRef>[] { GetBlockDimX, GetBlockDimY, GetBlockDimZ };

        }

        private void InitAtomics(LLVMContextRef context, LLVMModuleRef module)
        {
            AtomicAddF32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.atomic.load.add.f32.p0f32", LLVM.FunctionType(context.FloatTypeInContext(), new LLVMTypeRef[]
             {
                LLVM.PointerType(context.FloatTypeInContext(), 0),
                context.FloatTypeInContext()
             }, false)));

            AtomicIncU32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.atomic.load.inc.32.p0i32", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[]
             {
                LLVM.PointerType(context.Int32TypeInContext(), 0),
                context.Int32TypeInContext()
             }, false)));

            AtomicDecU32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.atomic.load.dec.32.p0i32", LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[]
             {
                LLVM.PointerType(context.Int32TypeInContext(), 0),
                context.Int32TypeInContext()
             }, false)));
        }

        private void InitMemoryFences(LLVMContextRef context, LLVMModuleRef module)
        {
            var fenceType = LLVM.FunctionType(context.VoidTypeInContext(), new LLVMTypeRef[] { }, false);
            BlockLevelFence = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.membar.cta", fenceType));
            DeviceLevelFence = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.membar.gl", fenceType));
            SystemLevelFence = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.membar.sys", fenceType));
        }

        private void InitGroups(LLVMContextRef context, LLVMModuleRef module)
        {
            GroupBarrier = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.barrier0", LLVM.FunctionType(context.VoidTypeInContext(), new LLVMTypeRef[] { }, false)));
            var genericGroupBarrierType = LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[]
            {
                context.Int32TypeInContext()
            }, false);
            GroupBarrierAnd = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.barrier0.and", genericGroupBarrierType));
            GroupBarrierOr = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.barrier0.or", genericGroupBarrierType));
            GroupBarrierPopCount = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.barrier0.popc", genericGroupBarrierType));
        }

        private void InitWarps(LLVMContextRef context, LLVMModuleRef module)
        {
            var getterType = LLVM.FunctionType(context.Int32TypeInContext(), new LLVMTypeRef[] { }, false); 
            GetWarpSize = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.warpsize", getterType));
            GetLaneId = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.read.ptx.sreg.laneid", getterType));
        }

        private void InitAssertions(LLVMContextRef context, LLVMModuleRef module, LLVMTypeRef intPtrType)
        {
            AssertFailedMethod = new Lazy<LLVMValueRef>(() =>
                LLVM.AddFunction(module, "__assertfail",
                LLVM.FunctionType(context.VoidTypeInContext(), new LLVMTypeRef[]
                {
                    LLVM.PointerType(context.VoidTypeInContext(), 0),
                    LLVM.PointerType(context.VoidTypeInContext(), 0),
                    context.Int32TypeInContext(),
                    LLVM.PointerType(context.VoidTypeInContext(), 0),
                    intPtrType
                }, false)));
        }

        private void InitShuffles(LLVMContextRef context, LLVMModuleRef module)
        {
            var shuffleI32Type = LLVM.FunctionType(
                context.Int32TypeInContext(), new LLVMTypeRef[]
                {
                    context.Int32TypeInContext(),
                    context.Int32TypeInContext(),
                    context.Int32TypeInContext(),
                }, false);
            var shuffleF32Type = LLVM.FunctionType(
                context.FloatTypeInContext(), new LLVMTypeRef[]
                {
                    context.FloatTypeInContext(),
                    context.Int32TypeInContext(),
                    context.Int32TypeInContext(),
                }, false);

            ShuffleI32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.shfl.idx.i32", shuffleI32Type));
            ShuffleF32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.shfl.idx.f32", shuffleF32Type));

            ShuffleDownI32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.shfl.down.i32", shuffleI32Type));
            ShuffleDownF32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.shfl.down.f32", shuffleF32Type));

            ShuffleUpI32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.shfl.up.i32", shuffleI32Type));
            ShuffleUpF32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.shfl.up.f32", shuffleF32Type));

            ShuffleXorI32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.shfl.bfly.i32", shuffleI32Type));
            ShuffleXorF32 = new Lazy<LLVMValueRef>(() => LLVM.AddFunction(
                module, "llvm.nvvm.shfl.bfly.f32", shuffleF32Type));

            ShuffleLookup = new Dictionary<WarpIntrinsicKind, KeyValuePair<Lazy<LLVMValueRef>, bool>>()
            {
                { WarpIntrinsicKind.ShuffleI32, new KeyValuePair<Lazy<LLVMValueRef>, bool>(ShuffleI32, true) },
                { WarpIntrinsicKind.ShuffleF32, new KeyValuePair<Lazy<LLVMValueRef>, bool>(ShuffleF32, true) },

                { WarpIntrinsicKind.ShuffleDownI32, new KeyValuePair<Lazy<LLVMValueRef>, bool>(ShuffleDownI32, true) },
                { WarpIntrinsicKind.ShuffleDownF32, new KeyValuePair<Lazy<LLVMValueRef>, bool>(ShuffleDownF32, true) },

                { WarpIntrinsicKind.ShuffleUpI32, new KeyValuePair<Lazy<LLVMValueRef>, bool>(ShuffleUpI32, false) },
                { WarpIntrinsicKind.ShuffleUpF32, new KeyValuePair<Lazy<LLVMValueRef>, bool>(ShuffleUpF32, false) },

                { WarpIntrinsicKind.ShuffleXorI32, new KeyValuePair<Lazy<LLVMValueRef>, bool>(ShuffleXorI32, true) },
                { WarpIntrinsicKind.ShuffleXorF32, new KeyValuePair<Lazy<LLVMValueRef>, bool>(ShuffleXorF32, true) },
            };
        }

        #endregion

        #region Properties

        #region Assertions

        /// <summary>
        /// Represents a failed assertion method inside a Cuda kernel.
        /// </summary>
        public Lazy<LLVMValueRef> AssertFailedMethod { get; private set; }

        #endregion

        #region Atomics

        /// <summary>
        /// Represents an atomic add for f32.
        /// </summary>
        public Lazy<LLVMValueRef> AtomicAddF32 { get; private set; }

        /// <summary>
        /// Represents an atomic inc for u32.
        /// </summary>
        public Lazy<LLVMValueRef> AtomicIncU32 { get; private set; }

        /// <summary>
        /// Represents an atomic dec for u32.
        /// </summary>
        public Lazy<LLVMValueRef> AtomicDecU32 { get; private set; }

        #endregion

        #region Groups

        /// <summary>
        /// Represents a simple group barrier (syncthreads).
        /// </summary>
        public Lazy<LLVMValueRef> GroupBarrier { get; private set; }

        /// <summary>
        /// Represents a simple and group barrier.
        /// </summary>
        public Lazy<LLVMValueRef> GroupBarrierAnd { get; private set; }

        /// <summary>
        /// Represents a simple or group barrier.
        /// </summary>
        public Lazy<LLVMValueRef> GroupBarrierOr { get; private set; }

        /// <summary>
        /// Represents a simple popcount group barrier.
        /// </summary>
        public Lazy<LLVMValueRef> GroupBarrierPopCount { get; private set; }

        #endregion

        #region Memory Fences

        /// <summary>
        /// Represents a block-level memory fence.
        /// </summary>
        public Lazy<LLVMValueRef> BlockLevelFence { get; private set; }

        /// <summary>
        /// Represents a device-level memory fence.
        /// </summary>
        public Lazy<LLVMValueRef> DeviceLevelFence { get; private set; }

        /// <summary>
        /// Represents a system-level memory fence.
        /// </summary>
        public Lazy<LLVMValueRef> SystemLevelFence { get; private set; }

        #endregion

        #region Registers

        /// <summary>
        /// Returns an operation to resolve the x dimension of the
        /// current block index.
        /// </summary>
        public Lazy<LLVMValueRef> GetBlockIdxX { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the y dimension of the
        /// current block index.
        /// </summary>
        public Lazy<LLVMValueRef> GetBlockIdxY { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the z dimension of the
        /// current block index.
        /// </summary>
        public Lazy<LLVMValueRef> GetBlockIdxZ { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the x dimension of
        /// the current grid dimension.
        /// </summary>
        public Lazy<LLVMValueRef> GetGridDimX { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the y dimension of
        /// the current grid dimension.
        /// </summary>
        public Lazy<LLVMValueRef> GetGridDimY { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the z dimension of
        /// the current grid dimension.
        /// </summary>
        public Lazy<LLVMValueRef> GetGridDimZ { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the x dimension of the
        /// current thread index.
        /// </summary>
        public Lazy<LLVMValueRef> GetThreadIdxX { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the y dimension of the
        /// current thread index.
        /// </summary>
        public Lazy<LLVMValueRef> GetThreadIdxY { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the z dimension of the
        /// current thread index.
        /// </summary>
        public Lazy<LLVMValueRef> GetThreadIdxZ { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the x dimension of
        /// the current block dimension.
        /// </summary>
        public Lazy<LLVMValueRef> GetBlockDimX { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the y dimension of
        /// the current block dimension.
        /// </summary>
        public Lazy<LLVMValueRef> GetBlockDimY { get; private set; }

        /// <summary>
        /// Returns an operation to resolve the z dimension of
        /// the current block dimension.
        /// </summary>
        public Lazy<LLVMValueRef> GetBlockDimZ { get; private set; }

        /// <summary>
        /// Returns access to the different grid dimensions.
        /// </summary>
        public Lazy<LLVMValueRef>[] GetGridDimensions { get; private set; }

        /// <summary>
        /// Returns access to the different block dimensions.
        /// </summary>
        public Lazy<LLVMValueRef>[] GetBlockDimensions { get; private set; }

        #endregion

        #region Shuffles

        /// <summary>
        /// Represents a shuffle-up operation for i32.
        /// </summary>
        public Lazy<LLVMValueRef> ShuffleI32 { get; private set; }

        /// <summary>
        /// Represents a shuffle-up operation for f32.
        /// </summary>
        public Lazy<LLVMValueRef> ShuffleF32 { get; private set; }

        /// <summary>
        /// Represents a shuffle-down operation for i32.
        /// </summary>
        public Lazy<LLVMValueRef> ShuffleDownI32 { get; private set; }

        /// <summary>
        /// Represents a shuffle-down operation for f32.
        /// </summary>
        public Lazy<LLVMValueRef> ShuffleDownF32 { get; private set; }

        /// <summary>
        /// Represents a shuffle-up operation for i32.
        /// </summary>
        public Lazy<LLVMValueRef> ShuffleUpI32 { get; private set; }

        /// <summary>
        /// Represents a shuffle-up operation for f32.
        /// </summary>
        public Lazy<LLVMValueRef> ShuffleUpF32 { get; private set; }

        /// <summary>
        /// Represents a shuffle-xor operation for i32.
        /// </summary>
        public Lazy<LLVMValueRef> ShuffleXorI32 { get; private set; }

        /// <summary>
        /// Represents a shuffle-xor operation for f32.
        /// </summary>
        public Lazy<LLVMValueRef> ShuffleXorF32 { get; private set; }

        /// <summary>
        /// Represents a lookup for shuffle intrinsics
        /// </summary>
        public IReadOnlyDictionary<
            WarpIntrinsicKind,
            KeyValuePair<Lazy<LLVMValueRef>, bool>> ShuffleLookup { get; private set; }

        #endregion

        #region Warps

        /// <summary>
        /// Represents a get-warp-size operation.
        /// </summary>
        public Lazy<LLVMValueRef> GetWarpSize { get; private set; }

        /// <summary>
        /// Represents a get-lane-id operation.
        /// </summary>
        public Lazy<LLVMValueRef> GetLaneId { get; private set; }

        #endregion

        #endregion

        #region Methods

        /// <summary cref="CompilerDeviceFunctions.MakeConditionAssert(InvocationContext)"/>
        protected override Value? MakeConditionAssert(InvocationContext context)
        {
            return MakeAssert(
                context,
                context.GetArgs()[0],
                null,
                context.Unit.Name,
                0,
                context.CallerMethod.ManagedFullName);
        }

        /// <summary cref="CompilerDeviceFunctions.MakeMessageAssert(InvocationContext)"/>
        protected override Value? MakeMessageAssert(InvocationContext context)
        {
            var args = context.GetArgs();
            return MakeAssert(
                context,
                args[0],
                args[1],
                context.Unit.Name,
                0,
                context.CallerMethod.ManagedFullName);
        }

        /// <summary>
        /// Builds code to invoke the assertion wrapper for debug assertions.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="message">The assertion message.</param>
        /// <param name="file">The current file name.</param>
        /// <param name="line">The line number.</param>
        /// <param name="func">The current function.</param>
        /// <returns>Null.</returns>
        private static Value? MakeAssert(
            InvocationContext context,
            Value condition,
            Value? message,
            string file,
            int line,
            string func)
        {
            var builder = context.Builder;

            var args = new LLVMValueRef[]
            {
                condition.LLVMValue,
                message.HasValue ? message.Value.LLVMValue : builder.CreateGlobalStringPtr("Assertion Failed", string.Empty),
                builder.CreateGlobalStringPtr(file, string.Empty),
                LLVMExtensions.ConstInt(context.LLVMContext.Int32TypeInContext(), line, false),
                builder.CreateGlobalStringPtr(func, string.Empty)
            };

            var assertFailedMethod = context.Unit.GetMethod(DebugAssertWrapper);
            builder.CreateCall(
                assertFailedMethod.LLVMFunction,
                args,
                string.Empty);

            return null;
        }

        /// <summary>
        /// Represents an assert-wrapper for Debug.Assert functions.
        /// </summary>
        private static void AssertWrapper(
            bool condition,
            string message,
            string file,
            int line,
            string func)
        {
            // We are currently using default 8-bit strings
            if (!condition)
                AssertFailedWrapper(message, file, line, func, 1);
        }

        /// <summary>
        /// Internal wrapper to call the built-in function for failed asserts.
        /// This method will never be compiled or called.
        /// </summary>
        private static void AssertFailedWrapper(
            string message,
            string file,
            int line,
            string func,
            int charSize)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Builds the actual invocation of the built-in assertion-failed method.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>Null.</returns>
        private Value? MakeAssertFailed(InvocationContext context)
        {
            var llvmContext = context.LLVMContext;
            var builder = context.Builder;
            var args = context.GetLLVMArgs();

            // Convert to void*
            args[0] = builder.CreateBitCast(args[0], LLVM.PointerType(llvmContext.VoidTypeInContext(), 0), string.Empty);
            args[1] = builder.CreateBitCast(args[1], LLVM.PointerType(llvmContext.VoidTypeInContext(), 0), string.Empty);
            args[3] = builder.CreateBitCast(args[3], LLVM.PointerType(llvmContext.VoidTypeInContext(), 0), string.Empty);
            // Convert to size_t
            args[4] = builder.CreateIntCast(args[4], context.Unit.NativeIntPtrType, string.Empty);

            builder.CreateCall(AssertFailedMethod.Value, args, string.Empty);

            return null;
        }

        /// <summary>
        /// Creates an atomic operation.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="ptr">The data pointer.</param>
        /// <param name="atomic">The atomic operation.</param>
        /// <returns>The created LLVM operation.</returns>
        private static Value MakeCudaAtomic(InvocationContext context, LLVMValueRef ptr, LLVMValueRef atomic)
        {
            var args = context.GetArgs();
            return new Value(
                args[1].ValueType,
                context.Builder.CreateCall(atomic, new LLVMValueRef[]
            {
                ptr,
                args[1].LLVMValue,
            }, string.Empty));
        }

        /// <summary cref="CompilerDeviceFunctions.MakeAtomicAdd(InvocationContext, LLVMValueRef, AtomicIntrinsicKind)" />
        protected override Value MakeAtomicAdd(InvocationContext context, LLVMValueRef ptr, AtomicIntrinsicKind kind)
        {
            if (kind != AtomicIntrinsicKind.AddF32)
                throw context.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedAtomicOperation, kind);
            return MakeCudaAtomic(context, ptr, AtomicAddF32.Value);
        }

        /// <summary cref="CompilerDeviceFunctions.MakeAtomicInc(InvocationContext, LLVMValueRef, AtomicIntrinsicKind)"/>
        protected override Value MakeAtomicInc(InvocationContext context, LLVMValueRef ptr, AtomicIntrinsicKind kind)
        {
            if (kind != AtomicIntrinsicKind.IncU32)
                throw context.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedAtomicOperation, kind);

            return MakeCudaAtomic(context, ptr, AtomicIncU32.Value);
        }

        /// <summary cref="CompilerDeviceFunctions.MakeAtomicDec(InvocationContext, LLVMValueRef, AtomicIntrinsicKind)"/>
        protected override Value MakeAtomicDec(InvocationContext context, LLVMValueRef ptr, AtomicIntrinsicKind kind)
        {
            if (kind != AtomicIntrinsicKind.DecU32)
                throw context.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedAtomicOperation, kind);

            return MakeCudaAtomic(context, ptr, AtomicDecU32.Value);
        }

        /// <summary cref="CompilerDeviceFunctions.MakeGrid(InvocationContext, GridIntrinsicKind)"/>
        protected override Value? MakeGrid(InvocationContext context, GridIntrinsicKind kind)
        {
            if (context.GetArgs().Length != 0 || context.GetMethodGenericArguments().Length != 0)
                throw context.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedGridIntrinsic, kind);

            Lazy<LLVMValueRef>[] indices;
            switch (kind)
            {
                case GridIntrinsicKind.GetGridDimension:
                    indices = GetGridDimensions;
                    break;
                case GridIntrinsicKind.GetGroupDimension:
                    indices = GetBlockDimensions;
                    break;
                default:
                    throw context.CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedGridIntrinsic, kind);
            }

            Debug.Assert(indices != null, "Invalid grid indices");

            var builder = context.Builder;
            var indexValue = LLVM.GetUndef(context.Unit.GetType(typeof(Index3)));

            for (int i = 0; i < 3; ++i)
            {
                var resolvedValue = builder.CreateCall(indices[i].Value, new LLVMValueRef[] { }, string.Empty);
                indexValue = builder.CreateInsertValue(indexValue, resolvedValue, (uint)i, string.Empty);
            }

            return new Value(typeof(Index3), indexValue);
        }

        /// <summary cref="CompilerDeviceFunctions.MakeGroup(InvocationContext, GroupIntrinsicKind)"/>
        protected override Value? MakeGroup(InvocationContext context, GroupIntrinsicKind kind)
        {
            LLVMValueRef barrierTarget;
            switch (kind)
            {
                case GroupIntrinsicKind.Barrier:
                    context.Builder.CreateCall(GroupBarrier.Value, new LLVMValueRef[] { }, string.Empty);
                    return null;
                case GroupIntrinsicKind.BarrierAnd:
                    barrierTarget = GroupBarrierAnd.Value;
                    break;
                case GroupIntrinsicKind.BarrierOr:
                    barrierTarget = GroupBarrierOr.Value;
                    break;
                case GroupIntrinsicKind.BarrierPopCount:
                    barrierTarget = GroupBarrierPopCount.Value;
                    break;
                default:
                    throw context.CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedGroupIntrinsic, kind);
            }
            Debug.Assert(barrierTarget.Pointer != IntPtr.Zero);

            if (context.GetArgs().Length != 1)
                throw context.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedGroupIntrinsic, kind);

            // Convert arg to int32
            var boolArg = context.GetArgs()[0];
            var arg = context.Builder.CreateZExtOrBitCast(boolArg.LLVMValue, context.LLVMContext.Int32TypeInContext(), string.Empty);
            var result = context.Builder.CreateCall(barrierTarget, new LLVMValueRef[] { arg }, string.Empty);

            // Convert return type to match the managed signature
            var method = context.Method as MethodInfo;
            Debug.Assert(method != null, "Invalid invocation of a group intrinsic");
            if (method.ReturnType == typeof(bool))
            {
                result = context.Builder.CreateICmp(LLVMIntPredicate.LLVMIntEQ, result, LLVMExtensions.ConstInt(
                    context.LLVMContext.Int32TypeInContext(), 1, false), string.Empty);
                result = context.Builder.CreateTrunc(result, context.LLVMContext.Int1TypeInContext(), string.Empty);
            }
            return new Value(method.ReturnType, result);
        }

        /// <summary cref="CompilerDeviceFunctions.MakeMath(InvocationContext, MathIntrinsicKind)"/>
        protected override Value? MakeMath(InvocationContext context, MathIntrinsicKind kind)
        {
            var ptxAttr = context.Method.GetCustomAttribute<PTXMathFunctionAttribute>();
            if (ptxAttr == null)
                throw context.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedMathIntrinsic, kind);
            string funcName = ptxAttr.Name;
            if (context.Unit.HasFlags(CompileUnitFlags.FastMath))
            {
                var ptxFastAttr = context.Method.GetCustomAttribute<PTXFastMathFunctionAttribute>();
                if (ptxFastAttr != null)
                    funcName = ptxFastAttr.Name;
            }

            var func = LLVM.GetNamedFunction(context.Unit.LLVMModule, funcName);
            if (func.Pointer == IntPtr.Zero)
                throw context.CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedMathIntrinsic, kind);

            var args = context.GetArgs();
            var llvmArgs = new LLVMValueRef[args.Length];
            for (int i = 0; i < args.Length; ++i)
                llvmArgs[i] = args[i].LLVMValue;

            var builder = context.Builder;
            var call = builder.CreateCall(func, llvmArgs, string.Empty);

            // Check for required comparison for i32 return-types instead of bool values
            if (ptxAttr.BoolAsInt32)
            {
                call = builder.CreateTrunc(call, context.LLVMContext.Int1TypeInContext(), string.Empty);
            }
            var info = context.Method as MethodInfo;
            Debug.Assert(info != null, "Invalid method invocation");
            return new Value(info.ReturnType, call);
        }

        /// <summary cref="CompilerDeviceFunctions.MakeMemoryFence(InvocationContext, MemoryFenceIntrinsicKind)"/>
        protected override Value? MakeMemoryFence(InvocationContext context, MemoryFenceIntrinsicKind kind)
        {
            LLVMValueRef memoryFenceFunction;
            switch (kind)
            {
                case MemoryFenceIntrinsicKind.GroupLevel:
                    memoryFenceFunction = BlockLevelFence.Value;
                    break;
                case MemoryFenceIntrinsicKind.DeviceLevel:
                    memoryFenceFunction = DeviceLevelFence.Value;
                    break;
                case MemoryFenceIntrinsicKind.SystemLevel:
                    memoryFenceFunction = SystemLevelFence.Value;
                    break;
                default:
                    throw context.CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedMemoryFenceOperation, kind);
            }
            context.Builder.CreateCall(memoryFenceFunction, new LLVMValueRef[] { }, string.Empty);
            return null;
        }

        /// <summary>
        /// Creates a new query of the current warp size.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <returns>A value that represents the current warp size.</returns>
        private LLVMValueRef MakeWarpSize(IRBuilder builder)
        {
            return builder.CreateCall(GetWarpSize.Value, new LLVMValueRef[] { }, "warpSize");
        }

        /// <summary>
        /// Builds a warp-shuffle mask.
        /// </summary>
        /// <param name="unit">The current unit.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="width">The width that was passed by the user.</param>
        /// <param name="addOrMask">True, to add an or mask consisting of (WarpSize - 1).</param>
        /// <returns>A value that represents the desired warp-shuffle mask.</returns>
        private LLVMValueRef BuildWarpShuffleMask(
            CompileUnit unit,
            IRBuilder builder,
            LLVMValueRef width,
            bool addOrMask)
        {
            var warpSize = MakeWarpSize(builder);
            var warpDiff = builder.CreateSub( warpSize, width, string.Empty);
            var result = builder.CreateShl(
                warpDiff,
                LLVM.ConstInt(unit.GetType(BasicValueType.Int32), 8, false),
                string.Empty);
            if (addOrMask)
            {
                var orMask = builder.CreateSub(
                    warpSize,
                    LLVM.ConstInt(unit.GetType(BasicValueType.Int32), 1, false),
                    string.Empty);
                result = builder.CreateOr(
                    result,
                    orMask,
                    string.Empty);
            }
            return result;
        }

        /// <summary cref="CompilerDeviceFunctions.MakeWarp(InvocationContext, WarpIntrinsicKind)"/>
        protected override Value? MakeWarp(InvocationContext context, WarpIntrinsicKind kind)
        {
            var builder = context.Builder;
            LLVMValueRef value;
            switch (kind)
            {
                case WarpIntrinsicKind.WarpSize:
                    value = MakeWarpSize(builder);
                    break;
                case WarpIntrinsicKind.LaneIdx:
                    value = builder.CreateCall(GetLaneId.Value, new LLVMValueRef[] { }, string.Empty);
                    break;
                default:
                    KeyValuePair<Lazy<LLVMValueRef>, bool> shuffleKey;
                    if (!ShuffleLookup.TryGetValue(kind, out shuffleKey))
                        throw context.CompilationContext.GetNotSupportedException(
                            ErrorMessages.NotSupportedWarpIntrinsic, kind);
                    // Create final mask
                    var args = context.GetLLVMArgs();
                    args[2] = BuildWarpShuffleMask(context.Unit, builder, args[2], shuffleKey.Value);
                    // Build desired shuffle instruction
                    value = builder.CreateCall(shuffleKey.Key.Value, args, string.Empty);
                    break;
            }
            return new Value(typeof(int), value);
        }

        #endregion
    }
}
