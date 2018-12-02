// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Conditional.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a conditional predicate.
        /// </summary>
        /// <param name="condition">The condition.</param>
        /// <param name="trueValue">The true value.</param>
        /// <param name="falseValue">The false value.</param>
        /// <returns>A node that represents the predicate operation.</returns>
        public ValueReference CreatePredicate(
            Value condition,
            Value trueValue,
            Value falseValue)
        {
            Debug.Assert(condition != null, "Invalid condition node");
            Debug.Assert(trueValue != null, "Invalid true node");
            Debug.Assert(falseValue != null, "Invalid false node");
            Debug.Assert(condition.Type.BasicValueType == BasicValueType.Int1, "Invalid condition type");

            if (trueValue.Type != falseValue.Type)
                falseValue = CreateConvert(falseValue, trueValue.Type as PrimitiveType);
            if (UseConstantPropagation && condition is PrimitiveValue constant)
                return constant.Int1Value ? trueValue : falseValue;

            return Append(new Predicate(
                BasicBlock,
                condition,
                trueValue,
                falseValue));
        }
    }
}
