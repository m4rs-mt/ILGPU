// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.ABI;
using ILGPU.Compiler;
using ILGPU.Resources;
using ILGPU.Util;
using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a PTX architecture.
    /// </summary>
    public enum PTXArchitecture
    {
        /// <summary>
        /// The 2.0 architecture.
        /// </summary>
        SM_20,

        /// <summary>
        /// The 2.1 architecture.
        /// </summary>
        SM_21,

        /// <summary>
        /// The 3.0 architecture.
        /// </summary>
        SM_30,

        /// <summary>
        /// The 3.5 architecture.
        /// </summary>
        SM_35,

        /// <summary>
        /// The 3.7 architecture.
        /// </summary>
        SM_37,

        /// <summary>
        /// The 5.0 architecture.
        /// </summary>
        SM_50,

        /// <summary>
        /// The 5.2 architecture.
        /// </summary>
        SM_52,

        /// <summary>
        /// The 6.0 architecture.
        /// </summary>
        SM_60,

        /// <summary>
        /// The 6.1 architecture.
        /// </summary>
        SM_61,

        /// <summary>
        /// The 6.2 architecture.
        /// </summary>
        SM_62,
    }

    /// <summary>
    /// Represents a PTX (Cuda) backend.
    /// </summary>
    public sealed class PTXBackend : LLVMBackend
    {
        #region Constants

        internal const string CudaKernelCategory = "PTXKernel";

        const string X86Triple = "nvptx-nvidia-cuda";
        const string X64Triple = "nvptx64-nvidia-cuda";

        const string X86Layout = "e-p:32:32:32-i1:8:8-i8:8:8-i16:16:16-i32:32:32-i64:64:64-f32:32:32-f64:64:64-v16:16:16-v32:32:32-v64:64:64-v128:128:128-n16:32:64";
        const string X64Layout = "e-p:64:64:64-i1:8:8-i8:8:8-i16:16:16-i32:32:32-i64:64:64-f32:32:32-f64:64:64-v16:16:16-v32:32:32-v64:64:64-v128:128:128-n16:32:64";

        #endregion

        #region Static

        /// <summary>
        /// Maps major and minor versions of Cuda devices to their corresponding PTX architecture.
        /// </summary>
        private static readonly IReadOnlyDictionary<ulong, PTXArchitecture> ArchitectureLookup =
            new Dictionary<ulong, PTXArchitecture>
        {
            { (2L << 32) | 0L, PTXArchitecture.SM_20 },
            { (2L << 32) | 1L, PTXArchitecture.SM_21 },

            { (3L << 32) | 0L, PTXArchitecture.SM_30 },
            { (3L << 32) | 5L, PTXArchitecture.SM_35 },
            { (3L << 32) | 7L, PTXArchitecture.SM_37 },

            { (5L << 32) | 0L, PTXArchitecture.SM_50 },
            { (5L << 32) | 2L, PTXArchitecture.SM_52 },

            { (6L << 32) | 0L, PTXArchitecture.SM_60 },
            { (6L << 32) | 1L, PTXArchitecture.SM_61 },
            { (6L << 32) | 2L, PTXArchitecture.SM_62 },
        };

        /// <summary>
        /// Resolves the PTX architecture for the given major and minor versions.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <returns>The resolved PTX version.</returns>
        [CLSCompliant(false)]
        public static PTXArchitecture GetArchitecture(uint major, uint minor)
        {
            if (!ArchitectureLookup.TryGetValue(((ulong)major << 32) | minor, out PTXArchitecture result))
                return PTXArchitecture.SM_50;
            return result;
        }

        /// <summary>
        /// Returns the appropriate triple for the nvptx backend.
        /// </summary>
        /// <param name="platform">The target platform.</param>
        /// <returns>The appropriate triple for the nvptx backend.</returns>
        static string GetLLVMTriple(TargetPlatform platform)
        {
            return platform == TargetPlatform.X86 ? X86Triple : X64Triple;
        }

        /// <summary>
        /// Returns the appropriate layout for the nvptx backend.
        /// </summary>
        /// <param name="platform">The target platform.</param>
        /// <returns>The appropriate triple for the nvptx backend.</returns>
        static string GetLLVMLayout(TargetPlatform platform)
        {
            return platform == TargetPlatform.X86 ? X86Layout : X64Layout;
        }

        /// <summary>
        /// Determines the current lib-device directory.
        /// </summary>
        /// <returns>The current lib-device directory.</returns>
        public static string ResolveLibDeviceDir()
        {
            var path = Environment.GetEnvironmentVariable("CUDA_PATH");
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                throw new InvalidOperationException(ErrorMessages.CudaPathNotFound);
            path = Path.Combine(path, "nvvm/libdevice");
            if (!Directory.Exists(path))
                throw new InvalidOperationException(ErrorMessages.LibDevicePathNotFound);
            return path;
        }

        /// <summary>
        /// Resolves a lib-device-filename pattern for the given gpu architecture.
        /// </summary>
        /// <returns>The resolved lib-device pattern.</returns>
        static string ResolveLibDevicePattern(PTXArchitecture gpuArch)
        {
            switch (gpuArch)
            {
                case PTXArchitecture.SM_30:
                case PTXArchitecture.SM_60:
                case PTXArchitecture.SM_61:
                case PTXArchitecture.SM_62:
                    return "libdevice.compute_30.*.bc";
                case PTXArchitecture.SM_20:
                case PTXArchitecture.SM_21:
                    return "libdevice.compute_20.*.bc";
                case PTXArchitecture.SM_35:
                case PTXArchitecture.SM_37:
                    return "libdevice.compute_35.*.bc";
                case PTXArchitecture.SM_50:
                case PTXArchitecture.SM_52:
                    return "libdevice.compute_50.*.bc";
                default:
                    throw new ArgumentOutOfRangeException(nameof(gpuArch));
            }
        }

        /// <summary>
        /// Initializes the PTX backend.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static PTXBackend()
        {
            LLVM.InitializeNVPTXAsmPrinter();
            LLVM.InitializeNVPTXTarget();
            LLVM.InitializeNVPTXTargetInfo();
            LLVM.InitializeNVPTXTargetMC();
        }

        #endregion

        #region Instance

        private readonly Dictionary<CompileUnit, PTXDeviceFunctions> ptxDeviceFunctions =
            new Dictionary<CompileUnit, PTXDeviceFunctions>();

        /// <summary>
        /// Constructs a new Cuda backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="gpuArch">The target gpu architecture.</param>
        /// <param name="platform">The target platform.</param>
        /// <param name="libDeviceDir">The directory that contains the different libdevice libraries.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.String.ToLower", Justification = "ToLower is only used for LLVM-interop functionality")]
        public PTXBackend(Context context, PTXArchitecture gpuArch, TargetPlatform platform, string libDeviceDir)
            : base(context, platform, GetLLVMTriple(platform), gpuArch.ToString().ToLower())
        {
            // Determine the correct lib-device version.
            var pattern = ResolveLibDevicePattern(gpuArch);
            var files = Directory.GetFiles(libDeviceDir, pattern);
            if (files.Length < 1)
                throw new ArgumentException(ErrorMessages.LibDeviceNotFound);
            LibDevicePath = files[0];
        }

        /// <summary>
        /// Constructs a new Cuda backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="gpuArch">The target gpu architecture.</param>
        /// <param name="libDeviceDir">The directory that contains the different libdevice libraries.</param>
        public PTXBackend(Context context, PTXArchitecture gpuArch, string libDeviceDir)
            : this(context, gpuArch, RuntimePlatform, libDeviceDir)
        { }

        /// <summary>
        /// Constructs a new Cuda backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="gpuArch">The target gpu architecture.</param>
        public PTXBackend(Context context, PTXArchitecture gpuArch)
            : this(context, gpuArch, RuntimePlatform, ResolveLibDeviceDir())
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the path to the used lib-device library.
        /// </summary>
        public string LibDevicePath { get; }

        #endregion

        #region Methods

        /// <summary cref="Backend.TargetUnit(CompileUnit)"/>
        internal override void TargetUnit(CompileUnit unit)
        {
            var module = unit.LLVMModule;
            var dataLayout = GetLLVMLayout(Platform);
            var targetTriple = GetLLVMTriple(Platform);

            LLVM.SetDataLayout(module, dataLayout);
            LLVM.SetTarget(module, targetTriple);

            if (LLVM.CreateMemoryBufferWithContentsOfFile(LibDevicePath, out LLVMMemoryBufferRef libDeviceBuffer, out IntPtr errorMessage))
                throw new InvalidOperationException(string.Format(
                    ErrorMessages.CouldNotReadLibDevice, Marshal.PtrToStringAnsi(errorMessage)));
            if (LLVM.GetBitcodeModuleInContext(unit.LLVMContext, libDeviceBuffer, out LLVMModuleRef libDeviceModule, out errorMessage))
                throw new InvalidOperationException(string.Format(
                    ErrorMessages.CouldNotLoadLibDevice, Marshal.PtrToStringAnsi(errorMessage)));
            LLVM.SetDataLayout(libDeviceModule, dataLayout);
            LLVM.SetTarget(libDeviceModule, targetTriple);
            LLVM.LinkModules2(module, libDeviceModule);

            var functions = new PTXDeviceFunctions(unit);
            ptxDeviceFunctions.Add(unit, functions);
            unit.RegisterDeviceFunctions(functions);
        }

        /// <summary cref="Backend.CreateABISpecification(CompileUnit)"/>
        internal override ABISpecification CreateABISpecification(CompileUnit unit)
        {
            return new PTXABI(unit);
        }

        /// <summary>
        /// Creates a signature for the actual kernel entry point.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="entryPoint">The target entry point.</param>
        /// <param name="parameterOffset">The parameter offset for the actual kernel parameters.</param>
        /// <returns>A signature for the actual kernel entry point.</returns>
        private LLVMTypeRef CreatePTXKernelFunctionType(
            CompileUnit unit,
            EntryPoint entryPoint,
            out int parameterOffset)
        {
            parameterOffset = entryPoint.IsGroupedIndexEntry ? 0 : 1;
            var numUniformVariables = entryPoint.NumUniformVariables;
            var argTypes = new LLVMTypeRef[parameterOffset + numUniformVariables + entryPoint.NumDynamicallySizedSharedMemoryVariables];

            // Custom dispatch-size information for implicitly grouped kernels
            if (parameterOffset > 0)
                argTypes[0] = unit.GetType(entryPoint.UngroupedIndexType);

            Debug.Assert(parameterOffset >= 0 && parameterOffset < 2);

            for (int i = 0, e = numUniformVariables; i < e ; ++i)
                argTypes[i + parameterOffset] = unit.GetType(entryPoint.UniformVariables[i].VariableType);

            // Attach length information to dynamically sized variables using runtime information
            for (int i = 0, e = entryPoint.NumDynamicallySizedSharedMemoryVariables; i < e; ++i)
                argTypes[i + parameterOffset + numUniformVariables] = unit.GetType(typeof(int));

            return LLVM.FunctionType(Context.LLVMContext.VoidTypeInContext(), argTypes, false);
        }

        /// <summary>
        /// Creates an <see cref="Index3"/> in the LLVM world containing the current grid indices.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="builder">The LLVM builder.</param>
        /// <param name="cudaDeviceFunctions">A reference to the cuda device functions.</param>
        /// <returns>An <see cref="Index3"/> in the LLVM world containg the current grid indices.</returns>
        private static LLVMValueRef CreateIndexValue(
            CompileUnit unit,
            EntryPoint entryPoint,
            IRBuilder builder,
            PTXDeviceFunctions cudaDeviceFunctions)
        {
            var indexType = unit.GetType(entryPoint.UngroupedIndexType);
            var indexValue = LLVM.GetUndef(indexType);

            Debug.Assert(entryPoint.Type >= IndexType.Index1D);

            indexValue = builder.CreateInsertValue(indexValue, builder.CreateCall(
                cudaDeviceFunctions.GetBlockIdxX.Value, new LLVMValueRef[] { }, "GetBlockIdxX"), 0, "Idx1");

            if (entryPoint.Type >= IndexType.Index2D && entryPoint.Type <= IndexType.Index3D ||
                entryPoint.Type >= IndexType.GroupedIndex2D)
                indexValue = builder.CreateInsertValue(indexValue, builder.CreateCall(
                    cudaDeviceFunctions.GetBlockIdxY.Value, new LLVMValueRef[] { }, "GetBlockIdxY"), 1, "Idx2");
            if (entryPoint.Type == IndexType.Index3D || entryPoint.Type == IndexType.GroupedIndex3D)
                indexValue = builder.CreateInsertValue(indexValue, builder.CreateCall(
                    cudaDeviceFunctions.GetBlockIdxZ.Value, new LLVMValueRef[] { }, "GetBlockIdxZ"), 2, "Idx3");

            return indexValue;
        }

        /// <summary>
        /// Creates an <see cref="Index3"/> in the LLVM world containing the current group-thread indices.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="builder">The LLVM builder.</param>
        /// <param name="cudaDeviceFunctions">A reference to the cuda device functions.</param>
        /// <returns>An <see cref="Index3"/> in the LLVM world containg the current group-thread indices.</returns>
        private static LLVMValueRef CreateGroupIndexValue(
            CompileUnit unit,
            EntryPoint entryPoint,
            IRBuilder builder,
            PTXDeviceFunctions cudaDeviceFunctions)
        {
            var indexType = unit.GetType(entryPoint.UngroupedIndexType);
            var threadIndexValue = LLVM.GetUndef(indexType);

            Debug.Assert(entryPoint.Type >= IndexType.Index1D);

            var isGroupedIndex = entryPoint.IsGroupedIndexEntry;

            threadIndexValue = builder.CreateInsertValue(threadIndexValue, builder.CreateCall(
                cudaDeviceFunctions.GetThreadIdxX.Value, new LLVMValueRef[] { }, "GetThreadIdxX"), 0, "TIdx1");

            if (entryPoint.Type >= IndexType.Index2D && !isGroupedIndex || entryPoint.Type >= IndexType.GroupedIndex2D)
                threadIndexValue = builder.CreateInsertValue(threadIndexValue, builder.CreateCall(
                    cudaDeviceFunctions.GetThreadIdxY.Value, new LLVMValueRef[] { }, "GetThreadIdxY"), 1, "TIdx2");
            if (entryPoint.Type >= IndexType.Index3D && !isGroupedIndex || entryPoint.Type >= IndexType.GroupedIndex3D)
                threadIndexValue = builder.CreateInsertValue(threadIndexValue, builder.CreateCall(
                    cudaDeviceFunctions.GetThreadIdxZ.Value, new LLVMValueRef[] { }, "GetThreadIdxZ"), 2, "TIdx3");

            return threadIndexValue;
        }

        /// <summary>
        /// Creates an <see cref="Index3"/> in the LLVM world containing the current global indices
        /// (gridIdx * blockDim + blockIdx).
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="builder">The LLVM builder.</param>
        /// <param name="cudaDeviceFunctions">A reference to the cuda device functions.</param>
        /// <param name="indexValue">The current grid-index value (gridIdx).</param>
        /// <param name="groupIndexValue">The current group-thread-index value (blockIdx).</param>
        /// <returns>An <see cref="Index3"/> in the LLVM world containg the current global indices.</returns>
        private static LLVMValueRef CreateGlobalIndexValue(
            CompileUnit unit,
            EntryPoint entryPoint,
            IRBuilder builder,
            PTXDeviceFunctions cudaDeviceFunctions,
            LLVMValueRef indexValue,
            LLVMValueRef groupIndexValue)
        {
            var indexType = unit.GetType(entryPoint.UngroupedIndexType);
            var globalIndexValue = LLVM.GetUndef(indexType);

            Debug.Assert(entryPoint.Type >= IndexType.Index1D && entryPoint.Type < IndexType.GroupedIndex1D);
            var blockDimensions = cudaDeviceFunctions.GetBlockDimensions;

            for (int i = 0, e = (int)entryPoint.Type; i < e; ++i)
            {
                var globalGroupOffset = builder.CreateMul(
                    builder.CreateExtractValue(
                        indexValue,
                        (uint)i,
                        "GridIdx_" + i),
                    builder.CreateCall(
                        blockDimensions[i].Value,
                        new LLVMValueRef[] { },
                        "GetBlockDim_" + i),
                    "GlobalGroupOffset_" + i);

                var globalIdx = builder.CreateAdd(
                    globalGroupOffset,
                    builder.CreateExtractValue(
                        groupIndexValue,
                        (uint)i,
                        "GroupIdx_" + i),
                    "GlobalIdxVal_" + i);

                globalIndexValue = builder.CreateInsertValue(
                    globalIndexValue,
                    globalIdx,
                    (uint)i,
                    "GlobalIdx_" + i);
            }

            return globalIndexValue;
        }

        /// <summary>
        /// Creates a comparison of the current global index to the custom desired number of user threads.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="builder">The LLVM builder.</param>
        /// <param name="cudaDeviceFunctions">A reference to the cuda device functions.</param>
        /// <param name="globalIndexValue">The current global index values.</param>
        /// <param name="userIndexRange">The user given custom launcher range.</param>
        /// <returns>An instance of an <see cref="IGroupedIndex{TIndex}"/> in the LLVM world.</returns>
        private static LLVMValueRef CreateGlobalIndexRangeComparison(
            CompileUnit unit,
            EntryPoint entryPoint,
            IRBuilder builder,
            PTXDeviceFunctions cudaDeviceFunctions,
            LLVMValueRef globalIndexValue,
            LLVMValueRef userIndexRange)
        {
            Debug.Assert(entryPoint.Type >= IndexType.Index1D && entryPoint.Type < IndexType.GroupedIndex1D);

            LLVMValueRef comparisonValue = LLVM.ConstInt(LLVM.Int1TypeInContext(unit.LLVMContext), 1, false);
            for (int i = 0, e = (int)entryPoint.Type; i <= e; ++i)
            {
                var compareResult = builder.CreateICmp(
                    LLVMIntPredicate.LLVMIntSLT,
                    builder.CreateExtractValue(globalIndexValue, 0, "GlobalIdx_" + i),
                    builder.CreateExtractValue(userIndexRange, 0, "UserRange_" + i),
                    "InRange_" + i);
                comparisonValue = builder.CreateAnd(
                    comparisonValue,
                    compareResult,
                    "RangeOr_" + i);
            }

            return comparisonValue;
        }

        /// <summary>
        /// Creates an instance of an <see cref="IGroupedIndex{TIndex}"/> in the LLVM world.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="builder">The LLVM builder.</param>
        /// <param name="cudaDeviceFunctions">A reference to the cuda device functions.</param>
        /// <param name="indexValue">The current index values (first part of a grouped index).</param>
        /// <param name="groupIndexValue">The current group-index values (second part of a grouped index).</param>
        /// <returns>An instance of an <see cref="IGroupedIndex{TIndex}"/> in the LLVM world.</returns>
        private static LLVMValueRef CreateGroupedIndex(
            CompileUnit unit,
            EntryPoint entryPoint,
            IRBuilder builder,
            PTXDeviceFunctions cudaDeviceFunctions,
            LLVMValueRef indexValue,
            LLVMValueRef groupIndexValue)
        {
            Debug.Assert(entryPoint.Type >= IndexType.GroupedIndex1D);

            // Create a new blocked index
            var blockIndexValue = LLVM.GetUndef(unit.GetType(entryPoint.KernelIndexType));
            blockIndexValue = builder.CreateInsertValue(blockIndexValue, indexValue, 0, "GridIdx");
            blockIndexValue = builder.CreateInsertValue(blockIndexValue, groupIndexValue, 1, "GroupIdx");
            return blockIndexValue;
        }

        /// <summary>
        /// Declares a new shared-memory variable.
        /// </summary>
        /// <param name="unit">The target compile unit.</param>
        /// <param name="builder">The current IR builder.</param>
        /// <param name="type">The type of the variable to declare.</param>
        /// <returns>A GEP value that points to the base address of the shared-memory variable.</returns>
        private static LLVMValueRef DeclareSharedMemoryVariable(CompileUnit unit, IRBuilder builder, LLVMTypeRef type)
        {
            var globalName = unit.GetLLVMName("Global", "SharedMem");
            var sharedMem = LLVM.AddGlobalInAddressSpace(unit.LLVMModule, type, globalName, 3);
            return builder.CreateAddrSpaceCast(
                sharedMem,
                LLVM.PointerType(unit.LLVMContext.VoidTypeInContext(), 0),
                string.Empty);
        }

        /// <summary cref="LLVMBackend.CreateEntry(CompileUnit, EntryPoint, out string)"/>
        internal override LLVMValueRef CreateEntry(CompileUnit unit, EntryPoint entryPoint, out string entryPointName)
        {
            if (!ptxDeviceFunctions.TryGetValue(unit, out PTXDeviceFunctions deviceFunctions))
                throw new InvalidOperationException(ErrorMessages.NotSupportedCompileUnit);

            entryPointName = unit.GetLLVMName(entryPoint.MethodInfo, CudaKernelCategory);
            var context = unit.LLVMContext;
            var module = unit.LLVMModule;

            LLVMValueRef cudaEntryPoint = LLVM.GetNamedFunction(module, entryPointName);
            if (cudaEntryPoint.Pointer != IntPtr.Zero)
            {
                cudaEntryPoint.SetLinkage(LLVMLinkage.LLVMExternalLinkage);
                return cudaEntryPoint;
            }

            var entryPointType = CreatePTXKernelFunctionType(unit, entryPoint, out int parameterOffset);
            cudaEntryPoint = LLVM.AddFunction(module, entryPointName, entryPointType);
            cudaEntryPoint.SetLinkage(LLVMLinkage.LLVMExternalLinkage);

            var entryBlock = cudaEntryPoint.AppendBasicBlock("Main");
            var exitBlock = cudaEntryPoint.AppendBasicBlock("Exit");
            using (var builder = new IRBuilder(unit.LLVMContext))
            {
                builder.PositionBuilderAtEnd(entryBlock);

                // Create a proper entry point for the virtual entry point
                var indexValue = CreateIndexValue(unit, entryPoint, builder, deviceFunctions);
                var groupIndexValue = CreateGroupIndexValue(unit, entryPoint, builder, deviceFunctions);
                if  (!entryPoint.IsGroupedIndexEntry)
                {
                    // We have to generate code for an implictly grouped kernel
                    // -> Compute the actual global idx
                    indexValue = CreateGlobalIndexValue(
                        unit,
                        entryPoint,
                        builder,
                        deviceFunctions,
                        indexValue,
                        groupIndexValue);

                    // Append a new main block that contains the actual body
                    var mainBlock = cudaEntryPoint.AppendBasicBlock("Core");

                    // Emit the required check (custom dimension size is stored in parameter 0).
                    // This check is required to ensure that the index is always smaller than the
                    // specified user size. Otherwise, the index might be larger due to custom blocking!
                    Debug.Assert(parameterOffset > 0);
                    var rangeComparisonResult = CreateGlobalIndexRangeComparison(
                        unit,
                        entryPoint,
                        builder,
                        deviceFunctions,
                        indexValue,
                        cudaEntryPoint.GetParam(0));
                    builder.CreateCondBr(rangeComparisonResult, mainBlock, exitBlock);

                    // Move builder to main block to emit the actual kernel body
                    builder.PositionBuilderAtEnd(mainBlock);
                }
                else
                {
                    Debug.Assert(parameterOffset < 1);
                    indexValue = CreateGroupedIndex(
                        unit,
                        entryPoint,
                        builder,
                        deviceFunctions,
                        indexValue,
                        groupIndexValue);
                }

                // Call the virtual entry point
                LLVMValueRef[] kernelValues = new LLVMValueRef[entryPoint.NumCustomParameters + 1];
                kernelValues[0] = indexValue;

                var kernelParameters = cudaEntryPoint.GetParams();
                var uniformVariables = entryPoint.UniformVariables;
                for (int i = 0, kernelParamIdx = parameterOffset, e = uniformVariables.Length; i < e; ++i, ++kernelParamIdx)
                {
                    var variable = uniformVariables[i];
                    LLVMValueRef kernelParam;
                    var kernelValue = kernelParam = kernelParameters[kernelParamIdx];
                    if (variable.VariableType.IsPassedViaPtr())
                    {
                        // We have to generate a local alloca and store the current parameter value
                        kernelValue = builder.CreateAlloca(kernelParam.TypeOf(), string.Empty);
                        builder.CreateStore(kernelParam, kernelValue);
                    }
                    kernelValues[variable.Index] = kernelValue;
                }

                var sharedMemoryVariables = entryPoint.SharedMemoryVariables;
                foreach (var variable in sharedMemoryVariables)
                {
                    // This type can be: ArrayType<T> or VariableType<T>
                    var variableType = unit.GetType(variable.Type);
                    var variableElementType = unit.GetType(variable.ElementType);
                    var sharedVariable = LLVM.GetUndef(variableType);
                    if (variable.IsArray)
                    {
                        // However, ArrayType<T> encapsulates the type ArrayView<T, Index>
                        var genericArrayView = LLVM.GetUndef(variableType.GetStructElementTypes()[0]);
                        var arrayType = LLVM.ArrayType(variableElementType, (uint)(variable.Count != null ? variable.Count.Value : 0));
                        var sharedMem = DeclareSharedMemoryVariable(unit, builder, arrayType);
                        genericArrayView = builder.CreateInsertValue(genericArrayView, sharedMem, 0, string.Empty);
                        LLVMValueRef intIndex;

                        if (variable.Count != null)
                            intIndex = LLVMExtensions.ConstInt(context.Int32TypeInContext(), variable.Count.Value, false);
                        else
                        {
                            // Attach the right length information that is given via a parameter
                            Debug.Assert(variable.SharedMemoryIndex >= 0);
                            intIndex = kernelParameters[uniformVariables.Length + variable.SharedMemoryIndex];
                        }

                        var indexInstance = LLVM.GetUndef(unit.GetType(typeof(Index)));
                        indexInstance = builder.CreateInsertValue(indexInstance, intIndex, 0, string.Empty);
                        genericArrayView = builder.CreateInsertValue(genericArrayView, indexInstance, 1, string.Empty);
                        sharedVariable = builder.CreateInsertValue(sharedVariable, genericArrayView, 0, string.Empty);
                    }
                    else
                    {
                        var sharedMem = DeclareSharedMemoryVariable(unit, builder, variableElementType);
                        // Insert pointer into variable view
                        sharedVariable = builder.CreateInsertValue(sharedVariable, sharedMem, 0, string.Empty);
                    }


                    // Setup the pointer as generic pointer
                    kernelValues[variable.Index] = sharedVariable;
                }

                // Declare external entry point
                var virtualEntryPoint = unit.GetMethod(entryPoint.MethodInfo);
                builder.CreateCall(virtualEntryPoint.LLVMFunction, kernelValues, string.Empty);

                // Verify method access in the scope of implicitly-grouped kernels
                if (!entryPoint.IsGroupedIndexEntry)
                {
                    virtualEntryPoint.VisitCalls((instruction, calledMethod) =>
                    {
                        CodeGenerator.VerifyAccessToMethodInImplicitlyGroupedKernel(
                            unit.CompilationContext,
                            calledMethod.MethodBase,
                            entryPoint);
                    });
                }

                // Jump to exit block
                builder.CreateBr(exitBlock);

                // Build exit block
                builder.PositionBuilderAtEnd(exitBlock);
                builder.CreateRetVoid();
            }

            unit.Optimize();

            return cudaEntryPoint;
        }

        /// <summary cref="LLVMBackend.PrepareModule(CompileUnit, LLVMModuleRef, EntryPoint, LLVMValueRef)"/>
        internal override void PrepareModule(CompileUnit unit, LLVMModuleRef module, EntryPoint entryPoint, LLVMValueRef generatedEntryPoint)
        {
            // Add required metdata for entry point
            var context = unit.LLVMContext;

            var kernelMd = context.MDNodeInContext(new LLVMValueRef[]
            {
                generatedEntryPoint,
                context.MDStringInContext("kernel", 6),
                LLVM.ConstInt(context.Int32TypeInContext(), 1, false)
            });

            LLVM.AddNamedMetadataOperand(module, "nvvm.annotations", kernelMd);

            // Perform NVVM reflect pass
            LLVMExtensions.PreparePTXModule(
                module,
                generatedEntryPoint,
                unit.HasFlags(CompileUnitFlags.PTXFlushDenormalsToZero | CompileUnitFlags.FastMath),
                unit.HasFlags(CompileUnitFlags.FastMath));
        }

        #endregion
    }
}
