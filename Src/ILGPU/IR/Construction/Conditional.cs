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
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            if (condition is PrimitiveValue constant)
                return constant.Int1Value ? trueValue : falseValue;
            return CreateUnifiedValue(new Predicate(
                Generation,
                condition,
                trueValue,
                falseValue));
        }

        /// <summary>
        /// Creates a value selection node.
        /// </summary>
        /// <param name="value">The selection value.</param>
        /// <param name="nodes">A node enumerator that enumerates all arguments.</param>
        /// <returns>A node that represents the predicate operation.</returns>
        public ValueReference CreateSelectPredicate<TNodes>(
            Value value,
            TNodes nodes)
            where TNodes : IReadOnlyList<ValueReference>
        {
            Debug.Assert(value != null, "Invalid value node");
            Debug.Assert(value.BasicValueType.IsInt(), "Invalid value type");
            Debug.Assert(nodes.Count >= 1, "Invalid number of nodes");

            value = CreateConvert(value, CreatePrimitiveType(BasicValueType.Int32));

            // Transformation to create simple predicates
            if (nodes.Count == 2)
            {
                return CreatePredicate(
                    CreateCompare(value, CreatePrimitiveValue(0), CompareKind.Equal),
                    nodes[0],
                    nodes[1]);
            }

            var nodeCount = nodes.Count - 1;
            if (value is PrimitiveValue constant)
            {
                var index = constant.Int32Value;
                if (index < 0 || index >= nodeCount)
                    return nodes[nodeCount];
                return nodes[index];
            }

            var args = ImmutableArray.CreateBuilder<ValueReference>();
            TypeNode baseNodeType = null;
            foreach (var node in nodes)
            {
                if (baseNodeType == null)
                    baseNodeType = node.Type;
                else if (baseNodeType != node.Type)
                    throw new ArgumentException("Invalid node type of node " + node.ToString(), nameof(nodes));
                args.Add(node.Refresh());
            }

            return CreateUnifiedValue(new SelectPredicate(
                Generation,
                value,
                args.ToImmutable()));
        }

    }
}
