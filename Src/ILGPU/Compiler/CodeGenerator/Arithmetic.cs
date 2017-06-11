// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler.Intrinsic;
using ILGPU.Resources;
using ILGPU.Util;
using LLVMSharp;
using System;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        #region Constants

        private static readonly MethodInfo MathMulF32 = typeof(GPUMath).GetMethod(
            "Mul",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(float), typeof(float) },
            null);

        private static readonly MethodInfo MathMulF64 = typeof(GPUMath).GetMethod(
            "Mul",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(double), typeof(double) },
            null);

        private static readonly MethodInfo MathDivF32 = typeof(GPUMath).GetMethod(
            "Div",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(float), typeof(float) },
            null);

        private static readonly MethodInfo MathDivF64 = typeof(GPUMath).GetMethod(
            "Div",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(double), typeof(double) },
            null);

        private static readonly MethodInfo MathRemF32 = typeof(GPUMath).GetMethod(
            "Rem",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(float), typeof(float) },
            null);

        private static readonly MethodInfo MathRemF64 = typeof(GPUMath).GetMethod(
            "Rem",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(double), typeof(double) },
            null);

        #endregion

        /// <summary>
        /// Constructs general math intrinsics.
        /// </summary>
        /// <param name="type">The type of the operation.</param>
        /// <param name="targetMethod">A reference to a specific target method.</param>
        /// <param name="left">The left param.</param>
        /// <param name="right">The right param.</param>
        private void MakeIntrinsicMathOperation(
            Type type,
            MethodInfo targetMethod,
            LLVMValueRef left,
            LLVMValueRef right)
        {
            if (!Unit.HandleIntrinsic(new InvocationContext(
                InstructionBuilder,
                Method,
                targetMethod, new Value[]
            {
                new Value(type, left),
                new Value(type, right),
            }, this), out Value? result) || !result.HasValue)
                throw CompilationContext.GetInvalidOperationException(
                    ErrorMessages.InvalidMathIntrinsic, targetMethod.Name);
            CurrentBlock.Push(result.Value);
        }

        /// <summary>
        /// Realizes an arithmetic add operation.
        /// </summary>
        /// <param name="overflow">True, if an overflow has to be checked.</param>
        private void MakeArithmeticAdd(bool overflow)
        {
            if (overflow)
                throw new NotSupportedException();
            Type type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            var name = string.Empty;
            if (type.GetBasicValueType() == BasicValueType.Ptr)
                CurrentBlock.Push(type, InstructionBuilder.CreateInBoundsGEP(left, new LLVMValueRef[] { right }, name));
            else if (type.IsFloat())
                CurrentBlock.Push(type, InstructionBuilder.CreateFAdd(left, right, name));
            else
            {
                Debug.Assert(type.IsInt());
                CurrentBlock.Push(type, InstructionBuilder.CreateAdd(left, right, name));
            }
        }

        /// <summary>
        /// Realizes an arithmetic sub operation.
        /// </summary>
        /// <param name="overflow">True, if an overflow has to be checked.</param>
        private void MakeArithmeticSub(bool overflow)
        {
            if (overflow)
                throw new NotSupportedException();
            Type type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            var name = string.Empty;
            if (type.GetBasicValueType() == BasicValueType.Ptr)
            {
                right = InstructionBuilder.CreateSub(LLVM.ConstPointerNull(right.TypeOf()), right, name);
                CurrentBlock.Push(type, InstructionBuilder.CreateInBoundsGEP(left, new LLVMValueRef[] { right }, name));
            }
            if (type.IsFloat())
                CurrentBlock.Push(type, InstructionBuilder.CreateFSub(left, right, name));
            else
                CurrentBlock.Push(type, InstructionBuilder.CreateSub(left, right, name));
        }

        /// <summary>
        /// Realizes an arithmetic mul operation.
        /// </summary>
        /// <param name="overflow">True, if an overflow has to be checked.</param>
        private void MakeArithmeticMul(bool overflow)
        {
            if (overflow)
                throw new NotSupportedException();
            Type type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            var name = string.Empty;
            var basicValueType = type.GetBasicValueType();
            if (basicValueType.IsFloat())
            {
                if (Unit.HasFlags(CompileUnitFlags.UseGPUMath))
                    MakeIntrinsicMathOperation(
                        type,
                        basicValueType == BasicValueType.Single ? MathMulF32 : MathMulF64,
                        left,
                        right);
                else
                    CurrentBlock.Push(type, InstructionBuilder.CreateFMul(left, right, name));
            }
            else
                CurrentBlock.Push(type, InstructionBuilder.CreateMul(left, right, name));
        }

        /// <summary>
        /// Realizes an arithmetic div operation.
        /// </summary>
        /// <param name="forceUnsigned">True, if the comparison should be forced to be unsigned.</param>
        private void MakeArithmeticDiv(bool forceUnsigned = false)
        {
            Type type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            var name = string.Empty;
            var basicValueType = type.GetBasicValueType();
            if (basicValueType.IsFloat())
            {
                if (Unit.HasFlags(CompileUnitFlags.UseGPUMath))
                    MakeIntrinsicMathOperation(
                        type,
                        basicValueType == BasicValueType.Single ? MathDivF32 : MathDivF64,
                        left,
                        right);
                else
                    CurrentBlock.Push(type, InstructionBuilder.CreateFDiv(left, right, name));
            }
            else if (forceUnsigned || basicValueType.IsUnsignedInt())
                CurrentBlock.Push(type, InstructionBuilder.CreateUDiv(left, right, name));
            else
                CurrentBlock.Push(type, InstructionBuilder.CreateSDiv(left, right, name));
        }

        /// <summary>
        /// Realizes an arithmetic rem operation.
        /// </summary>
        /// <param name="forceUnsigned">True, if the comparison should be forced to be unsigned.</param>
        private void MakeArithmeticRem(bool forceUnsigned = false)
        {
            Type type = CurrentBlock.PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right);
            var name = string.Empty;
            var basicValueType = type.GetBasicValueType();
            if (basicValueType.IsFloat())
            {
                if (Unit.HasFlags(CompileUnitFlags.UseGPUMath))
                    MakeIntrinsicMathOperation(
                        type,
                        basicValueType == BasicValueType.Single ? MathRemF32 : MathRemF64,
                        left,
                        right);
                else
                    CurrentBlock.Push(type, InstructionBuilder.CreateFRem(left, right, name));
            }
            else if (forceUnsigned || basicValueType.IsUnsignedInt())
                CurrentBlock.Push(type, InstructionBuilder.CreateURem(left, right, name));
            else
                CurrentBlock.Push(type, InstructionBuilder.CreateSRem(left, right, name));
        }
    }
}
