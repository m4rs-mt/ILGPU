// -----------------------------------------------------------------------------
//                                 LLVM Bindings
//               Generated using a modified ClangSharpPInvokeGenerator
//         ClangSharpPInvokeGenerator Copyright (c) 2015 Mukul Sabharwal
//                    https://github.com/Microsoft/ClangSharp
//
// File: LLVMTypes.cs
//
// -----------------------------------------------------------------------------

using System;
using System.CodeDom.Compiler;
using System.Runtime.InteropServices;


#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE0003 // Remove qualification

namespace ILGPU.LLVM
{
    static class CodeGeneratorConstants
    {
        public const string GeneratorName = "ClangSharpPInvokeGenerator";
        public const string GeneratorVersion = "1.0";
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueMemoryBuffer
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueContext
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueModule
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueType
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueValue
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueBasicBlock
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueBuilder
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueModuleProvider
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaquePassManager
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaquePassRegistry
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueUse
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueAttributeRef
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueDiagnosticInfo
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpInfoSymbol1
    {
        public int @Present;
        [MarshalAs(UnmanagedType.LPStr)] public string @Name;
        public int @Value;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpInfo1
    {
        public LLVMOpInfoSymbol1 @AddSymbol;
        public LLVMOpInfoSymbol1 @SubtractSymbol;
        public int @Value;
        public int @VariantKind;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueTargetData
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueTargetLibraryInfotData
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueTargetMachine
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMTarget
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueGenericValue
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueExecutionEngine
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueMCJITMemoryManager
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMMCJITCompilerOptions
    {
        public int @OptLevel;
        public LLVMCodeModel @CodeModel;
        public int @NoFramePointerElim;
        public int @EnableFastISel;
        public IntPtr @MCJMM;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueLTOModule
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueLTOCodeGenerator
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueThinLTOCodeGenerator
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LTOObjectBuffer
    {
        [MarshalAs(UnmanagedType.LPStr)] public string @Buffer;
        public long @Size;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueObjectFile
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueSectionIterator
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueSymbolIterator
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaqueRelocationIterator
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOrcOpaqueJITStack
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOpaquePassManagerBuilder
    {
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct ssize_t
    {
        public ssize_t(long value)
        {
            this.Value = value;
        }

        public long Value;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMBool
    {
        public LLVMBool(int value)
        {
            this.Value = value;
        }

        public int Value;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMMemoryBufferRef
    {
        public LLVMMemoryBufferRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMContextRef
    {
        public LLVMContextRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMModuleRef
    {
        public LLVMModuleRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMTypeRef
    {
        public LLVMTypeRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMValueRef
    {
        public LLVMValueRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMBasicBlockRef
    {
        public LLVMBasicBlockRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMBuilderRef
    {
        public LLVMBuilderRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMModuleProviderRef
    {
        public LLVMModuleProviderRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMPassManagerRef
    {
        public LLVMPassManagerRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMPassRegistryRef
    {
        public LLVMPassRegistryRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMUseRef
    {
        public LLVMUseRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMAttributeRef
    {
        public LLVMAttributeRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMDiagnosticInfoRef
    {
        public LLVMDiagnosticInfoRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LLVMFatalErrorHandler([MarshalAs(UnmanagedType.LPStr)] string @Reason);

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LLVMDiagnosticHandler(out LLVMOpaqueDiagnosticInfo @param0, IntPtr @param1);

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LLVMYieldCallback(out LLVMOpaqueContext @param0, IntPtr @param1);

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMDisasmContextRef
    {
        public LLVMDisasmContextRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int LLVMOpInfoCallback(IntPtr @DisInfo, int @PC, int @Offset, int @Size, int @TagType, IntPtr @TagBuf);

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr LLVMSymbolLookupCallback(IntPtr @DisInfo, int @ReferenceValue, out int @ReferenceType, int @ReferencePC, out IntPtr @ReferenceName);

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMTargetDataRef
    {
        public LLVMTargetDataRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMTargetLibraryInfoRef
    {
        public LLVMTargetLibraryInfoRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMTargetMachineRef
    {
        public LLVMTargetMachineRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMTargetRef
    {
        public LLVMTargetRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMGenericValueRef
    {
        public LLVMGenericValueRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMExecutionEngineRef
    {
        public LLVMExecutionEngineRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMMCJITMemoryManagerRef
    {
        public LLVMMCJITMemoryManagerRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr LLVMMemoryManagerAllocateCodeSectionCallback();

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr LLVMMemoryManagerAllocateDataSectionCallback();

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int LLVMMemoryManagerFinalizeMemoryCallback(IntPtr @Opaque, out IntPtr @ErrMsg);

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LLVMMemoryManagerDestroyCallback(IntPtr @Opaque);

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMObjectFileRef
    {
        public LLVMObjectFileRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMSectionIteratorRef
    {
        public LLVMSectionIteratorRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMSymbolIteratorRef
    {
        public LLVMSymbolIteratorRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMRelocationIteratorRef
    {
        public LLVMRelocationIteratorRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOrcJITStackRef
    {
        public LLVMOrcJITStackRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOrcModuleHandle
    {
        public LLVMOrcModuleHandle(int value)
        {
            this.Value = value;
        }

        public int Value;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMOrcTargetAddress
    {
        public LLVMOrcTargetAddress(int value)
        {
            this.Value = value;
        }

        public int Value;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int LLVMOrcSymbolResolverFn();

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int LLVMOrcLazyCompileCallbackFn();

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public partial struct LLVMPassManagerBuilderRef
    {
        public LLVMPassManagerBuilderRef(IntPtr pointer)
        {
            this.Pointer = pointer;
        }

        public IntPtr Pointer;
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMVerifierFailureAction : int
    {
        @LLVMAbortProcessAction = 0,
        @LLVMPrintMessageAction = 1,
        @LLVMReturnStatusAction = 2,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMAttribute : int
    {
        @LLVMZExtAttribute = 1,
        @LLVMSExtAttribute = 2,
        @LLVMNoReturnAttribute = 4,
        @LLVMInRegAttribute = 8,
        @LLVMStructRetAttribute = 16,
        @LLVMNoUnwindAttribute = 32,
        @LLVMNoAliasAttribute = 64,
        @LLVMByValAttribute = 128,
        @LLVMNestAttribute = 256,
        @LLVMReadNoneAttribute = 512,
        @LLVMReadOnlyAttribute = 1024,
        @LLVMNoInlineAttribute = 2048,
        @LLVMAlwaysInlineAttribute = 4096,
        @LLVMOptimizeForSizeAttribute = 8192,
        @LLVMStackProtectAttribute = 16384,
        @LLVMStackProtectReqAttribute = 32768,
        @LLVMAlignment = 2031616,
        @LLVMNoCaptureAttribute = 2097152,
        @LLVMNoRedZoneAttribute = 4194304,
        @LLVMNoImplicitFloatAttribute = 8388608,
        @LLVMNakedAttribute = 16777216,
        @LLVMInlineHintAttribute = 33554432,
        @LLVMStackAlignment = 469762048,
        @LLVMReturnsTwice = 536870912,
        @LLVMUWTable = 1073741824,
        @LLVMNonLazyBind = -2147483648,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMOpcode : int
    {
        @LLVMRet = 1,
        @LLVMBr = 2,
        @LLVMSwitch = 3,
        @LLVMIndirectBr = 4,
        @LLVMInvoke = 5,
        @LLVMUnreachable = 7,
        @LLVMAdd = 8,
        @LLVMFAdd = 9,
        @LLVMSub = 10,
        @LLVMFSub = 11,
        @LLVMMul = 12,
        @LLVMFMul = 13,
        @LLVMUDiv = 14,
        @LLVMSDiv = 15,
        @LLVMFDiv = 16,
        @LLVMURem = 17,
        @LLVMSRem = 18,
        @LLVMFRem = 19,
        @LLVMShl = 20,
        @LLVMLShr = 21,
        @LLVMAShr = 22,
        @LLVMAnd = 23,
        @LLVMOr = 24,
        @LLVMXor = 25,
        @LLVMAlloca = 26,
        @LLVMLoad = 27,
        @LLVMStore = 28,
        @LLVMGetElementPtr = 29,
        @LLVMTrunc = 30,
        @LLVMZExt = 31,
        @LLVMSExt = 32,
        @LLVMFPToUI = 33,
        @LLVMFPToSI = 34,
        @LLVMUIToFP = 35,
        @LLVMSIToFP = 36,
        @LLVMFPTrunc = 37,
        @LLVMFPExt = 38,
        @LLVMPtrToInt = 39,
        @LLVMIntToPtr = 40,
        @LLVMBitCast = 41,
        @LLVMAddrSpaceCast = 60,
        @LLVMICmp = 42,
        @LLVMFCmp = 43,
        @LLVMPHI = 44,
        @LLVMCall = 45,
        @LLVMSelect = 46,
        @LLVMUserOp1 = 47,
        @LLVMUserOp2 = 48,
        @LLVMVAArg = 49,
        @LLVMExtractElement = 50,
        @LLVMInsertElement = 51,
        @LLVMShuffleVector = 52,
        @LLVMExtractValue = 53,
        @LLVMInsertValue = 54,
        @LLVMFence = 55,
        @LLVMAtomicCmpXchg = 56,
        @LLVMAtomicRMW = 57,
        @LLVMResume = 58,
        @LLVMLandingPad = 59,
        @LLVMCleanupRet = 61,
        @LLVMCatchRet = 62,
        @LLVMCatchPad = 63,
        @LLVMCleanupPad = 64,
        @LLVMCatchSwitch = 65,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMTypeKind : int
    {
        @LLVMVoidTypeKind = 0,
        @LLVMHalfTypeKind = 1,
        @LLVMFloatTypeKind = 2,
        @LLVMDoubleTypeKind = 3,
        @LLVMX86_FP80TypeKind = 4,
        @LLVMFP128TypeKind = 5,
        @LLVMPPC_FP128TypeKind = 6,
        @LLVMLabelTypeKind = 7,
        @LLVMIntegerTypeKind = 8,
        @LLVMFunctionTypeKind = 9,
        @LLVMStructTypeKind = 10,
        @LLVMArrayTypeKind = 11,
        @LLVMPointerTypeKind = 12,
        @LLVMVectorTypeKind = 13,
        @LLVMMetadataTypeKind = 14,
        @LLVMX86_MMXTypeKind = 15,
        @LLVMTokenTypeKind = 16,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMLinkage : int
    {
        @LLVMExternalLinkage = 0,
        @LLVMAvailableExternallyLinkage = 1,
        @LLVMLinkOnceAnyLinkage = 2,
        @LLVMLinkOnceODRLinkage = 3,
        @LLVMLinkOnceODRAutoHideLinkage = 4,
        @LLVMWeakAnyLinkage = 5,
        @LLVMWeakODRLinkage = 6,
        @LLVMAppendingLinkage = 7,
        @LLVMInternalLinkage = 8,
        @LLVMPrivateLinkage = 9,
        @LLVMDLLImportLinkage = 10,
        @LLVMDLLExportLinkage = 11,
        @LLVMExternalWeakLinkage = 12,
        @LLVMGhostLinkage = 13,
        @LLVMCommonLinkage = 14,
        @LLVMLinkerPrivateLinkage = 15,
        @LLVMLinkerPrivateWeakLinkage = 16,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMVisibility : int
    {
        @LLVMDefaultVisibility = 0,
        @LLVMHiddenVisibility = 1,
        @LLVMProtectedVisibility = 2,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMDLLStorageClass : int
    {
        @LLVMDefaultStorageClass = 0,
        @LLVMDLLImportStorageClass = 1,
        @LLVMDLLExportStorageClass = 2,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMCallConv : int
    {
        @LLVMCCallConv = 0,
        @LLVMFastCallConv = 8,
        @LLVMColdCallConv = 9,
        @LLVMWebKitJSCallConv = 12,
        @LLVMAnyRegCallConv = 13,
        @LLVMX86StdcallCallConv = 64,
        @LLVMX86FastcallCallConv = 65,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMValueKind : int
    {
        @LLVMArgumentValueKind = 0,
        @LLVMBasicBlockValueKind = 1,
        @LLVMMemoryUseValueKind = 2,
        @LLVMMemoryDefValueKind = 3,
        @LLVMMemoryPhiValueKind = 4,
        @LLVMFunctionValueKind = 5,
        @LLVMGlobalAliasValueKind = 6,
        @LLVMGlobalIFuncValueKind = 7,
        @LLVMGlobalVariableValueKind = 8,
        @LLVMBlockAddressValueKind = 9,
        @LLVMConstantExprValueKind = 10,
        @LLVMConstantArrayValueKind = 11,
        @LLVMConstantStructValueKind = 12,
        @LLVMConstantVectorValueKind = 13,
        @LLVMUndefValueValueKind = 14,
        @LLVMConstantAggregateZeroValueKind = 15,
        @LLVMConstantDataArrayValueKind = 16,
        @LLVMConstantDataVectorValueKind = 17,
        @LLVMConstantIntValueKind = 18,
        @LLVMConstantFPValueKind = 19,
        @LLVMConstantPointerNullValueKind = 20,
        @LLVMConstantTokenNoneValueKind = 21,
        @LLVMMetadataAsValueValueKind = 22,
        @LLVMInlineAsmValueKind = 23,
        @LLVMInstructionValueKind = 24,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMIntPredicate : int
    {
        @LLVMIntEQ = 32,
        @LLVMIntNE = 33,
        @LLVMIntUGT = 34,
        @LLVMIntUGE = 35,
        @LLVMIntULT = 36,
        @LLVMIntULE = 37,
        @LLVMIntSGT = 38,
        @LLVMIntSGE = 39,
        @LLVMIntSLT = 40,
        @LLVMIntSLE = 41,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMRealPredicate : int
    {
        @LLVMRealPredicateFalse = 0,
        @LLVMRealOEQ = 1,
        @LLVMRealOGT = 2,
        @LLVMRealOGE = 3,
        @LLVMRealOLT = 4,
        @LLVMRealOLE = 5,
        @LLVMRealONE = 6,
        @LLVMRealORD = 7,
        @LLVMRealUNO = 8,
        @LLVMRealUEQ = 9,
        @LLVMRealUGT = 10,
        @LLVMRealUGE = 11,
        @LLVMRealULT = 12,
        @LLVMRealULE = 13,
        @LLVMRealUNE = 14,
        @LLVMRealPredicateTrue = 15,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMLandingPadClauseTy : int
    {
        @LLVMLandingPadCatch = 0,
        @LLVMLandingPadFilter = 1,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMThreadLocalMode : int
    {
        @LLVMNotThreadLocal = 0,
        @LLVMGeneralDynamicTLSModel = 1,
        @LLVMLocalDynamicTLSModel = 2,
        @LLVMInitialExecTLSModel = 3,
        @LLVMLocalExecTLSModel = 4,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMAtomicOrdering : int
    {
        @LLVMAtomicOrderingNotAtomic = 0,
        @LLVMAtomicOrderingUnordered = 1,
        @LLVMAtomicOrderingMonotonic = 2,
        @LLVMAtomicOrderingAcquire = 4,
        @LLVMAtomicOrderingRelease = 5,
        @LLVMAtomicOrderingAcquireRelease = 6,
        @LLVMAtomicOrderingSequentiallyConsistent = 7,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMAtomicRMWBinOp : int
    {
        @LLVMAtomicRMWBinOpXchg = 0,
        @LLVMAtomicRMWBinOpAdd = 1,
        @LLVMAtomicRMWBinOpSub = 2,
        @LLVMAtomicRMWBinOpAnd = 3,
        @LLVMAtomicRMWBinOpNand = 4,
        @LLVMAtomicRMWBinOpOr = 5,
        @LLVMAtomicRMWBinOpXor = 6,
        @LLVMAtomicRMWBinOpMax = 7,
        @LLVMAtomicRMWBinOpMin = 8,
        @LLVMAtomicRMWBinOpUMax = 9,
        @LLVMAtomicRMWBinOpUMin = 10,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMDiagnosticSeverity : int
    {
        @LLVMDSError = 0,
        @LLVMDSWarning = 1,
        @LLVMDSRemark = 2,
        @LLVMDSNote = 3,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMAttributeIndex : int
    {
        @LLVMAttributeReturnIndex = 0,
        @LLVMAttributeFunctionIndex = -1,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMByteOrdering : int
    {
        @LLVMBigEndian = 0,
        @LLVMLittleEndian = 1,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMCodeGenOptLevel : int
    {
        @LLVMCodeGenLevelNone = 0,
        @LLVMCodeGenLevelLess = 1,
        @LLVMCodeGenLevelDefault = 2,
        @LLVMCodeGenLevelAggressive = 3,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMRelocMode : int
    {
        @LLVMRelocDefault = 0,
        @LLVMRelocStatic = 1,
        @LLVMRelocPIC = 2,
        @LLVMRelocDynamicNoPic = 3,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMCodeModel : int
    {
        @LLVMCodeModelDefault = 0,
        @LLVMCodeModelJITDefault = 1,
        @LLVMCodeModelSmall = 2,
        @LLVMCodeModelKernel = 3,
        @LLVMCodeModelMedium = 4,
        @LLVMCodeModelLarge = 5,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMCodeGenFileType : int
    {
        @LLVMAssemblyFile = 0,
        @LLVMObjectFile = 1,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMLinkerMode : int
    {
        @LLVMLinkerDestroySource = 0,
        @LLVMLinkerPreserveSource_Removed = 1,
    }

    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public enum LLVMOrcErrorCode : int
    {
        @LLVMOrcErrSuccess = 0,
        @LLVMOrcErrGeneric = 1,
    }

}

#pragma warning restore IDE0003 // Remove qualification
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
