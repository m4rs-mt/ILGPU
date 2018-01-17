// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: MethodExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ILGPU.LLVM
{
    partial class LLVMMethods
    {
        public static LLVMTypeRef FunctionType(LLVMTypeRef returnType)
        {
            return FunctionType(returnType, out LLVMTypeRef paramType, 0, false);
        }

        public static LLVMTypeRef FunctionType(LLVMTypeRef returnType, params LLVMTypeRef[] paramTypes)
        {
            if (paramTypes == null || paramTypes.Length < 1)
                return FunctionType(returnType, out LLVMTypeRef paramType, 0, false);
            return FunctionType(returnType, out paramTypes[0], paramTypes.Length, false);
        }

        public static LLVMValueRef ConstStringInContext(LLVMContextRef Context, string Str, LLVMBool @DontNullTerminate)
        {
            return ConstStringInContext(Context, Str, Str.Length, DontNullTerminate);
        }

        public static LLVMValueRef[] GetParams(LLVMValueRef functionRef)
        {
            var numParams = CountParams(functionRef);
            var values = new LLVMValueRef[numParams];
            if (numParams > 0)
                GetParams(functionRef, out values[0]);
            return values;
        }

        public static LLVMValueRef BuildInBoundsGEP(LLVMBuilderRef builder, LLVMValueRef pointer, params LLVMValueRef[] indices)
        {
            if (indices == null || indices.Length < 1)
                return BuildInBoundsGEP(builder, pointer, out LLVMValueRef _, 0, string.Empty);
            return BuildInBoundsGEP(builder, pointer, out indices[0], indices.Length, string.Empty);
        }

        public static LLVMTypeRef StructTypeInContext(LLVMContextRef context, params LLVMTypeRef[] elementTypes)
        {
            if (elementTypes == null || elementTypes.Length < 1)
                return StructTypeInContext(context, out LLVMTypeRef _, 0, false);
            return StructTypeInContext(context, out elementTypes[0], elementTypes.Length, false);
        }

        public static void StructSetBody(LLVMTypeRef structType, params LLVMTypeRef[] elementTypes)
        {
            if (elementTypes == null || elementTypes.Length < 1)
                StructSetBody(structType, out LLVMTypeRef _, 0, false);
            StructSetBody(structType, out elementTypes[0], elementTypes.Length, false);
        }

        public static LLVMTypeRef[] GetStructElementTypes(LLVMTypeRef structType)
        {
            var numElementTypes = CountStructElementTypes(structType);
            var result = new LLVMTypeRef[numElementTypes];
            if (numElementTypes > 0)
                GetStructElementTypes(structType, out result[0]);
            return result;
        }

        public static LLVMTypeRef PointerType(LLVMTypeRef elementType)
        {
            return PointerType(elementType, 0);
        }

        public static LLVMValueRef BuildCall(
            LLVMBuilderRef builder,
            LLVMValueRef functionRef,
            params LLVMValueRef[] values)
        {
            if (values == null || values.Length < 1)
                return BuildCall(builder, functionRef, out LLVMValueRef emptyParam, 0, string.Empty);
            return BuildCall(builder, functionRef, out values[0], values.Length, string.Empty);
        }
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
