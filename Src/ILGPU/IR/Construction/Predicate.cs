// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Predicate.cs
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
        /// Creates a conditional if predicate.
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

            // Match simple constant predicates
            if (condition is PrimitiveValue constant)
                return constant.Int1Value ? trueValue : falseValue;

            // Match bool predicates that can be represented via simple expressions
            if (trueValue.BasicValueType == BasicValueType.Int1)
            {
                // Check for two cases: condition ? True : falseValue
                //                       --> condition | falseValue
                //                      condition ? False : falseValue
                //                       --> !condition & falseValue
                if (trueValue is PrimitiveValue truePrimitive)
                {
                    var kind = BinaryArithmeticKind.Or;
                    // Check for: condition ? False ...
                    if (!truePrimitive.Int1Value)
                    {
                        kind = BinaryArithmeticKind.And;
                        condition = CreateArithmetic(
                            location,
                            condition,
                            UnaryArithmeticKind.Not);
                    }
                    return CreateArithmetic(
                        location,
                        condition,
                        falseValue,
                        kind,
                        ArithmeticFlags.Unsigned);
                }
                // Move constants to the left
                else if (falseValue is PrimitiveValue falsePrimitive)
                {
                    return CreatePredicate(
                        location,
                        CreateArithmetic(
                            location,
                            condition,
                            UnaryArithmeticKind.Not),
                        falseValue,
                        trueValue);
                }

                // If we arrive here we cannot merge any constants
            }

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
