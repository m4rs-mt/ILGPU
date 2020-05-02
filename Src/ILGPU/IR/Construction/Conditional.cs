// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Conditional.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a conditional predicate.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="trueValue">The true value.</param>
        /// <param name="falseValue">The false value.</param>
        /// <returns>A node that represents the predicate operation.</returns>
        public ValueReference CreatePredicate(
            Location location,
            Value condition,
            Value trueValue,
            Value falseValue)
        {
            location.Assert(condition.Type.BasicValueType == BasicValueType.Int1);

            if (trueValue.Type != falseValue.Type)
            {
                falseValue = CreateConvert(
                    location,
                    falseValue,
                    trueValue.Type as PrimitiveType);
            }

            if (UseConstantPropagation && condition is PrimitiveValue constant)
                return constant.Int1Value ? trueValue : falseValue;

            // Match negated predicates
            return condition is UnaryArithmeticValue unary &&
                unary.Kind == UnaryArithmeticKind.Not
                ? CreatePredicate(
                    location,
                    unary.Value,
                    falseValue,
                    trueValue)
                : Append(new Predicate(
                    GetInitializer(location),
                    condition,
                    trueValue,
                    falseValue));
        }
    }
}
