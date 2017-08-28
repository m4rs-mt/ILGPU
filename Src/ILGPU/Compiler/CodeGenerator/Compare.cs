// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Resources;
using ILGPU.Util;
using System.Diagnostics;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Realizes a compare instruction of the given type.
        /// </summary>
        /// <param name="compareType">The comparison type to use.</param>
        /// <param name="forceUnsigned">True, if the comparison should be forced to be unsigned.</param>
        private void MakeCompare(CompareType compareType, bool forceUnsigned = false)
        {
            CurrentBlock.Push(CreateCompare(compareType, forceUnsigned));
        }

        /// <summary>
        /// Creates a compare instruction of the given type.
        /// </summary>
        /// <param name="compareType">The comparison type to use.</param>
        /// <param name="forceUnsigned">True, if the comparison should be forced to be unsigned.</param>
        private Value CreateCompare(CompareType compareType, bool forceUnsigned = false)
        {
            var right = CurrentBlock.PopCompareOrArithmeticValue();
            var left = CurrentBlock.PopCompareOrArithmeticValue();
            right = CreateConversion(right, left.ValueType);
            left = CreateConversion(left, right.ValueType);
            Debug.Assert(left.BasicValueType == right.BasicValueType);

            if (left.BasicValueType.IsPtrOrInt())
            {
                LLVMIntPredicate intPredicate;
                bool unsigned = forceUnsigned || left.BasicValueType.IsUnsignedInt();
                switch (compareType)
                {
                    case CompareType.Equal:
                        intPredicate = LLVMIntPredicate.LLVMIntEQ;
                        break;
                    case CompareType.GreaterEqual:
                        intPredicate = unsigned ? LLVMIntPredicate.LLVMIntUGE : LLVMIntPredicate.LLVMIntSGE;
                        break;
                    case CompareType.GreaterThan:
                        intPredicate = unsigned ? LLVMIntPredicate.LLVMIntUGT : LLVMIntPredicate.LLVMIntSGT;
                        break;
                    case CompareType.LessEqual:
                        intPredicate = unsigned ? LLVMIntPredicate.LLVMIntULE : LLVMIntPredicate.LLVMIntSLE;
                        break;
                    case CompareType.LessThan:
                        intPredicate = unsigned ? LLVMIntPredicate.LLVMIntULT : LLVMIntPredicate.LLVMIntSLT;
                        break;
                    case CompareType.NotEqual:
                        intPredicate = LLVMIntPredicate.LLVMIntNE;
                        break;
                    default:
                        throw CompilationContext.GetNotSupportedException(
                            ErrorMessages.NotSupportedIntComparison, compareType);
                }
                return new Value(
                    typeof(bool),
                    BuildICmp(Builder, intPredicate, left.LLVMValue, right.LLVMValue, "icmp"));
            }
            else if (left.BasicValueType.IsFloat())
            {
                // TODO: rare case of unordered comparisons in case of NaN
                LLVMRealPredicate realPredicate;
                switch (compareType)
                {
                    case CompareType.Equal:
                        realPredicate = LLVMRealPredicate.LLVMRealOEQ;
                        break;
                    case CompareType.GreaterEqual:
                        realPredicate = LLVMRealPredicate.LLVMRealOGE;
                        break;
                    case CompareType.GreaterThan:
                        realPredicate = LLVMRealPredicate.LLVMRealOGT;
                        break;
                    case CompareType.LessEqual:
                        realPredicate = LLVMRealPredicate.LLVMRealOLE;
                        break;
                    case CompareType.LessThan:
                        realPredicate = LLVMRealPredicate.LLVMRealOLT;
                        break;
                    case CompareType.NotEqual:
                        realPredicate = LLVMRealPredicate.LLVMRealONE;
                        break;
                    default:
                        throw CompilationContext.GetNotSupportedException(
                            ErrorMessages.NotSupportedFloatComparison, compareType);
                }
                return new Value(
                    typeof(bool),
                    BuildFCmp(Builder, realPredicate, left.LLVMValue, right.LLVMValue, "fcmp"));
            }
            else
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedCompareOperation, left.BasicValueType, right.BasicValueType);
        }
    }
}
