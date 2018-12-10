// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a compare operation.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <param name="targetType">The target type.</param>
        /// <returns>A node that represents the convert operation.</returns>
        public ValueReference CreateConvert(
            Value node,
            TypeNode targetType) =>
            CreateConvert(node, targetType, ConvertFlags.None);

        /// <summary>
        /// Creates a compare operation.
        /// </summary>
        /// <param name="node">The operand.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the convert operation.</returns>
        public ValueReference CreateConvert(
            Value node,
            TypeNode targetType,
            ConvertFlags flags)
        {
            Debug.Assert(node != null, "Invalid node");
            Debug.Assert(targetType.BasicValueType != BasicValueType.None, "Invalid node type");

            if (node.Type == targetType)
                return node;

            if (!(targetType is PrimitiveType targetPrimitiveType))
                throw new NotSupportedException("Not supported target type");

            bool isSourceUnsigned = (flags & ConvertFlags.SourceUnsigned) == ConvertFlags.SourceUnsigned;

            // Match nested conversions
            if (node is ConvertValue convert)
            {
                var basicValueType = targetPrimitiveType.BasicValueType;
                if (basicValueType.IsInt() &&
                    convert.SourceType == basicValueType.GetArithmeticBasicValueType(isSourceUnsigned))
                    return convert.Value;
            }

            // Match X to bool
            if (targetPrimitiveType.IsBool)
            {
                return CreateCompare(
                    node,
                    CreatePrimitiveValue(node.BasicValueType, 0),
                    CompareKind.NotEqual);
            }
            // Match bool to X
            else if (node.BasicValueType == BasicValueType.Int1)
            {
                return CreatePredicate(
                    node,
                    CreatePrimitiveValue(targetPrimitiveType.BasicValueType, 1),
                    CreatePrimitiveValue(targetPrimitiveType.BasicValueType, 0));
            }

            // Match primitive types
            if (UseConstantPropagation && node is PrimitiveValue value)
            {
                bool isTargetUnsigned = (flags & ConvertFlags.TargetUnsigned) == ConvertFlags.TargetUnsigned;
                var targetBasicValueType = targetType.BasicValueType;

                switch (value.BasicValueType)
                {
                    case BasicValueType.Int1:
                        switch (targetBasicValueType)
                        {
                            case BasicValueType.Float32:
                                return CreatePrimitiveValue(Convert.ToSingle(value.Int1Value));
                            case BasicValueType.Float64:
                                return CreatePrimitiveValue(Convert.ToDouble(value.Int1Value));
                            default:
                                return CreatePrimitiveValue(targetBasicValueType, value.Int1Value ? 1 : 0);
                        }
                    case BasicValueType.Int8:
                    case BasicValueType.Int16:
                    case BasicValueType.Int32:
                    case BasicValueType.Int64:
                        switch (targetBasicValueType)
                        {
                            case BasicValueType.Float32:
                                if (isSourceUnsigned)
                                    return CreatePrimitiveValue(Convert.ToSingle(value.UInt64Value));
                                else
                                    return CreatePrimitiveValue(Convert.ToSingle(value.Int64Value));
                            case BasicValueType.Float64:
                                if (isSourceUnsigned)
                                    return CreatePrimitiveValue(Convert.ToDouble(value.UInt64Value));
                                else
                                    return CreatePrimitiveValue(Convert.ToDouble(value.Int64Value));
                            default:
                                if (!isSourceUnsigned && !isTargetUnsigned)
                                {
                                    switch (value.BasicValueType)
                                    {
                                        case BasicValueType.Int8:
                                            return CreatePrimitiveValue(targetBasicValueType, value.Int8Value);
                                        case BasicValueType.Int16:
                                            return CreatePrimitiveValue(targetBasicValueType, value.Int16Value);
                                        case BasicValueType.Int32:
                                            return CreatePrimitiveValue(targetBasicValueType, value.Int32Value);
                                    }
                                }
                                return CreatePrimitiveValue(targetBasicValueType, value.RawValue);
                        }
                    case BasicValueType.Float32:
                        switch (targetBasicValueType)
                        {
                            case BasicValueType.Int1:
                                return CreatePrimitiveValue(targetBasicValueType, value.Float32Value != 0.0f ? 1 : 0);
                            case BasicValueType.Int8:
                            case BasicValueType.Int16:
                            case BasicValueType.Int32:
                            case BasicValueType.Int64:
                                if (isTargetUnsigned)
                                    return CreatePrimitiveValue(targetBasicValueType, (long)(ulong)value.Float32Value);
                                else
                                    return CreatePrimitiveValue(targetBasicValueType, (long)value.Float32Value);
                            case BasicValueType.Float64:
                                return CreatePrimitiveValue((double)value.Float32Value);
                        }
                        throw new NotSupportedException();
                    case BasicValueType.Float64:
                        switch (targetBasicValueType)
                        {
                            case BasicValueType.Int1:
                            case BasicValueType.Int8:
                            case BasicValueType.Int16:
                            case BasicValueType.Int32:
                            case BasicValueType.Int64:
                                if (isTargetUnsigned)
                                    return CreatePrimitiveValue(targetBasicValueType, (long)(ulong)value.Float64Value);
                                else
                                    return CreatePrimitiveValue(targetBasicValueType, (long)value.Float64Value);
                            case BasicValueType.Float32:
                                return CreatePrimitiveValue((float)value.Float64Value);
                        }
                        throw new NotSupportedException();
                    default:
                        throw new NotSupportedException();
                }
            }

            return Append(new ConvertValue(
                BasicBlock,
                node,
                targetType,
                flags));
        }
    }
}
