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
using ILGPU.Util;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a conditional predicate.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="conditionOrValue">The condition or select value.</param>
        /// <param name="values">The list of condition/select values.</param>
        /// <returns>A node that represents the conditional predicate.</returns>
        public ValueReference CreatePredicate(
            Location location,
            Value conditionOrValue,
            ref ValueList values)
        {
            if (conditionOrValue.BasicValueType == BasicValueType.Int1)
            {
                location.Assert(values.Count == 2);
                return CreateIfPredicate(
                    location,
                    conditionOrValue,
                    values[0],
                    values[1]);
            }
            else if (conditionOrValue.BasicValueType.IsInt())
            {
                var switchBuilder = CreateSwitchPredicate(
                    location,
                    conditionOrValue,
                    values.Count);
                foreach (var value in values)
                    switchBuilder.Add(value);
                return switchBuilder.Seal();
            }
            else
            {
                // Unreachable
                throw location.GetInvalidOperationException();
            }
        }

        /// <summary>
        /// Creates a conditional if predicate.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="trueValue">The true value.</param>
        /// <param name="falseValue">The false value.</param>
        /// <returns>A node that represents the predicate operation.</returns>
        public ValueReference CreateIfPredicate(
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
            if (UseConstantPropagation)
            {
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
                        return CreateIfPredicate(
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
            }

            // Match negated predicates
            return condition is UnaryArithmeticValue unary &&
                unary.Kind == UnaryArithmeticKind.Not
                ? CreateIfPredicate(
                    location,
                    unary.Value,
                    falseValue,
                    trueValue)
                : Append(new IfPredicate(
                    GetInitializer(location),
                    condition,
                    trueValue,
                    falseValue));
        }

        /// <summary>
        /// Creates a conditional switch predicate.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="condition">The condition.</param>
        /// <returns>A node that represents the predicate operation.</returns>
        public SwitchPredicate.Builder CreateSwitchPredicate(
            Location location,
            Value condition) =>
            CreateSwitchPredicate(location, condition, 4);

        /// <summary>
        /// Creates a conditional switch predicate.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="capacity">The initial case capacity.</param>
        /// <returns>A node that represents the predicate operation.</returns>
        public SwitchPredicate.Builder CreateSwitchPredicate(
            Location location,
            Value condition,
            int capacity)
        {
            location.Assert(condition.Type.BasicValueType.IsInt());
            condition = CreateConvert(
                location,
                condition,
                GetPrimitiveType(BasicValueType.Int32));
            return new SwitchPredicate.Builder(
                this,
                location,
                condition,
                capacity);
        }

        /// <summary>
        /// Creates a conditional switch predicate.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="values">The switch predicate values.</param>
        /// <returns>A node that represents the predicate operation.</returns>
        internal ValueReference CreateSwitchPredicate(
            Location location,
            ref ValueList values) =>
            values.Count == 3
            ? CreateIfPredicate(
                location,
                values[0],
                values[1],
                values[2])
            : new SwitchPredicate(
                GetInitializer(location),
                ref values);
    }
}
