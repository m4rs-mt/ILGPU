// -----------------------------------------------------------------------------
//                                  LLVM Bindings
//               Generated using a modified ClangSharpPInvokeGenerator
//         ClangSharpPInvokeGenerator Copyright (c) 2015 Mukul Sabharwal
//                    https://github.com/Microsoft/ClangSharp
//
// File: LLVMMethods.cs
//
// -----------------------------------------------------------------------------

using System;
using System.CodeDom.Compiler;
using System.Runtime.InteropServices;


#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE0003 // Remove qualification

namespace ILGPU.LLVM
{
    [GeneratedCode(CodeGeneratorConstants.GeneratorName, CodeGeneratorConstants.GeneratorVersion)]
    public static partial class LLVMMethods
    {
        public const string LibraryName = "LLVM";

        [DllImport(LibraryName, EntryPoint = "LLVMVerifyModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool VerifyModule(LLVMModuleRef @M, LLVMVerifierFailureAction @Action, out IntPtr @OutMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMVerifyFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool VerifyFunction(LLVMValueRef @Fn, LLVMVerifierFailureAction @Action);

        [DllImport(LibraryName, EntryPoint = "LLVMViewFunctionCFG", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ViewFunctionCFG(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMViewFunctionCFGOnly", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ViewFunctionCFGOnly(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMParseBitcode", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool ParseBitcode(LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutModule, out IntPtr @OutMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMParseBitcode2", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool ParseBitcode2(LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutModule);

        [DllImport(LibraryName, EntryPoint = "LLVMParseBitcodeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool ParseBitcodeInContext(LLVMContextRef @ContextRef, LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutModule, out IntPtr @OutMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMParseBitcodeInContext2", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool ParseBitcodeInContext2(LLVMContextRef @ContextRef, LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutModule);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBitcodeModuleInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool GetBitcodeModuleInContext(LLVMContextRef @ContextRef, LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutM, out IntPtr @OutMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBitcodeModuleInContext2", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool GetBitcodeModuleInContext2(LLVMContextRef @ContextRef, LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutM);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBitcodeModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool GetBitcodeModule(LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutM, out IntPtr @OutMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBitcodeModule2", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool GetBitcodeModule2(LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutM);

        [DllImport(LibraryName, EntryPoint = "LLVMWriteBitcodeToFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern int WriteBitcodeToFile(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Path);

        [DllImport(LibraryName, EntryPoint = "LLVMWriteBitcodeToFD", CallingConvention = CallingConvention.Cdecl)]
        public static extern int WriteBitcodeToFD(LLVMModuleRef @M, int @FD, int @ShouldClose, int @Unbuffered);

        [DllImport(LibraryName, EntryPoint = "LLVMWriteBitcodeToFileHandle", CallingConvention = CallingConvention.Cdecl)]
        public static extern int WriteBitcodeToFileHandle(LLVMModuleRef @M, int @Handle);

        [DllImport(LibraryName, EntryPoint = "LLVMWriteBitcodeToMemoryBuffer", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMMemoryBufferRef WriteBitcodeToMemoryBuffer(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMInstallFatalErrorHandler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InstallFatalErrorHandler(LLVMFatalErrorHandler @Handler);

        [DllImport(LibraryName, EntryPoint = "LLVMResetFatalErrorHandler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ResetFatalErrorHandler();

        [DllImport(LibraryName, EntryPoint = "LLVMEnablePrettyStackTrace", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EnablePrettyStackTrace();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeCore", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeCore(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMShutdown", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Shutdown();

        [DllImport(LibraryName, EntryPoint = "LLVMCreateMessage", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateMessage([MarshalAs(UnmanagedType.LPStr)] string @Message);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeMessage", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeMessage(IntPtr @Message);

        [DllImport(LibraryName, EntryPoint = "LLVMContextCreate", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMContextRef ContextCreate();

        [DllImport(LibraryName, EntryPoint = "LLVMGetGlobalContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMContextRef GetGlobalContext();

        [DllImport(LibraryName, EntryPoint = "LLVMContextSetDiagnosticHandler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ContextSetDiagnosticHandler(LLVMContextRef @C, LLVMDiagnosticHandler @Handler, IntPtr @DiagnosticContext);

        [DllImport(LibraryName, EntryPoint = "LLVMContextGetDiagnosticHandler", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMDiagnosticHandler ContextGetDiagnosticHandler(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMContextGetDiagnosticContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ContextGetDiagnosticContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMContextSetYieldCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ContextSetYieldCallback(LLVMContextRef @C, LLVMYieldCallback @Callback, IntPtr @OpaqueHandle);

        [DllImport(LibraryName, EntryPoint = "LLVMContextDispose", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ContextDispose(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMGetDiagInfoDescription", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetDiagInfoDescription(LLVMDiagnosticInfoRef @DI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetDiagInfoSeverity", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMDiagnosticSeverity GetDiagInfoSeverity(LLVMDiagnosticInfoRef @DI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetMDKindIDInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMDKindIDInContext(LLVMContextRef @C, [MarshalAs(UnmanagedType.LPStr)] string @Name, int @SLen);

        [DllImport(LibraryName, EntryPoint = "LLVMGetMDKindID", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMDKindID([MarshalAs(UnmanagedType.LPStr)] string @Name, int @SLen);

        [DllImport(LibraryName, EntryPoint = "LLVMGetEnumAttributeKindForName", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetEnumAttributeKindForName([MarshalAs(UnmanagedType.LPStr)] string @Name, IntPtr @SLen);

        [DllImport(LibraryName, EntryPoint = "LLVMGetLastEnumAttributeKind", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetLastEnumAttributeKind();

        [DllImport(LibraryName, EntryPoint = "LLVMCreateEnumAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAttributeRef CreateEnumAttribute(LLVMContextRef @C, int @KindID, int @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMGetEnumAttributeKind", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetEnumAttributeKind(LLVMAttributeRef @A);

        [DllImport(LibraryName, EntryPoint = "LLVMGetEnumAttributeValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetEnumAttributeValue(LLVMAttributeRef @A);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateStringAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAttributeRef CreateStringAttribute(LLVMContextRef @C, [MarshalAs(UnmanagedType.LPStr)] string @K, int @KLength, [MarshalAs(UnmanagedType.LPStr)] string @V, int @VLength);

        [DllImport(LibraryName, EntryPoint = "LLVMGetStringAttributeKind", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetStringAttributeKind(LLVMAttributeRef @A, out int @Length);

        [DllImport(LibraryName, EntryPoint = "LLVMGetStringAttributeValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetStringAttributeValue(LLVMAttributeRef @A, out int @Length);

        [DllImport(LibraryName, EntryPoint = "LLVMIsEnumAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsEnumAttribute(LLVMAttributeRef @A);

        [DllImport(LibraryName, EntryPoint = "LLVMIsStringAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsStringAttribute(LLVMAttributeRef @A);

        [DllImport(LibraryName, EntryPoint = "LLVMModuleCreateWithName", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMModuleRef ModuleCreateWithName([MarshalAs(UnmanagedType.LPStr)] string @ModuleID);

        [DllImport(LibraryName, EntryPoint = "LLVMModuleCreateWithNameInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMModuleRef ModuleCreateWithNameInContext([MarshalAs(UnmanagedType.LPStr)] string @ModuleID, LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMCloneModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMModuleRef CloneModule(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeModule(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMGetModuleIdentifier", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetModuleIdentifier(LLVMModuleRef @M, out IntPtr @Len);

        [DllImport(LibraryName, EntryPoint = "LLVMSetModuleIdentifier", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetModuleIdentifier(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Ident, IntPtr @Len);

        [DllImport(LibraryName, EntryPoint = "LLVMGetDataLayoutStr", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetDataLayoutStr(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMGetDataLayout", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetDataLayout(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMSetDataLayout", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetDataLayout(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @DataLayoutStr);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTarget", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetTarget(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMSetTarget", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetTarget(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Triple);

        [DllImport(LibraryName, EntryPoint = "LLVMDumpModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DumpModule(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMPrintModuleToFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool PrintModuleToFile(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Filename, out IntPtr @ErrorMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMPrintModuleToString", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr PrintModuleToString(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMSetModuleInlineAsm", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetModuleInlineAsm(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Asm);

        [DllImport(LibraryName, EntryPoint = "LLVMGetModuleContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMContextRef GetModuleContext(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTypeByName", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef GetTypeByName(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNamedMetadataNumOperands", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetNamedMetadataNumOperands(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNamedMetadataOperands", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetNamedMetadataOperands(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Name, out LLVMValueRef @Dest);

        [DllImport(LibraryName, EntryPoint = "LLVMAddNamedMetadataOperand", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddNamedMetadataOperand(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Name, LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMAddFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef AddFunction(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Name, LLVMTypeRef @FunctionTy);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNamedFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetNamedFunction(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFirstFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetFirstFunction(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMGetLastFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetLastFunction(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNextFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetNextFunction(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetPreviousFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetPreviousFunction(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTypeKind", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeKind GetTypeKind(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMTypeIsSized", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool TypeIsSized(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTypeContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMContextRef GetTypeContext(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMDumpType", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DumpType(LLVMTypeRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMPrintTypeToString", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr PrintTypeToString(LLVMTypeRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMInt1TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int1TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMInt8TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int8TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMInt16TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int16TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMInt32TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int32TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMInt64TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int64TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMInt128TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int128TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMIntTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef IntTypeInContext(LLVMContextRef @C, int @NumBits);

        [DllImport(LibraryName, EntryPoint = "LLVMInt1Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int1Type();

        [DllImport(LibraryName, EntryPoint = "LLVMInt8Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int8Type();

        [DllImport(LibraryName, EntryPoint = "LLVMInt16Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int16Type();

        [DllImport(LibraryName, EntryPoint = "LLVMInt32Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int32Type();

        [DllImport(LibraryName, EntryPoint = "LLVMInt64Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int64Type();

        [DllImport(LibraryName, EntryPoint = "LLVMInt128Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef Int128Type();

        [DllImport(LibraryName, EntryPoint = "LLVMIntType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef IntType(int @NumBits);

        [DllImport(LibraryName, EntryPoint = "LLVMGetIntTypeWidth", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetIntTypeWidth(LLVMTypeRef @IntegerTy);

        [DllImport(LibraryName, EntryPoint = "LLVMHalfTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef HalfTypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMFloatTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef FloatTypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMDoubleTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef DoubleTypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMX86FP80TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef X86FP80TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMFP128TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef FP128TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMPPCFP128TypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef PPCFP128TypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMHalfType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef HalfType();

        [DllImport(LibraryName, EntryPoint = "LLVMFloatType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef FloatType();

        [DllImport(LibraryName, EntryPoint = "LLVMDoubleType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef DoubleType();

        [DllImport(LibraryName, EntryPoint = "LLVMX86FP80Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef X86FP80Type();

        [DllImport(LibraryName, EntryPoint = "LLVMFP128Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef FP128Type();

        [DllImport(LibraryName, EntryPoint = "LLVMPPCFP128Type", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef PPCFP128Type();

        [DllImport(LibraryName, EntryPoint = "LLVMFunctionType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef FunctionType(LLVMTypeRef @ReturnType, out LLVMTypeRef @ParamTypes, int @ParamCount, LLVMBool @IsVarArg);

        [DllImport(LibraryName, EntryPoint = "LLVMIsFunctionVarArg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsFunctionVarArg(LLVMTypeRef @FunctionTy);

        [DllImport(LibraryName, EntryPoint = "LLVMGetReturnType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef GetReturnType(LLVMTypeRef @FunctionTy);

        [DllImport(LibraryName, EntryPoint = "LLVMCountParamTypes", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CountParamTypes(LLVMTypeRef @FunctionTy);

        [DllImport(LibraryName, EntryPoint = "LLVMGetParamTypes", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetParamTypes(LLVMTypeRef @FunctionTy, out LLVMTypeRef @Dest);

        [DllImport(LibraryName, EntryPoint = "LLVMStructTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef StructTypeInContext(LLVMContextRef @C, out LLVMTypeRef @ElementTypes, int @ElementCount, LLVMBool @Packed);

        [DllImport(LibraryName, EntryPoint = "LLVMStructType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef StructType(out LLVMTypeRef @ElementTypes, int @ElementCount, LLVMBool @Packed);

        [DllImport(LibraryName, EntryPoint = "LLVMStructCreateNamed", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef StructCreateNamed(LLVMContextRef @C, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMGetStructName", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetStructName(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMStructSetBody", CallingConvention = CallingConvention.Cdecl)]
        public static extern void StructSetBody(LLVMTypeRef @StructTy, out LLVMTypeRef @ElementTypes, int @ElementCount, LLVMBool @Packed);

        [DllImport(LibraryName, EntryPoint = "LLVMCountStructElementTypes", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CountStructElementTypes(LLVMTypeRef @StructTy);

        [DllImport(LibraryName, EntryPoint = "LLVMGetStructElementTypes", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetStructElementTypes(LLVMTypeRef @StructTy, out LLVMTypeRef @Dest);

        [DllImport(LibraryName, EntryPoint = "LLVMStructGetTypeAtIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef StructGetTypeAtIndex(LLVMTypeRef @StructTy, int @i);

        [DllImport(LibraryName, EntryPoint = "LLVMIsPackedStruct", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsPackedStruct(LLVMTypeRef @StructTy);

        [DllImport(LibraryName, EntryPoint = "LLVMIsOpaqueStruct", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsOpaqueStruct(LLVMTypeRef @StructTy);

        [DllImport(LibraryName, EntryPoint = "LLVMGetElementType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef GetElementType(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMArrayType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef ArrayType(LLVMTypeRef @ElementType, int @ElementCount);

        [DllImport(LibraryName, EntryPoint = "LLVMGetArrayLength", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetArrayLength(LLVMTypeRef @ArrayTy);

        [DllImport(LibraryName, EntryPoint = "LLVMPointerType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef PointerType(LLVMTypeRef @ElementType, int @AddressSpace);

        [DllImport(LibraryName, EntryPoint = "LLVMGetPointerAddressSpace", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetPointerAddressSpace(LLVMTypeRef @PointerTy);

        [DllImport(LibraryName, EntryPoint = "LLVMVectorType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef VectorType(LLVMTypeRef @ElementType, int @ElementCount);

        [DllImport(LibraryName, EntryPoint = "LLVMGetVectorSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetVectorSize(LLVMTypeRef @VectorTy);

        [DllImport(LibraryName, EntryPoint = "LLVMVoidTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef VoidTypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMLabelTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef LabelTypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMX86MMXTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef X86MMXTypeInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMVoidType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef VoidType();

        [DllImport(LibraryName, EntryPoint = "LLVMLabelType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef LabelType();

        [DllImport(LibraryName, EntryPoint = "LLVMX86MMXType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef X86MMXType();

        [DllImport(LibraryName, EntryPoint = "LLVMTypeOf", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef TypeOf(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMGetValueKind", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueKind GetValueKind(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMGetValueName", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetValueName(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMSetValueName", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetValueName(LLVMValueRef @Val, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMDumpValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DumpValue(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMPrintValueToString", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr PrintValueToString(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMReplaceAllUsesWith", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReplaceAllUsesWith(LLVMValueRef @OldVal, LLVMValueRef @NewVal);

        [DllImport(LibraryName, EntryPoint = "LLVMIsConstant", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsConstant(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsUndef", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsUndef(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAArgument", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAArgument(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsABasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsABasicBlock(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAInlineAsm", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAInlineAsm(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAUser", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAUser(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstant", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstant(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsABlockAddress", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsABlockAddress(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantAggregateZero", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantAggregateZero(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantArray", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantArray(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantDataSequential", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantDataSequential(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantDataArray", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantDataArray(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantDataVector", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantDataVector(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantExpr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantExpr(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantFP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantFP(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantInt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantInt(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantPointerNull", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantPointerNull(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantStruct", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantStruct(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantTokenNone", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantTokenNone(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAConstantVector", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAConstantVector(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAGlobalValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAGlobalValue(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAGlobalAlias", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAGlobalAlias(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAGlobalObject", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAGlobalObject(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAFunction(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAGlobalVariable", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAGlobalVariable(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAUndefValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAUndefValue(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAInstruction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAInstruction(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsABinaryOperator", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsABinaryOperator(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsACallInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsACallInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAIntrinsicInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAIntrinsicInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsADbgInfoIntrinsic", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsADbgInfoIntrinsic(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsADbgDeclareInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsADbgDeclareInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAMemIntrinsic", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAMemIntrinsic(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAMemCpyInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAMemCpyInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAMemMoveInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAMemMoveInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAMemSetInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAMemSetInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsACmpInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsACmpInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAFCmpInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAFCmpInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAICmpInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAICmpInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAExtractElementInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAExtractElementInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAGetElementPtrInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAGetElementPtrInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAInsertElementInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAInsertElementInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAInsertValueInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAInsertValueInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsALandingPadInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsALandingPadInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAPHINode", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAPHINode(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsASelectInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsASelectInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAShuffleVectorInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAShuffleVectorInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAStoreInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAStoreInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsATerminatorInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsATerminatorInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsABranchInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsABranchInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAIndirectBrInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAIndirectBrInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAInvokeInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAInvokeInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAReturnInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAReturnInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsASwitchInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsASwitchInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAUnreachableInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAUnreachableInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAResumeInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAResumeInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsACleanupReturnInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsACleanupReturnInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsACatchReturnInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsACatchReturnInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAFuncletPadInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAFuncletPadInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsACatchPadInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsACatchPadInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsACleanupPadInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsACleanupPadInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAUnaryInstruction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAUnaryInstruction(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAAllocaInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAAllocaInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsACastInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsACastInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAAddrSpaceCastInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAAddrSpaceCastInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsABitCastInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsABitCastInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAFPExtInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAFPExtInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAFPToSIInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAFPToSIInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAFPToUIInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAFPToUIInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAFPTruncInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAFPTruncInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAIntToPtrInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAIntToPtrInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAPtrToIntInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAPtrToIntInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsASExtInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsASExtInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsASIToFPInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsASIToFPInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsATruncInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsATruncInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAUIToFPInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAUIToFPInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAZExtInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAZExtInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAExtractValueInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAExtractValueInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsALoadInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsALoadInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAVAArgInst", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAVAArgInst(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAMDNode", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAMDNode(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAMDString", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef IsAMDString(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFirstUse", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMUseRef GetFirstUse(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNextUse", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMUseRef GetNextUse(LLVMUseRef @U);

        [DllImport(LibraryName, EntryPoint = "LLVMGetUser", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetUser(LLVMUseRef @U);

        [DllImport(LibraryName, EntryPoint = "LLVMGetUsedValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetUsedValue(LLVMUseRef @U);

        [DllImport(LibraryName, EntryPoint = "LLVMGetOperand", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetOperand(LLVMValueRef @Val, int @Index);

        [DllImport(LibraryName, EntryPoint = "LLVMGetOperandUse", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMUseRef GetOperandUse(LLVMValueRef @Val, int @Index);

        [DllImport(LibraryName, EntryPoint = "LLVMSetOperand", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetOperand(LLVMValueRef @User, int @Index, LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNumOperands", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetNumOperands(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNull", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNull(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMConstAllOnes", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstAllOnes(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMGetUndef", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetUndef(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMIsNull", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsNull(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMConstPointerNull", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstPointerNull(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMConstInt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstInt(LLVMTypeRef @IntTy, long @N, LLVMBool @SignExtend);

        [DllImport(LibraryName, EntryPoint = "LLVMConstIntOfArbitraryPrecision", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstIntOfArbitraryPrecision(LLVMTypeRef @IntTy, int @NumWords, int[] @Words);

        [DllImport(LibraryName, EntryPoint = "LLVMConstIntOfString", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstIntOfString(LLVMTypeRef @IntTy, [MarshalAs(UnmanagedType.LPStr)] string @Text, int @Radix);

        [DllImport(LibraryName, EntryPoint = "LLVMConstIntOfStringAndSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstIntOfStringAndSize(LLVMTypeRef @IntTy, [MarshalAs(UnmanagedType.LPStr)] string @Text, int @SLen, int @Radix);

        [DllImport(LibraryName, EntryPoint = "LLVMConstReal", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstReal(LLVMTypeRef @RealTy, double @N);

        [DllImport(LibraryName, EntryPoint = "LLVMConstRealOfString", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstRealOfString(LLVMTypeRef @RealTy, [MarshalAs(UnmanagedType.LPStr)] string @Text);

        [DllImport(LibraryName, EntryPoint = "LLVMConstRealOfStringAndSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstRealOfStringAndSize(LLVMTypeRef @RealTy, [MarshalAs(UnmanagedType.LPStr)] string @Text, int @SLen);

        [DllImport(LibraryName, EntryPoint = "LLVMConstIntGetZExtValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern long ConstIntGetZExtValue(LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMConstIntGetSExtValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern long ConstIntGetSExtValue(LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMConstRealGetDouble", CallingConvention = CallingConvention.Cdecl)]
        public static extern double ConstRealGetDouble(LLVMValueRef @ConstantVal, out LLVMBool @losesInfo);

        [DllImport(LibraryName, EntryPoint = "LLVMConstStringInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstStringInContext(LLVMContextRef @C, [MarshalAs(UnmanagedType.LPStr)] string @Str, int @Length, LLVMBool @DontNullTerminate);

        [DllImport(LibraryName, EntryPoint = "LLVMConstString", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstString([MarshalAs(UnmanagedType.LPStr)] string @Str, int @Length, LLVMBool @DontNullTerminate);

        [DllImport(LibraryName, EntryPoint = "LLVMIsConstantString", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsConstantString(LLVMValueRef @c);

        [DllImport(LibraryName, EntryPoint = "LLVMGetAsString", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetAsString(LLVMValueRef @c, out IntPtr @Length);

        [DllImport(LibraryName, EntryPoint = "LLVMConstStructInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstStructInContext(LLVMContextRef @C, out LLVMValueRef @ConstantVals, int @Count, LLVMBool @Packed);

        [DllImport(LibraryName, EntryPoint = "LLVMConstStruct", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstStruct(out LLVMValueRef @ConstantVals, int @Count, LLVMBool @Packed);

        [DllImport(LibraryName, EntryPoint = "LLVMConstArray", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstArray(LLVMTypeRef @ElementTy, out LLVMValueRef @ConstantVals, int @Length);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNamedStruct", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNamedStruct(LLVMTypeRef @StructTy, out LLVMValueRef @ConstantVals, int @Count);

        [DllImport(LibraryName, EntryPoint = "LLVMGetElementAsConstant", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetElementAsConstant(LLVMValueRef @C, int @idx);

        [DllImport(LibraryName, EntryPoint = "LLVMConstVector", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstVector(out LLVMValueRef @ScalarConstantVals, int @Size);

        [DllImport(LibraryName, EntryPoint = "LLVMGetConstOpcode", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOpcode GetConstOpcode(LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMAlignOf", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef AlignOf(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMSizeOf", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef SizeOf(LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNeg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNeg(LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNSWNeg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNSWNeg(LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNUWNeg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNUWNeg(LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFNeg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFNeg(LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNot", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNot(LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMConstAdd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstAdd(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNSWAdd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNSWAdd(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNUWAdd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNUWAdd(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFAdd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFAdd(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstSub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstSub(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNSWSub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNSWSub(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNUWSub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNUWSub(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFSub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFSub(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstMul", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstMul(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNSWMul", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNSWMul(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstNUWMul", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstNUWMul(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFMul", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFMul(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstUDiv", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstUDiv(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstSDiv", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstSDiv(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstExactSDiv", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstExactSDiv(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFDiv", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFDiv(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstURem", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstURem(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstSRem", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstSRem(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFRem", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFRem(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstAnd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstAnd(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstOr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstOr(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstXor", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstXor(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstICmp", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstICmp(LLVMIntPredicate @Predicate, LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFCmp", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFCmp(LLVMRealPredicate @Predicate, LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstShl", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstShl(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstLShr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstLShr(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstAShr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstAShr(LLVMValueRef @LHSConstant, LLVMValueRef @RHSConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstGEP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstGEP(LLVMValueRef @ConstantVal, out LLVMValueRef @ConstantIndices, int @NumIndices);

        [DllImport(LibraryName, EntryPoint = "LLVMConstInBoundsGEP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstInBoundsGEP(LLVMValueRef @ConstantVal, out LLVMValueRef @ConstantIndices, int @NumIndices);

        [DllImport(LibraryName, EntryPoint = "LLVMConstTrunc", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstTrunc(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstSExt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstSExt(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstZExt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstZExt(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFPTrunc", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFPTrunc(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFPExt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFPExt(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstUIToFP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstUIToFP(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstSIToFP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstSIToFP(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFPToUI", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFPToUI(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFPToSI", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFPToSI(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstPtrToInt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstPtrToInt(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstIntToPtr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstIntToPtr(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstBitCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstBitCast(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstAddrSpaceCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstAddrSpaceCast(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstZExtOrBitCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstZExtOrBitCast(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstSExtOrBitCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstSExtOrBitCast(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstTruncOrBitCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstTruncOrBitCast(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstPointerCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstPointerCast(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstIntCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstIntCast(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType, LLVMBool @isSigned);

        [DllImport(LibraryName, EntryPoint = "LLVMConstFPCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstFPCast(LLVMValueRef @ConstantVal, LLVMTypeRef @ToType);

        [DllImport(LibraryName, EntryPoint = "LLVMConstSelect", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstSelect(LLVMValueRef @ConstantCondition, LLVMValueRef @ConstantIfTrue, LLVMValueRef @ConstantIfFalse);

        [DllImport(LibraryName, EntryPoint = "LLVMConstExtractElement", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstExtractElement(LLVMValueRef @VectorConstant, LLVMValueRef @IndexConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstInsertElement", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstInsertElement(LLVMValueRef @VectorConstant, LLVMValueRef @ElementValueConstant, LLVMValueRef @IndexConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstShuffleVector", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstShuffleVector(LLVMValueRef @VectorAConstant, LLVMValueRef @VectorBConstant, LLVMValueRef @MaskConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMConstExtractValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstExtractValue(LLVMValueRef @AggConstant, out int @IdxList, int @NumIdx);

        [DllImport(LibraryName, EntryPoint = "LLVMConstInsertValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstInsertValue(LLVMValueRef @AggConstant, LLVMValueRef @ElementValueConstant, out int @IdxList, int @NumIdx);

        [DllImport(LibraryName, EntryPoint = "LLVMConstInlineAsm", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef ConstInlineAsm(LLVMTypeRef @Ty, [MarshalAs(UnmanagedType.LPStr)] string @AsmString, [MarshalAs(UnmanagedType.LPStr)] string @Constraints, LLVMBool @HasSideEffects, LLVMBool @IsAlignStack);

        [DllImport(LibraryName, EntryPoint = "LLVMBlockAddress", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BlockAddress(LLVMValueRef @F, LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMGetGlobalParent", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMModuleRef GetGlobalParent(LLVMValueRef @Global);

        [DllImport(LibraryName, EntryPoint = "LLVMIsDeclaration", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsDeclaration(LLVMValueRef @Global);

        [DllImport(LibraryName, EntryPoint = "LLVMGetLinkage", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMLinkage GetLinkage(LLVMValueRef @Global);

        [DllImport(LibraryName, EntryPoint = "LLVMSetLinkage", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetLinkage(LLVMValueRef @Global, LLVMLinkage @Linkage);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSection", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetSection(LLVMValueRef @Global);

        [DllImport(LibraryName, EntryPoint = "LLVMSetSection", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetSection(LLVMValueRef @Global, [MarshalAs(UnmanagedType.LPStr)] string @Section);

        [DllImport(LibraryName, EntryPoint = "LLVMGetVisibility", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMVisibility GetVisibility(LLVMValueRef @Global);

        [DllImport(LibraryName, EntryPoint = "LLVMSetVisibility", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetVisibility(LLVMValueRef @Global, LLVMVisibility @Viz);

        [DllImport(LibraryName, EntryPoint = "LLVMGetDLLStorageClass", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMDLLStorageClass GetDLLStorageClass(LLVMValueRef @Global);

        [DllImport(LibraryName, EntryPoint = "LLVMSetDLLStorageClass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetDLLStorageClass(LLVMValueRef @Global, LLVMDLLStorageClass @Class);

        [DllImport(LibraryName, EntryPoint = "LLVMHasUnnamedAddr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool HasUnnamedAddr(LLVMValueRef @Global);

        [DllImport(LibraryName, EntryPoint = "LLVMSetUnnamedAddr", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetUnnamedAddr(LLVMValueRef @Global, LLVMBool @HasUnnamedAddr);

        [DllImport(LibraryName, EntryPoint = "LLVMGetAlignment", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetAlignment(LLVMValueRef @V);

        [DllImport(LibraryName, EntryPoint = "LLVMSetAlignment", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetAlignment(LLVMValueRef @V, int @Bytes);

        [DllImport(LibraryName, EntryPoint = "LLVMAddGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef AddGlobal(LLVMModuleRef @M, LLVMTypeRef @Ty, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMAddGlobalInAddressSpace", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef AddGlobalInAddressSpace(LLVMModuleRef @M, LLVMTypeRef @Ty, [MarshalAs(UnmanagedType.LPStr)] string @Name, int @AddressSpace);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNamedGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetNamedGlobal(LLVMModuleRef @M, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFirstGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetFirstGlobal(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMGetLastGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetLastGlobal(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNextGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetNextGlobal(LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMGetPreviousGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetPreviousGlobal(LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMDeleteGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DeleteGlobal(LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMGetInitializer", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetInitializer(LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMSetInitializer", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetInitializer(LLVMValueRef @GlobalVar, LLVMValueRef @ConstantVal);

        [DllImport(LibraryName, EntryPoint = "LLVMIsThreadLocal", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsThreadLocal(LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMSetThreadLocal", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetThreadLocal(LLVMValueRef @GlobalVar, LLVMBool @IsThreadLocal);

        [DllImport(LibraryName, EntryPoint = "LLVMIsGlobalConstant", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsGlobalConstant(LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMSetGlobalConstant", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetGlobalConstant(LLVMValueRef @GlobalVar, LLVMBool @IsConstant);

        [DllImport(LibraryName, EntryPoint = "LLVMGetThreadLocalMode", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMThreadLocalMode GetThreadLocalMode(LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMSetThreadLocalMode", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetThreadLocalMode(LLVMValueRef @GlobalVar, LLVMThreadLocalMode @Mode);

        [DllImport(LibraryName, EntryPoint = "LLVMIsExternallyInitialized", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsExternallyInitialized(LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMSetExternallyInitialized", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetExternallyInitialized(LLVMValueRef @GlobalVar, LLVMBool @IsExtInit);

        [DllImport(LibraryName, EntryPoint = "LLVMAddAlias", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef AddAlias(LLVMModuleRef @M, LLVMTypeRef @Ty, LLVMValueRef @Aliasee, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMDeleteFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DeleteFunction(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMHasPersonalityFn", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool HasPersonalityFn(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetPersonalityFn", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetPersonalityFn(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMSetPersonalityFn", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetPersonalityFn(LLVMValueRef @Fn, LLVMValueRef @PersonalityFn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetIntrinsicID", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetIntrinsicID(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFunctionCallConv", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetFunctionCallConv(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMSetFunctionCallConv", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetFunctionCallConv(LLVMValueRef @Fn, int @CC);

        [DllImport(LibraryName, EntryPoint = "LLVMGetGC", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetGC(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMSetGC", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetGC(LLVMValueRef @Fn, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMAddFunctionAttr", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddFunctionAttr(LLVMValueRef @Fn, LLVMAttribute @PA);

        [DllImport(LibraryName, EntryPoint = "LLVMAddAttributeAtIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddAttributeAtIndex(LLVMValueRef @F, LLVMAttributeIndex @Idx, LLVMAttributeRef @A);

        [DllImport(LibraryName, EntryPoint = "LLVMGetAttributeCountAtIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetAttributeCountAtIndex(LLVMValueRef @F, LLVMAttributeIndex @Idx);

        [DllImport(LibraryName, EntryPoint = "LLVMGetAttributesAtIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetAttributesAtIndex(LLVMValueRef @F, LLVMAttributeIndex @Idx, out LLVMAttributeRef @Attrs);

        [DllImport(LibraryName, EntryPoint = "LLVMGetEnumAttributeAtIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAttributeRef GetEnumAttributeAtIndex(LLVMValueRef @F, LLVMAttributeIndex @Idx, int @KindID);

        [DllImport(LibraryName, EntryPoint = "LLVMGetStringAttributeAtIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAttributeRef GetStringAttributeAtIndex(LLVMValueRef @F, LLVMAttributeIndex @Idx, [MarshalAs(UnmanagedType.LPStr)] string @K, int @KLen);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveEnumAttributeAtIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveEnumAttributeAtIndex(LLVMValueRef @F, LLVMAttributeIndex @Idx, int @KindID);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveStringAttributeAtIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveStringAttributeAtIndex(LLVMValueRef @F, LLVMAttributeIndex @Idx, [MarshalAs(UnmanagedType.LPStr)] string @K, int @KLen);

        [DllImport(LibraryName, EntryPoint = "LLVMAddTargetDependentFunctionAttr", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddTargetDependentFunctionAttr(LLVMValueRef @Fn, [MarshalAs(UnmanagedType.LPStr)] string @A, [MarshalAs(UnmanagedType.LPStr)] string @V);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFunctionAttr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAttribute GetFunctionAttr(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveFunctionAttr", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveFunctionAttr(LLVMValueRef @Fn, LLVMAttribute @PA);

        [DllImport(LibraryName, EntryPoint = "LLVMCountParams", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CountParams(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetParams", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetParams(LLVMValueRef @Fn, out LLVMValueRef @Params);

        [DllImport(LibraryName, EntryPoint = "LLVMGetParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetParam(LLVMValueRef @Fn, int @Index);

        [DllImport(LibraryName, EntryPoint = "LLVMGetParamParent", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetParamParent(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFirstParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetFirstParam(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetLastParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetLastParam(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNextParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetNextParam(LLVMValueRef @Arg);

        [DllImport(LibraryName, EntryPoint = "LLVMGetPreviousParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetPreviousParam(LLVMValueRef @Arg);

        [DllImport(LibraryName, EntryPoint = "LLVMAddAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddAttribute(LLVMValueRef @Arg, LLVMAttribute @PA);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveAttribute(LLVMValueRef @Arg, LLVMAttribute @PA);

        [DllImport(LibraryName, EntryPoint = "LLVMGetAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAttribute GetAttribute(LLVMValueRef @Arg);

        [DllImport(LibraryName, EntryPoint = "LLVMSetParamAlignment", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetParamAlignment(LLVMValueRef @Arg, int @Align);

        [DllImport(LibraryName, EntryPoint = "LLVMMDStringInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef MDStringInContext(LLVMContextRef @C, [MarshalAs(UnmanagedType.LPStr)] string @Str, int @SLen);

        [DllImport(LibraryName, EntryPoint = "LLVMMDString", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef MDString([MarshalAs(UnmanagedType.LPStr)] string @Str, int @SLen);

        [DllImport(LibraryName, EntryPoint = "LLVMMDNodeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef MDNodeInContext(LLVMContextRef @C, out LLVMValueRef @Vals, int @Count);

        [DllImport(LibraryName, EntryPoint = "LLVMMDNode", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef MDNode(out LLVMValueRef @Vals, int @Count);

        [DllImport(LibraryName, EntryPoint = "LLVMGetMDString", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetMDString(LLVMValueRef @V, out int @Length);

        [DllImport(LibraryName, EntryPoint = "LLVMGetMDNodeNumOperands", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMDNodeNumOperands(LLVMValueRef @V);

        [DllImport(LibraryName, EntryPoint = "LLVMGetMDNodeOperands", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetMDNodeOperands(LLVMValueRef @V, out LLVMValueRef @Dest);

        [DllImport(LibraryName, EntryPoint = "LLVMBasicBlockAsValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BasicBlockAsValue(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMValueIsBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool ValueIsBasicBlock(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMValueAsBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef ValueAsBasicBlock(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBasicBlockName", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetBasicBlockName(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBasicBlockParent", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetBasicBlockParent(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBasicBlockTerminator", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetBasicBlockTerminator(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMCountBasicBlocks", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CountBasicBlocks(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBasicBlocks", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetBasicBlocks(LLVMValueRef @Fn, out LLVMBasicBlockRef @BasicBlocks);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFirstBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetFirstBasicBlock(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetLastBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetLastBasicBlock(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNextBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetNextBasicBlock(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMGetPreviousBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetPreviousBasicBlock(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMGetEntryBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetEntryBasicBlock(LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMAppendBasicBlockInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef AppendBasicBlockInContext(LLVMContextRef @C, LLVMValueRef @Fn, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMAppendBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef AppendBasicBlock(LLVMValueRef @Fn, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMInsertBasicBlockInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef InsertBasicBlockInContext(LLVMContextRef @C, LLVMBasicBlockRef @BB, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMInsertBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef InsertBasicBlock(LLVMBasicBlockRef @InsertBeforeBB, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMDeleteBasicBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DeleteBasicBlock(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveBasicBlockFromParent", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveBasicBlockFromParent(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMMoveBasicBlockBefore", CallingConvention = CallingConvention.Cdecl)]
        public static extern void MoveBasicBlockBefore(LLVMBasicBlockRef @BB, LLVMBasicBlockRef @MovePos);

        [DllImport(LibraryName, EntryPoint = "LLVMMoveBasicBlockAfter", CallingConvention = CallingConvention.Cdecl)]
        public static extern void MoveBasicBlockAfter(LLVMBasicBlockRef @BB, LLVMBasicBlockRef @MovePos);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFirstInstruction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetFirstInstruction(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMGetLastInstruction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetLastInstruction(LLVMBasicBlockRef @BB);

        [DllImport(LibraryName, EntryPoint = "LLVMHasMetadata", CallingConvention = CallingConvention.Cdecl)]
        public static extern int HasMetadata(LLVMValueRef @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMGetMetadata", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetMetadata(LLVMValueRef @Val, int @KindID);

        [DllImport(LibraryName, EntryPoint = "LLVMSetMetadata", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetMetadata(LLVMValueRef @Val, int @KindID, LLVMValueRef @Node);

        [DllImport(LibraryName, EntryPoint = "LLVMGetInstructionParent", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetInstructionParent(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNextInstruction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetNextInstruction(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetPreviousInstruction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetPreviousInstruction(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMInstructionRemoveFromParent", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InstructionRemoveFromParent(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMInstructionEraseFromParent", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InstructionEraseFromParent(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetInstructionOpcode", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOpcode GetInstructionOpcode(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetICmpPredicate", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMIntPredicate GetICmpPredicate(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFCmpPredicate", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMRealPredicate GetFCmpPredicate(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMInstructionClone", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef InstructionClone(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNumArgOperands", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetNumArgOperands(LLVMValueRef @Instr);

        [DllImport(LibraryName, EntryPoint = "LLVMSetInstructionCallConv", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetInstructionCallConv(LLVMValueRef @Instr, int @CC);

        [DllImport(LibraryName, EntryPoint = "LLVMGetInstructionCallConv", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetInstructionCallConv(LLVMValueRef @Instr);

        [DllImport(LibraryName, EntryPoint = "LLVMAddInstrAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddInstrAttribute(LLVMValueRef @Instr, int @index, LLVMAttribute @param2);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveInstrAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveInstrAttribute(LLVMValueRef @Instr, int @index, LLVMAttribute @param2);

        [DllImport(LibraryName, EntryPoint = "LLVMSetInstrParamAlignment", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetInstrParamAlignment(LLVMValueRef @Instr, int @index, int @Align);

        [DllImport(LibraryName, EntryPoint = "LLVMAddCallSiteAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddCallSiteAttribute(LLVMValueRef @C, LLVMAttributeIndex @Idx, LLVMAttributeRef @A);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCallSiteAttributeCount", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetCallSiteAttributeCount(LLVMValueRef @C, LLVMAttributeIndex @Idx);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCallSiteAttributes", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetCallSiteAttributes(LLVMValueRef @C, LLVMAttributeIndex @Idx, out LLVMAttributeRef @Attrs);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCallSiteEnumAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAttributeRef GetCallSiteEnumAttribute(LLVMValueRef @C, LLVMAttributeIndex @Idx, int @KindID);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCallSiteStringAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAttributeRef GetCallSiteStringAttribute(LLVMValueRef @C, LLVMAttributeIndex @Idx, [MarshalAs(UnmanagedType.LPStr)] string @K, int @KLen);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveCallSiteEnumAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveCallSiteEnumAttribute(LLVMValueRef @C, LLVMAttributeIndex @Idx, int @KindID);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveCallSiteStringAttribute", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveCallSiteStringAttribute(LLVMValueRef @C, LLVMAttributeIndex @Idx, [MarshalAs(UnmanagedType.LPStr)] string @K, int @KLen);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCalledValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetCalledValue(LLVMValueRef @Instr);

        [DllImport(LibraryName, EntryPoint = "LLVMIsTailCall", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsTailCall(LLVMValueRef @CallInst);

        [DllImport(LibraryName, EntryPoint = "LLVMSetTailCall", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetTailCall(LLVMValueRef @CallInst, LLVMBool @IsTailCall);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNormalDest", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetNormalDest(LLVMValueRef @InvokeInst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetUnwindDest", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetUnwindDest(LLVMValueRef @InvokeInst);

        [DllImport(LibraryName, EntryPoint = "LLVMSetNormalDest", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetNormalDest(LLVMValueRef @InvokeInst, LLVMBasicBlockRef @B);

        [DllImport(LibraryName, EntryPoint = "LLVMSetUnwindDest", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetUnwindDest(LLVMValueRef @InvokeInst, LLVMBasicBlockRef @B);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNumSuccessors", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetNumSuccessors(LLVMValueRef @Term);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSuccessor", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetSuccessor(LLVMValueRef @Term, int @i);

        [DllImport(LibraryName, EntryPoint = "LLVMSetSuccessor", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetSuccessor(LLVMValueRef @Term, int @i, LLVMBasicBlockRef @block);

        [DllImport(LibraryName, EntryPoint = "LLVMIsConditional", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsConditional(LLVMValueRef @Branch);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCondition", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetCondition(LLVMValueRef @Branch);

        [DllImport(LibraryName, EntryPoint = "LLVMSetCondition", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetCondition(LLVMValueRef @Branch, LLVMValueRef @Cond);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSwitchDefaultDest", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetSwitchDefaultDest(LLVMValueRef @SwitchInstr);

        [DllImport(LibraryName, EntryPoint = "LLVMGetAllocatedType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef GetAllocatedType(LLVMValueRef @Alloca);

        [DllImport(LibraryName, EntryPoint = "LLVMIsInBounds", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsInBounds(LLVMValueRef @GEP);

        [DllImport(LibraryName, EntryPoint = "LLVMSetIsInBounds", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetIsInBounds(LLVMValueRef @GEP, LLVMBool @InBounds);

        [DllImport(LibraryName, EntryPoint = "LLVMAddIncoming", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddIncoming(LLVMValueRef @PhiNode, out LLVMValueRef @IncomingValues, out LLVMBasicBlockRef @IncomingBlocks, int @Count);

        [DllImport(LibraryName, EntryPoint = "LLVMCountIncoming", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CountIncoming(LLVMValueRef @PhiNode);

        [DllImport(LibraryName, EntryPoint = "LLVMGetIncomingValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetIncomingValue(LLVMValueRef @PhiNode, int @Index);

        [DllImport(LibraryName, EntryPoint = "LLVMGetIncomingBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetIncomingBlock(LLVMValueRef @PhiNode, int @Index);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNumIndices", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetNumIndices(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMGetIndices", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetIndices(LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateBuilderInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBuilderRef CreateBuilderInContext(LLVMContextRef @C);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateBuilder", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBuilderRef CreateBuilder();

        [DllImport(LibraryName, EntryPoint = "LLVMPositionBuilder", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PositionBuilder(LLVMBuilderRef @Builder, LLVMBasicBlockRef @Block, LLVMValueRef @Instr);

        [DllImport(LibraryName, EntryPoint = "LLVMPositionBuilderBefore", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PositionBuilderBefore(LLVMBuilderRef @Builder, LLVMValueRef @Instr);

        [DllImport(LibraryName, EntryPoint = "LLVMPositionBuilderAtEnd", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PositionBuilderAtEnd(LLVMBuilderRef @Builder, LLVMBasicBlockRef @Block);

        [DllImport(LibraryName, EntryPoint = "LLVMGetInsertBlock", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBasicBlockRef GetInsertBlock(LLVMBuilderRef @Builder);

        [DllImport(LibraryName, EntryPoint = "LLVMClearInsertionPosition", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ClearInsertionPosition(LLVMBuilderRef @Builder);

        [DllImport(LibraryName, EntryPoint = "LLVMInsertIntoBuilder", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InsertIntoBuilder(LLVMBuilderRef @Builder, LLVMValueRef @Instr);

        [DllImport(LibraryName, EntryPoint = "LLVMInsertIntoBuilderWithName", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InsertIntoBuilderWithName(LLVMBuilderRef @Builder, LLVMValueRef @Instr, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeBuilder", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeBuilder(LLVMBuilderRef @Builder);

        [DllImport(LibraryName, EntryPoint = "LLVMSetCurrentDebugLocation", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetCurrentDebugLocation(LLVMBuilderRef @Builder, LLVMValueRef @L);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCurrentDebugLocation", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetCurrentDebugLocation(LLVMBuilderRef @Builder);

        [DllImport(LibraryName, EntryPoint = "LLVMSetInstDebugLocation", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetInstDebugLocation(LLVMBuilderRef @Builder, LLVMValueRef @Inst);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildRetVoid", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildRetVoid(LLVMBuilderRef @param0);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildRet", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildRet(LLVMBuilderRef @param0, LLVMValueRef @V);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildAggregateRet", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildAggregateRet(LLVMBuilderRef @param0, out LLVMValueRef @RetVals, int @N);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildBr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildBr(LLVMBuilderRef @param0, LLVMBasicBlockRef @Dest);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildCondBr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildCondBr(LLVMBuilderRef @param0, LLVMValueRef @If, LLVMBasicBlockRef @Then, LLVMBasicBlockRef @Else);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildSwitch", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildSwitch(LLVMBuilderRef @param0, LLVMValueRef @V, LLVMBasicBlockRef @Else, int @NumCases);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildIndirectBr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildIndirectBr(LLVMBuilderRef @B, LLVMValueRef @Addr, int @NumDests);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildInvoke", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildInvoke(LLVMBuilderRef @param0, LLVMValueRef @Fn, out LLVMValueRef @Args, int @NumArgs, LLVMBasicBlockRef @Then, LLVMBasicBlockRef @Catch, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildLandingPad", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildLandingPad(LLVMBuilderRef @B, LLVMTypeRef @Ty, LLVMValueRef @PersFn, int @NumClauses, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildResume", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildResume(LLVMBuilderRef @B, LLVMValueRef @Exn);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildUnreachable", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildUnreachable(LLVMBuilderRef @param0);

        [DllImport(LibraryName, EntryPoint = "LLVMAddCase", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddCase(LLVMValueRef @Switch, LLVMValueRef @OnVal, LLVMBasicBlockRef @Dest);

        [DllImport(LibraryName, EntryPoint = "LLVMAddDestination", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddDestination(LLVMValueRef @IndirectBr, LLVMBasicBlockRef @Dest);

        [DllImport(LibraryName, EntryPoint = "LLVMGetNumClauses", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetNumClauses(LLVMValueRef @LandingPad);

        [DllImport(LibraryName, EntryPoint = "LLVMGetClause", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef GetClause(LLVMValueRef @LandingPad, int @Idx);

        [DllImport(LibraryName, EntryPoint = "LLVMAddClause", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddClause(LLVMValueRef @LandingPad, LLVMValueRef @ClauseVal);

        [DllImport(LibraryName, EntryPoint = "LLVMIsCleanup", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsCleanup(LLVMValueRef @LandingPad);

        [DllImport(LibraryName, EntryPoint = "LLVMSetCleanup", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetCleanup(LLVMValueRef @LandingPad, LLVMBool @Val);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildAdd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildAdd(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNSWAdd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNSWAdd(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNUWAdd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNUWAdd(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFAdd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFAdd(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildSub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildSub(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNSWSub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNSWSub(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNUWSub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNUWSub(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFSub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFSub(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildMul", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildMul(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNSWMul", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNSWMul(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNUWMul", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNUWMul(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFMul", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFMul(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildUDiv", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildUDiv(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildSDiv", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildSDiv(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildExactSDiv", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildExactSDiv(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFDiv", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFDiv(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildURem", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildURem(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildSRem", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildSRem(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFRem", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFRem(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildShl", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildShl(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildLShr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildLShr(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildAShr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildAShr(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildAnd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildAnd(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildOr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildOr(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildXor", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildXor(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildBinOp", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildBinOp(LLVMBuilderRef @B, LLVMOpcode @Op, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNeg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNeg(LLVMBuilderRef @param0, LLVMValueRef @V, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNSWNeg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNSWNeg(LLVMBuilderRef @B, LLVMValueRef @V, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNUWNeg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNUWNeg(LLVMBuilderRef @B, LLVMValueRef @V, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFNeg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFNeg(LLVMBuilderRef @param0, LLVMValueRef @V, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildNot", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildNot(LLVMBuilderRef @param0, LLVMValueRef @V, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildMalloc", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildMalloc(LLVMBuilderRef @param0, LLVMTypeRef @Ty, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildArrayMalloc", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildArrayMalloc(LLVMBuilderRef @param0, LLVMTypeRef @Ty, LLVMValueRef @Val, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildAlloca", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildAlloca(LLVMBuilderRef @param0, LLVMTypeRef @Ty, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildArrayAlloca", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildArrayAlloca(LLVMBuilderRef @param0, LLVMTypeRef @Ty, LLVMValueRef @Val, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFree", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFree(LLVMBuilderRef @param0, LLVMValueRef @PointerVal);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildLoad", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildLoad(LLVMBuilderRef @param0, LLVMValueRef @PointerVal, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildStore", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildStore(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMValueRef @Ptr);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildGEP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildGEP(LLVMBuilderRef @B, LLVMValueRef @Pointer, out LLVMValueRef @Indices, int @NumIndices, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildInBoundsGEP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildInBoundsGEP(LLVMBuilderRef @B, LLVMValueRef @Pointer, out LLVMValueRef @Indices, int @NumIndices, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildStructGEP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildStructGEP(LLVMBuilderRef @B, LLVMValueRef @Pointer, int @Idx, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildGlobalString", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildGlobalString(LLVMBuilderRef @B, [MarshalAs(UnmanagedType.LPStr)] string @Str, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildGlobalStringPtr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildGlobalStringPtr(LLVMBuilderRef @B, [MarshalAs(UnmanagedType.LPStr)] string @Str, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMGetVolatile", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool GetVolatile(LLVMValueRef @MemoryAccessInst);

        [DllImport(LibraryName, EntryPoint = "LLVMSetVolatile", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetVolatile(LLVMValueRef @MemoryAccessInst, LLVMBool @IsVolatile);

        [DllImport(LibraryName, EntryPoint = "LLVMGetOrdering", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAtomicOrdering GetOrdering(LLVMValueRef @MemoryAccessInst);

        [DllImport(LibraryName, EntryPoint = "LLVMSetOrdering", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetOrdering(LLVMValueRef @MemoryAccessInst, LLVMAtomicOrdering @Ordering);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildTrunc", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildTrunc(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildZExt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildZExt(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildSExt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildSExt(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFPToUI", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFPToUI(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFPToSI", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFPToSI(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildUIToFP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildUIToFP(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildSIToFP", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildSIToFP(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFPTrunc", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFPTrunc(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFPExt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFPExt(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildPtrToInt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildPtrToInt(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildIntToPtr", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildIntToPtr(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildBitCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildBitCast(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildAddrSpaceCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildAddrSpaceCast(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildZExtOrBitCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildZExtOrBitCast(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildSExtOrBitCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildSExtOrBitCast(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildTruncOrBitCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildTruncOrBitCast(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildCast(LLVMBuilderRef @B, LLVMOpcode @Op, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildPointerCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildPointerCast(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildIntCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildIntCast(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFPCast", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFPCast(LLVMBuilderRef @param0, LLVMValueRef @Val, LLVMTypeRef @DestTy, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildICmp", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildICmp(LLVMBuilderRef @param0, LLVMIntPredicate @Op, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFCmp", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFCmp(LLVMBuilderRef @param0, LLVMRealPredicate @Op, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildPhi", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildPhi(LLVMBuilderRef @param0, LLVMTypeRef @Ty, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildCall", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildCall(LLVMBuilderRef @param0, LLVMValueRef @Fn, out LLVMValueRef @Args, int @NumArgs, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildSelect", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildSelect(LLVMBuilderRef @param0, LLVMValueRef @If, LLVMValueRef @Then, LLVMValueRef @Else, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildVAArg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildVAArg(LLVMBuilderRef @param0, LLVMValueRef @List, LLVMTypeRef @Ty, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildExtractElement", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildExtractElement(LLVMBuilderRef @param0, LLVMValueRef @VecVal, LLVMValueRef @Index, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildInsertElement", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildInsertElement(LLVMBuilderRef @param0, LLVMValueRef @VecVal, LLVMValueRef @EltVal, LLVMValueRef @Index, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildShuffleVector", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildShuffleVector(LLVMBuilderRef @param0, LLVMValueRef @V1, LLVMValueRef @V2, LLVMValueRef @Mask, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildExtractValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildExtractValue(LLVMBuilderRef @param0, LLVMValueRef @AggVal, int @Index, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildInsertValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildInsertValue(LLVMBuilderRef @param0, LLVMValueRef @AggVal, LLVMValueRef @EltVal, int @Index, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildIsNull", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildIsNull(LLVMBuilderRef @param0, LLVMValueRef @Val, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildIsNotNull", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildIsNotNull(LLVMBuilderRef @param0, LLVMValueRef @Val, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildPtrDiff", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildPtrDiff(LLVMBuilderRef @param0, LLVMValueRef @LHS, LLVMValueRef @RHS, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildFence", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildFence(LLVMBuilderRef @B, LLVMAtomicOrdering @ordering, LLVMBool @singleThread, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildAtomicRMW", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildAtomicRMW(LLVMBuilderRef @B, LLVMAtomicRMWBinOp @op, LLVMValueRef @PTR, LLVMValueRef @Val, LLVMAtomicOrdering @ordering, LLVMBool @singleThread);

        [DllImport(LibraryName, EntryPoint = "LLVMBuildAtomicCmpXchg", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMValueRef BuildAtomicCmpXchg(LLVMBuilderRef @B, LLVMValueRef @Ptr, LLVMValueRef @Cmp, LLVMValueRef @New, LLVMAtomicOrdering @SuccessOrdering, LLVMAtomicOrdering @FailureOrdering, LLVMBool @SingleThread);

        [DllImport(LibraryName, EntryPoint = "LLVMIsAtomicSingleThread", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsAtomicSingleThread(LLVMValueRef @AtomicInst);

        [DllImport(LibraryName, EntryPoint = "LLVMSetAtomicSingleThread", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetAtomicSingleThread(LLVMValueRef @AtomicInst, LLVMBool @SingleThread);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCmpXchgSuccessOrdering", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAtomicOrdering GetCmpXchgSuccessOrdering(LLVMValueRef @CmpXchgInst);

        [DllImport(LibraryName, EntryPoint = "LLVMSetCmpXchgSuccessOrdering", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetCmpXchgSuccessOrdering(LLVMValueRef @CmpXchgInst, LLVMAtomicOrdering @Ordering);

        [DllImport(LibraryName, EntryPoint = "LLVMGetCmpXchgFailureOrdering", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMAtomicOrdering GetCmpXchgFailureOrdering(LLVMValueRef @CmpXchgInst);

        [DllImport(LibraryName, EntryPoint = "LLVMSetCmpXchgFailureOrdering", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetCmpXchgFailureOrdering(LLVMValueRef @CmpXchgInst, LLVMAtomicOrdering @Ordering);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateModuleProviderForExistingModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMModuleProviderRef CreateModuleProviderForExistingModule(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeModuleProvider", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeModuleProvider(LLVMModuleProviderRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateMemoryBufferWithContentsOfFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool CreateMemoryBufferWithContentsOfFile([MarshalAs(UnmanagedType.LPStr)] string @Path, out LLVMMemoryBufferRef @OutMemBuf, out IntPtr @OutMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateMemoryBufferWithSTDIN", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool CreateMemoryBufferWithSTDIN(out LLVMMemoryBufferRef @OutMemBuf, out IntPtr @OutMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateMemoryBufferWithMemoryRange", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMMemoryBufferRef CreateMemoryBufferWithMemoryRange([MarshalAs(UnmanagedType.LPStr)] string @InputData, IntPtr @InputDataLength, [MarshalAs(UnmanagedType.LPStr)] string @BufferName, LLVMBool @RequiresNullTerminator);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateMemoryBufferWithMemoryRangeCopy", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMMemoryBufferRef CreateMemoryBufferWithMemoryRangeCopy([MarshalAs(UnmanagedType.LPStr)] string @InputData, IntPtr @InputDataLength, [MarshalAs(UnmanagedType.LPStr)] string @BufferName);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBufferStart", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetBufferStart(LLVMMemoryBufferRef @MemBuf);

        [DllImport(LibraryName, EntryPoint = "LLVMGetBufferSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetBufferSize(LLVMMemoryBufferRef @MemBuf);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeMemoryBuffer", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeMemoryBuffer(LLVMMemoryBufferRef @MemBuf);

        [DllImport(LibraryName, EntryPoint = "LLVMGetGlobalPassRegistry", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMPassRegistryRef GetGlobalPassRegistry();

        [DllImport(LibraryName, EntryPoint = "LLVMCreatePassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMPassManagerRef CreatePassManager();

        [DllImport(LibraryName, EntryPoint = "LLVMCreateFunctionPassManagerForModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMPassManagerRef CreateFunctionPassManagerForModule(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateFunctionPassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMPassManagerRef CreateFunctionPassManager(LLVMModuleProviderRef @MP);

        [DllImport(LibraryName, EntryPoint = "LLVMRunPassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool RunPassManager(LLVMPassManagerRef @PM, LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeFunctionPassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool InitializeFunctionPassManager(LLVMPassManagerRef @FPM);

        [DllImport(LibraryName, EntryPoint = "LLVMRunFunctionPassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool RunFunctionPassManager(LLVMPassManagerRef @FPM, LLVMValueRef @F);

        [DllImport(LibraryName, EntryPoint = "LLVMFinalizeFunctionPassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool FinalizeFunctionPassManager(LLVMPassManagerRef @FPM);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposePassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposePassManager(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMStartMultithreaded", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool StartMultithreaded();

        [DllImport(LibraryName, EntryPoint = "LLVMStopMultithreaded", CallingConvention = CallingConvention.Cdecl)]
        public static extern void StopMultithreaded();

        [DllImport(LibraryName, EntryPoint = "LLVMIsMultithreaded", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsMultithreaded();

        [DllImport(LibraryName, EntryPoint = "LLVMCreateDisasm", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMDisasmContextRef CreateDisasm([MarshalAs(UnmanagedType.LPStr)] string @TripleName, IntPtr @DisInfo, int @TagType, LLVMOpInfoCallback @GetOpInfo, LLVMSymbolLookupCallback @SymbolLookUp);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateDisasmCPU", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMDisasmContextRef CreateDisasmCPU([MarshalAs(UnmanagedType.LPStr)] string @Triple, [MarshalAs(UnmanagedType.LPStr)] string @CPU, IntPtr @DisInfo, int @TagType, LLVMOpInfoCallback @GetOpInfo, LLVMSymbolLookupCallback @SymbolLookUp);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateDisasmCPUFeatures", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMDisasmContextRef CreateDisasmCPUFeatures([MarshalAs(UnmanagedType.LPStr)] string @Triple, [MarshalAs(UnmanagedType.LPStr)] string @CPU, [MarshalAs(UnmanagedType.LPStr)] string @Features, IntPtr @DisInfo, int @TagType, LLVMOpInfoCallback @GetOpInfo, LLVMSymbolLookupCallback @SymbolLookUp);

        [DllImport(LibraryName, EntryPoint = "LLVMSetDisasmOptions", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetDisasmOptions(LLVMDisasmContextRef @DC, int @Options);

        [DllImport(LibraryName, EntryPoint = "LLVMDisasmDispose", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisasmDispose(LLVMDisasmContextRef @DC);

        [DllImport(LibraryName, EntryPoint = "LLVMDisasmInstruction", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DisasmInstruction(LLVMDisasmContextRef @DC, out int @Bytes, int @BytesSize, int @PC, IntPtr @OutString, IntPtr @OutStringSize);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeNVPTXTargetInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeNVPTXTargetInfo();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeX86TargetInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeX86TargetInfo();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeNVPTXTarget", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeNVPTXTarget();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeX86Target", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeX86Target();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeNVPTXTargetMC", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeNVPTXTargetMC();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeX86TargetMC", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeX86TargetMC();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeNVPTXAsmPrinter", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeNVPTXAsmPrinter();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeX86AsmPrinter", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeX86AsmPrinter();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeX86AsmParser", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeX86AsmParser();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeX86Disassembler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeX86Disassembler();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeAllTargetInfos", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeAllTargetInfos();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeAllTargets", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeAllTargets();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeAllTargetMCs", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeAllTargetMCs();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeAllAsmPrinters", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeAllAsmPrinters();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeAllAsmParsers", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeAllAsmParsers();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeAllDisassemblers", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeAllDisassemblers();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeNativeTarget", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool InitializeNativeTarget();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeNativeAsmParser", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool InitializeNativeAsmParser();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeNativeAsmPrinter", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool InitializeNativeAsmPrinter();

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeNativeDisassembler", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool InitializeNativeDisassembler();

        [DllImport(LibraryName, EntryPoint = "LLVMGetModuleDataLayout", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetDataRef GetModuleDataLayout(LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMSetModuleDataLayout", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetModuleDataLayout(LLVMModuleRef @M, LLVMTargetDataRef @DL);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateTargetData", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetDataRef CreateTargetData([MarshalAs(UnmanagedType.LPStr)] string @StringRep);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeTargetData", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeTargetData(LLVMTargetDataRef @TD);

        [DllImport(LibraryName, EntryPoint = "LLVMAddTargetLibraryInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddTargetLibraryInfo(LLVMTargetLibraryInfoRef @TLI, LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMCopyStringRepOfTargetData", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CopyStringRepOfTargetData(LLVMTargetDataRef @TD);

        [DllImport(LibraryName, EntryPoint = "LLVMByteOrder", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMByteOrdering ByteOrder(LLVMTargetDataRef @TD);

        [DllImport(LibraryName, EntryPoint = "LLVMPointerSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int PointerSize(LLVMTargetDataRef @TD);

        [DllImport(LibraryName, EntryPoint = "LLVMPointerSizeForAS", CallingConvention = CallingConvention.Cdecl)]
        public static extern int PointerSizeForAS(LLVMTargetDataRef @TD, int @AS);

        [DllImport(LibraryName, EntryPoint = "LLVMIntPtrType", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef IntPtrType(LLVMTargetDataRef @TD);

        [DllImport(LibraryName, EntryPoint = "LLVMIntPtrTypeForAS", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef IntPtrTypeForAS(LLVMTargetDataRef @TD, int @AS);

        [DllImport(LibraryName, EntryPoint = "LLVMIntPtrTypeInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef IntPtrTypeInContext(LLVMContextRef @C, LLVMTargetDataRef @TD);

        [DllImport(LibraryName, EntryPoint = "LLVMIntPtrTypeForASInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTypeRef IntPtrTypeForASInContext(LLVMContextRef @C, LLVMTargetDataRef @TD, int @AS);

        [DllImport(LibraryName, EntryPoint = "LLVMSizeOfTypeInBits", CallingConvention = CallingConvention.Cdecl)]
        public static extern long SizeOfTypeInBits(LLVMTargetDataRef @TD, LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMStoreSizeOfType", CallingConvention = CallingConvention.Cdecl)]
        public static extern long StoreSizeOfType(LLVMTargetDataRef @TD, LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMABISizeOfType", CallingConvention = CallingConvention.Cdecl)]
        public static extern long ABISizeOfType(LLVMTargetDataRef @TD, LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMABIAlignmentOfType", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ABIAlignmentOfType(LLVMTargetDataRef @TD, LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMCallFrameAlignmentOfType", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CallFrameAlignmentOfType(LLVMTargetDataRef @TD, LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMPreferredAlignmentOfType", CallingConvention = CallingConvention.Cdecl)]
        public static extern int PreferredAlignmentOfType(LLVMTargetDataRef @TD, LLVMTypeRef @Ty);

        [DllImport(LibraryName, EntryPoint = "LLVMPreferredAlignmentOfGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern int PreferredAlignmentOfGlobal(LLVMTargetDataRef @TD, LLVMValueRef @GlobalVar);

        [DllImport(LibraryName, EntryPoint = "LLVMElementAtOffset", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ElementAtOffset(LLVMTargetDataRef @TD, LLVMTypeRef @StructTy, long @Offset);

        [DllImport(LibraryName, EntryPoint = "LLVMOffsetOfElement", CallingConvention = CallingConvention.Cdecl)]
        public static extern long OffsetOfElement(LLVMTargetDataRef @TD, LLVMTypeRef @StructTy, int @Element);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFirstTarget", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetRef GetFirstTarget();

        [DllImport(LibraryName, EntryPoint = "LLVMGetNextTarget", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetRef GetNextTarget(LLVMTargetRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTargetFromName", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetRef GetTargetFromName([MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTargetFromTriple", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool GetTargetFromTriple([MarshalAs(UnmanagedType.LPStr)] string @Triple, out LLVMTargetRef @T, out IntPtr @ErrorMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTargetName", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetTargetName(LLVMTargetRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTargetDescription", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetTargetDescription(LLVMTargetRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMTargetHasJIT", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool TargetHasJIT(LLVMTargetRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMTargetHasTargetMachine", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool TargetHasTargetMachine(LLVMTargetRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMTargetHasAsmBackend", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool TargetHasAsmBackend(LLVMTargetRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateTargetMachine", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetMachineRef CreateTargetMachine(LLVMTargetRef @T, [MarshalAs(UnmanagedType.LPStr)] string @Triple, [MarshalAs(UnmanagedType.LPStr)] string @CPU, [MarshalAs(UnmanagedType.LPStr)] string @Features, LLVMCodeGenOptLevel @Level, LLVMRelocMode @Reloc, LLVMCodeModel @CodeModel);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeTargetMachine", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeTargetMachine(LLVMTargetMachineRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTargetMachineTarget", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetRef GetTargetMachineTarget(LLVMTargetMachineRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTargetMachineTriple", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetTargetMachineTriple(LLVMTargetMachineRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTargetMachineCPU", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetTargetMachineCPU(LLVMTargetMachineRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMGetTargetMachineFeatureString", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetTargetMachineFeatureString(LLVMTargetMachineRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateTargetDataLayout", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetDataRef CreateTargetDataLayout(LLVMTargetMachineRef @T);

        [DllImport(LibraryName, EntryPoint = "LLVMSetTargetMachineAsmVerbosity", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetTargetMachineAsmVerbosity(LLVMTargetMachineRef @T, LLVMBool @VerboseAsm);

        [DllImport(LibraryName, EntryPoint = "LLVMTargetMachineEmitToFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool TargetMachineEmitToFile(LLVMTargetMachineRef @T, LLVMModuleRef @M, IntPtr @Filename, LLVMCodeGenFileType @codegen, out IntPtr @ErrorMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMTargetMachineEmitToMemoryBuffer", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool TargetMachineEmitToMemoryBuffer(LLVMTargetMachineRef @T, LLVMModuleRef @M, LLVMCodeGenFileType @codegen, out IntPtr @ErrorMessage, out LLVMMemoryBufferRef @OutMemBuf);

        [DllImport(LibraryName, EntryPoint = "LLVMGetDefaultTargetTriple", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetDefaultTargetTriple();

        [DllImport(LibraryName, EntryPoint = "LLVMAddAnalysisPasses", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddAnalysisPasses(LLVMTargetMachineRef @T, LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMLinkInMCJIT", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LinkInMCJIT();

        [DllImport(LibraryName, EntryPoint = "LLVMLinkInInterpreter", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LinkInInterpreter();

        [DllImport(LibraryName, EntryPoint = "LLVMCreateGenericValueOfInt", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMGenericValueRef CreateGenericValueOfInt(LLVMTypeRef @Ty, long @N, LLVMBool @IsSigned);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateGenericValueOfPointer", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMGenericValueRef CreateGenericValueOfPointer(IntPtr @P);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateGenericValueOfFloat", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMGenericValueRef CreateGenericValueOfFloat(LLVMTypeRef @Ty, double @N);

        [DllImport(LibraryName, EntryPoint = "LLVMGenericValueIntWidth", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GenericValueIntWidth(LLVMGenericValueRef @GenValRef);

        [DllImport(LibraryName, EntryPoint = "LLVMGenericValueToInt", CallingConvention = CallingConvention.Cdecl)]
        public static extern long GenericValueToInt(LLVMGenericValueRef @GenVal, LLVMBool @IsSigned);

        [DllImport(LibraryName, EntryPoint = "LLVMGenericValueToPointer", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GenericValueToPointer(LLVMGenericValueRef @GenVal);

        [DllImport(LibraryName, EntryPoint = "LLVMGenericValueToFloat", CallingConvention = CallingConvention.Cdecl)]
        public static extern double GenericValueToFloat(LLVMTypeRef @TyRef, LLVMGenericValueRef @GenVal);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeGenericValue", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeGenericValue(LLVMGenericValueRef @GenVal);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateExecutionEngineForModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool CreateExecutionEngineForModule(out LLVMExecutionEngineRef @OutEE, LLVMModuleRef @M, out IntPtr @OutError);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateInterpreterForModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool CreateInterpreterForModule(out LLVMExecutionEngineRef @OutInterp, LLVMModuleRef @M, out IntPtr @OutError);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateJITCompilerForModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool CreateJITCompilerForModule(out LLVMExecutionEngineRef @OutJIT, LLVMModuleRef @M, int @OptLevel, out IntPtr @OutError);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeMCJITCompilerOptions", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeMCJITCompilerOptions(out LLVMMCJITCompilerOptions @Options, IntPtr @SizeOfOptions);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateMCJITCompilerForModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool CreateMCJITCompilerForModule(out LLVMExecutionEngineRef @OutJIT, LLVMModuleRef @M, out LLVMMCJITCompilerOptions @Options, IntPtr @SizeOfOptions, out IntPtr @OutError);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeExecutionEngine", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeExecutionEngine(LLVMExecutionEngineRef @EE);

        [DllImport(LibraryName, EntryPoint = "LLVMRunStaticConstructors", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RunStaticConstructors(LLVMExecutionEngineRef @EE);

        [DllImport(LibraryName, EntryPoint = "LLVMRunStaticDestructors", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RunStaticDestructors(LLVMExecutionEngineRef @EE);

        [DllImport(LibraryName, EntryPoint = "LLVMRunFunctionAsMain", CallingConvention = CallingConvention.Cdecl)]
        public static extern int RunFunctionAsMain(LLVMExecutionEngineRef @EE, LLVMValueRef @F, int @ArgC, string[] @ArgV, string[] @EnvP);

        [DllImport(LibraryName, EntryPoint = "LLVMRunFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMGenericValueRef RunFunction(LLVMExecutionEngineRef @EE, LLVMValueRef @F, int @NumArgs, out LLVMGenericValueRef @Args);

        [DllImport(LibraryName, EntryPoint = "LLVMFreeMachineCodeForFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeMachineCodeForFunction(LLVMExecutionEngineRef @EE, LLVMValueRef @F);

        [DllImport(LibraryName, EntryPoint = "LLVMAddModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddModule(LLVMExecutionEngineRef @EE, LLVMModuleRef @M);

        [DllImport(LibraryName, EntryPoint = "LLVMRemoveModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool RemoveModule(LLVMExecutionEngineRef @EE, LLVMModuleRef @M, out LLVMModuleRef @OutMod, out IntPtr @OutError);

        [DllImport(LibraryName, EntryPoint = "LLVMFindFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool FindFunction(LLVMExecutionEngineRef @EE, [MarshalAs(UnmanagedType.LPStr)] string @Name, out LLVMValueRef @OutFn);

        [DllImport(LibraryName, EntryPoint = "LLVMRecompileAndRelinkFunction", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr RecompileAndRelinkFunction(LLVMExecutionEngineRef @EE, LLVMValueRef @Fn);

        [DllImport(LibraryName, EntryPoint = "LLVMGetExecutionEngineTargetData", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetDataRef GetExecutionEngineTargetData(LLVMExecutionEngineRef @EE);

        [DllImport(LibraryName, EntryPoint = "LLVMGetExecutionEngineTargetMachine", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMTargetMachineRef GetExecutionEngineTargetMachine(LLVMExecutionEngineRef @EE);

        [DllImport(LibraryName, EntryPoint = "LLVMAddGlobalMapping", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddGlobalMapping(LLVMExecutionEngineRef @EE, LLVMValueRef @Global, IntPtr @Addr);

        [DllImport(LibraryName, EntryPoint = "LLVMGetPointerToGlobal", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetPointerToGlobal(LLVMExecutionEngineRef @EE, LLVMValueRef @Global);

        [DllImport(LibraryName, EntryPoint = "LLVMGetGlobalValueAddress", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetGlobalValueAddress(LLVMExecutionEngineRef @EE, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMGetFunctionAddress", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetFunctionAddress(LLVMExecutionEngineRef @EE, [MarshalAs(UnmanagedType.LPStr)] string @Name);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateSimpleMCJITMemoryManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMMCJITMemoryManagerRef CreateSimpleMCJITMemoryManager(IntPtr @Opaque, LLVMMemoryManagerAllocateCodeSectionCallback @AllocateCodeSection, LLVMMemoryManagerAllocateDataSectionCallback @AllocateDataSection, LLVMMemoryManagerFinalizeMemoryCallback @FinalizeMemory, LLVMMemoryManagerDestroyCallback @Destroy);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeMCJITMemoryManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeMCJITMemoryManager(LLVMMCJITMemoryManagerRef @MM);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeTransformUtils", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeTransformUtils(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeScalarOpts", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeScalarOpts(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeObjCARCOpts", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeObjCARCOpts(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeVectorization", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeVectorization(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeInstCombine", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeInstCombine(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeIPO", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeIPO(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeInstrumentation", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeInstrumentation(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeAnalysis", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeAnalysis(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeIPA", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeIPA(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeCodeGen", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeCodeGen(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMInitializeTarget", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitializeTarget(LLVMPassRegistryRef @R);

        [DllImport(LibraryName, EntryPoint = "LLVMParseIRInContext", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool ParseIRInContext(LLVMContextRef @ContextRef, LLVMMemoryBufferRef @MemBuf, out LLVMModuleRef @OutM, out IntPtr @OutMessage);

        [DllImport(LibraryName, EntryPoint = "LLVMLinkModules2", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool LinkModules2(LLVMModuleRef @Dest, LLVMModuleRef @Src);

        [DllImport(LibraryName, EntryPoint = "LLVMCreateObjectFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMObjectFileRef CreateObjectFile(LLVMMemoryBufferRef @MemBuf);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeObjectFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeObjectFile(LLVMObjectFileRef @ObjectFile);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSections", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMSectionIteratorRef GetSections(LLVMObjectFileRef @ObjectFile);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeSectionIterator", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeSectionIterator(LLVMSectionIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMIsSectionIteratorAtEnd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsSectionIteratorAtEnd(LLVMObjectFileRef @ObjectFile, LLVMSectionIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMMoveToNextSection", CallingConvention = CallingConvention.Cdecl)]
        public static extern void MoveToNextSection(LLVMSectionIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMMoveToContainingSection", CallingConvention = CallingConvention.Cdecl)]
        public static extern void MoveToContainingSection(LLVMSectionIteratorRef @Sect, LLVMSymbolIteratorRef @Sym);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSymbols", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMSymbolIteratorRef GetSymbols(LLVMObjectFileRef @ObjectFile);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeSymbolIterator", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeSymbolIterator(LLVMSymbolIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMIsSymbolIteratorAtEnd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsSymbolIteratorAtEnd(LLVMObjectFileRef @ObjectFile, LLVMSymbolIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMMoveToNextSymbol", CallingConvention = CallingConvention.Cdecl)]
        public static extern void MoveToNextSymbol(LLVMSymbolIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSectionName", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetSectionName(LLVMSectionIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSectionSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetSectionSize(LLVMSectionIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSectionContents", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetSectionContents(LLVMSectionIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSectionAddress", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetSectionAddress(LLVMSectionIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSectionContainsSymbol", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool GetSectionContainsSymbol(LLVMSectionIteratorRef @SI, LLVMSymbolIteratorRef @Sym);

        [DllImport(LibraryName, EntryPoint = "LLVMGetRelocations", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMRelocationIteratorRef GetRelocations(LLVMSectionIteratorRef @Section);

        [DllImport(LibraryName, EntryPoint = "LLVMDisposeRelocationIterator", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeRelocationIterator(LLVMRelocationIteratorRef @RI);

        [DllImport(LibraryName, EntryPoint = "LLVMIsRelocationIteratorAtEnd", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool IsRelocationIteratorAtEnd(LLVMSectionIteratorRef @Section, LLVMRelocationIteratorRef @RI);

        [DllImport(LibraryName, EntryPoint = "LLVMMoveToNextRelocation", CallingConvention = CallingConvention.Cdecl)]
        public static extern void MoveToNextRelocation(LLVMRelocationIteratorRef @RI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSymbolName", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetSymbolName(LLVMSymbolIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSymbolAddress", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetSymbolAddress(LLVMSymbolIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetSymbolSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetSymbolSize(LLVMSymbolIteratorRef @SI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetRelocationOffset", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRelocationOffset(LLVMRelocationIteratorRef @RI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetRelocationSymbol", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMSymbolIteratorRef GetRelocationSymbol(LLVMRelocationIteratorRef @RI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetRelocationType", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRelocationType(LLVMRelocationIteratorRef @RI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetRelocationTypeName", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetRelocationTypeName(LLVMRelocationIteratorRef @RI);

        [DllImport(LibraryName, EntryPoint = "LLVMGetRelocationValueString", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetRelocationValueString(LLVMRelocationIteratorRef @RI);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcCreateInstance", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOrcJITStackRef OrcCreateInstance(LLVMTargetMachineRef @TM);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcGetErrorMsg", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OrcGetErrorMsg(LLVMOrcJITStackRef @JITStack);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcGetMangledSymbol", CallingConvention = CallingConvention.Cdecl)]
        public static extern void OrcGetMangledSymbol(LLVMOrcJITStackRef @JITStack, out IntPtr @MangledSymbol, [MarshalAs(UnmanagedType.LPStr)] string @Symbol);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcDisposeMangledSymbol", CallingConvention = CallingConvention.Cdecl)]
        public static extern void OrcDisposeMangledSymbol(IntPtr @MangledSymbol);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcCreateLazyCompileCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOrcTargetAddress OrcCreateLazyCompileCallback(LLVMOrcJITStackRef @JITStack, LLVMOrcLazyCompileCallbackFn @Callback, IntPtr @CallbackCtx);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcCreateIndirectStub", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOrcErrorCode OrcCreateIndirectStub(LLVMOrcJITStackRef @JITStack, [MarshalAs(UnmanagedType.LPStr)] string @StubName, LLVMOrcTargetAddress @InitAddr);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcSetIndirectStubPointer", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOrcErrorCode OrcSetIndirectStubPointer(LLVMOrcJITStackRef @JITStack, [MarshalAs(UnmanagedType.LPStr)] string @StubName, LLVMOrcTargetAddress @NewAddr);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcAddEagerlyCompiledIR", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOrcModuleHandle OrcAddEagerlyCompiledIR(LLVMOrcJITStackRef @JITStack, LLVMModuleRef @Mod, LLVMOrcSymbolResolverFn @SymbolResolver, IntPtr @SymbolResolverCtx);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcAddLazilyCompiledIR", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOrcModuleHandle OrcAddLazilyCompiledIR(LLVMOrcJITStackRef @JITStack, LLVMModuleRef @Mod, LLVMOrcSymbolResolverFn @SymbolResolver, IntPtr @SymbolResolverCtx);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcAddObjectFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOrcModuleHandle OrcAddObjectFile(LLVMOrcJITStackRef @JITStack, LLVMObjectFileRef @Obj, LLVMOrcSymbolResolverFn @SymbolResolver, IntPtr @SymbolResolverCtx);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcRemoveModule", CallingConvention = CallingConvention.Cdecl)]
        public static extern void OrcRemoveModule(LLVMOrcJITStackRef @JITStack, LLVMOrcModuleHandle @H);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcGetSymbolAddress", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMOrcTargetAddress OrcGetSymbolAddress(LLVMOrcJITStackRef @JITStack, [MarshalAs(UnmanagedType.LPStr)] string @SymbolName);

        [DllImport(LibraryName, EntryPoint = "LLVMOrcDisposeInstance", CallingConvention = CallingConvention.Cdecl)]
        public static extern void OrcDisposeInstance(LLVMOrcJITStackRef @JITStack);

        [DllImport(LibraryName, EntryPoint = "LLVMLoadLibraryPermanently", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMBool LoadLibraryPermanently([MarshalAs(UnmanagedType.LPStr)] string @Filename);

        [DllImport(LibraryName, EntryPoint = "LLVMParseCommandLineOptions", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ParseCommandLineOptions(int @argc, string[] @argv, [MarshalAs(UnmanagedType.LPStr)] string @Overview);

        [DllImport(LibraryName, EntryPoint = "LLVMSearchForAddressOfSymbol", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SearchForAddressOfSymbol([MarshalAs(UnmanagedType.LPStr)] string @symbolName);

        [DllImport(LibraryName, EntryPoint = "LLVMAddSymbol", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddSymbol([MarshalAs(UnmanagedType.LPStr)] string @symbolName, IntPtr @symbolValue);

        [DllImport(LibraryName, EntryPoint = "LLVMAddArgumentPromotionPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddArgumentPromotionPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddConstantMergePass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddConstantMergePass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddDeadArgEliminationPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddDeadArgEliminationPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddFunctionAttrsPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddFunctionAttrsPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddFunctionInliningPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddFunctionInliningPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddAlwaysInlinerPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddAlwaysInlinerPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddGlobalDCEPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddGlobalDCEPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddGlobalOptimizerPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddGlobalOptimizerPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddIPConstantPropagationPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddIPConstantPropagationPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddPruneEHPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddPruneEHPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddIPSCCPPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddIPSCCPPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddInternalizePass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddInternalizePass(LLVMPassManagerRef @param0, int @AllButMain);

        [DllImport(LibraryName, EntryPoint = "LLVMAddStripDeadPrototypesPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddStripDeadPrototypesPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddStripSymbolsPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddStripSymbolsPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderCreate", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMPassManagerBuilderRef PassManagerBuilderCreate();

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderDispose", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderDispose(LLVMPassManagerBuilderRef @PMB);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderSetOptLevel", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderSetOptLevel(LLVMPassManagerBuilderRef @PMB, int @OptLevel);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderSetSizeLevel", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderSetSizeLevel(LLVMPassManagerBuilderRef @PMB, int @SizeLevel);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderSetDisableUnitAtATime", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderSetDisableUnitAtATime(LLVMPassManagerBuilderRef @PMB, LLVMBool @Value);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderSetDisableUnrollLoops", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderSetDisableUnrollLoops(LLVMPassManagerBuilderRef @PMB, LLVMBool @Value);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderSetDisableSimplifyLibCalls", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderSetDisableSimplifyLibCalls(LLVMPassManagerBuilderRef @PMB, LLVMBool @Value);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderUseInlinerWithThreshold", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderUseInlinerWithThreshold(LLVMPassManagerBuilderRef @PMB, int @Threshold);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderPopulateFunctionPassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderPopulateFunctionPassManager(LLVMPassManagerBuilderRef @PMB, LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderPopulateModulePassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderPopulateModulePassManager(LLVMPassManagerBuilderRef @PMB, LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMPassManagerBuilderPopulateLTOPassManager", CallingConvention = CallingConvention.Cdecl)]
        public static extern void PassManagerBuilderPopulateLTOPassManager(LLVMPassManagerBuilderRef @PMB, LLVMPassManagerRef @PM, LLVMBool @Internalize, LLVMBool @RunInliner);

        [DllImport(LibraryName, EntryPoint = "LLVMAddAggressiveDCEPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddAggressiveDCEPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddBitTrackingDCEPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddBitTrackingDCEPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddAlignmentFromAssumptionsPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddAlignmentFromAssumptionsPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddCFGSimplificationPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddCFGSimplificationPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddDeadStoreEliminationPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddDeadStoreEliminationPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddScalarizerPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddScalarizerPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddMergedLoadStoreMotionPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddMergedLoadStoreMotionPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddGVNPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddGVNPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddIndVarSimplifyPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddIndVarSimplifyPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddInstructionCombiningPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddInstructionCombiningPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddJumpThreadingPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddJumpThreadingPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLICMPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLICMPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLoopDeletionPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLoopDeletionPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLoopIdiomPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLoopIdiomPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLoopRotatePass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLoopRotatePass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLoopRerollPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLoopRerollPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLoopUnrollPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLoopUnrollPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLoopUnswitchPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLoopUnswitchPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddMemCpyOptPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddMemCpyOptPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddPartiallyInlineLibCallsPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddPartiallyInlineLibCallsPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLowerSwitchPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLowerSwitchPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddPromoteMemoryToRegisterPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddPromoteMemoryToRegisterPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddReassociatePass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddReassociatePass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddSCCPPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddSCCPPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddScalarReplAggregatesPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddScalarReplAggregatesPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddScalarReplAggregatesPassSSA", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddScalarReplAggregatesPassSSA(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddScalarReplAggregatesPassWithThreshold", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddScalarReplAggregatesPassWithThreshold(LLVMPassManagerRef @PM, int @Threshold);

        [DllImport(LibraryName, EntryPoint = "LLVMAddSimplifyLibCallsPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddSimplifyLibCallsPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddTailCallEliminationPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddTailCallEliminationPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddConstantPropagationPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddConstantPropagationPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddDemoteMemoryToRegisterPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddDemoteMemoryToRegisterPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddVerifierPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddVerifierPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddCorrelatedValuePropagationPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddCorrelatedValuePropagationPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddEarlyCSEPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddEarlyCSEPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLowerExpectIntrinsicPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLowerExpectIntrinsicPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddTypeBasedAliasAnalysisPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddTypeBasedAliasAnalysisPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddScopedNoAliasAAPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddScopedNoAliasAAPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddBasicAliasAnalysisPass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddBasicAliasAnalysisPass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddBBVectorizePass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddBBVectorizePass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddLoopVectorizePass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddLoopVectorizePass(LLVMPassManagerRef @PM);

        [DllImport(LibraryName, EntryPoint = "LLVMAddSLPVectorizePass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddSLPVectorizePass(LLVMPassManagerRef @PM);

    }
}
#pragma warning restore IDE0003 // Remove qualification
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
