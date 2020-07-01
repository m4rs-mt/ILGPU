// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a compare operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="targetType">The target type.</param>
        /// <returns>A node that represents the convert operation.</returns>
        public ValueReference CreateConvert(
            Location location,
            Value node,
            TypeNode targetType) =>
            CreateConvert(
                location,
                node,
                targetType,
                ConvertFlags.None);

        /// <summary>
        /// Creates a compare operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="flags">Operation flags.</param>
        /// <returns>A node that represents the convert operation.</returns>
        public ValueReference CreateConvert(
            Location location,
            Value node,
            TypeNode targetType,
            ConvertFlags flags)
        {
            location.Assert(targetType.BasicValueType != BasicValueType.None);
            if (node.Type == targetType)
                return node;

            if (!(targetType is PrimitiveType targetPrimitiveType))
            {
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedConversion,
                    node.Type,
                    targetType);
            }

            bool isSourceUnsigned = (flags & ConvertFlags.SourceUnsigned) ==
                ConvertFlags.SourceUnsigned;
            bool isTargetUnsigned = (flags & ConvertFlags.TargetUnsigned) ==
                ConvertFlags.TargetUnsigned;

            // Match nested conversions
            if (node is ConvertValue convert)
            {
                var targetBasicType = targetPrimitiveType.BasicValueType;
                if (targetBasicType.IsInt() &&
                    convert.SourceType == targetBasicType.GetArithmeticBasicValueType(
                        isTargetUnsigned))
                {
                    return convert.Value;
                }

                var sourceBasicType = convert.BasicValueType;
                if (sourceBasicType.IsInt() &&
                    convert.Value.BasicValueType < sourceBasicType)
                {
                    ConvertFlags newFlags =
                        (convert.Flags & ~ConvertFlags.TargetUnsigned) |
                        flags & ~(ConvertFlags.SourceUnsigned |
                            ConvertFlags.OverflowSourceUnsigned);
                    return CreateConvert(
                        location,
                        convert.Value,
                        targetType,
                        newFlags);
                }
            }

            // Match X to bool
            if (targetPrimitiveType.IsBool)
            {
                return CreateCompare(
                    location,
                    node,
                    CreatePrimitiveValue(
                        location,
                        node.BasicValueType,
                        0),
                    CompareKind.NotEqual);
            }
            // Match bool to X
            else if (node.BasicValueType == BasicValueType.Int1)
            {
                return CreatePredicate(
                    location,
                    node,
                    CreatePrimitiveValue(
                        location,
                        targetPrimitiveType.BasicValueType,
                        1),
                    CreatePrimitiveValue(
                        location,
                        targetPrimitiveType.BasicValueType,
                        0));
            }


            // Match primitive types
            if (UseConstantPropagation && node is PrimitiveValue value)
            {
                var targetBasicValueType = targetType.BasicValueType;

                switch (value.BasicValueType)
                {
                    case BasicValueType.Int1:
                        return targetBasicValueType switch
                        {
                            BasicValueType.Float16 => CreatePrimitiveValue(
                                location,
                                value.Int1Value ? Half.One : Half.Zero),
                            BasicValueType.Float32 => CreatePrimitiveValue(
                                location,
                                Convert.ToSingle(value.Int1Value)),
                            BasicValueType.Float64 => CreatePrimitiveValue(
                                location,
                                Convert.ToDouble(value.Int1Value)),
                            _ => CreatePrimitiveValue(
                                location,
                                targetBasicValueType,
                                value.Int1Value ? 1 : 0),
                        };
                    case BasicValueType.Int8:
                    case BasicValueType.Int16:
                    case BasicValueType.Int32:
                    case BasicValueType.Int64:
                        switch (targetBasicValueType)
                        {
                            case BasicValueType.Float16:
                                return isSourceUnsigned
                                    ? CreatePrimitiveValue(
                                        location,
                                        (Half)value.UInt64Value)
                                    : (ValueReference)CreatePrimitiveValue(
                                        location,
                                        (Half)value.Int64Value);
                            case BasicValueType.Float32:
                                return isSourceUnsigned
                                    ? CreatePrimitiveValue(
                                        location,
                                        Convert.ToSingle(value.UInt64Value))
                                    : (ValueReference)CreatePrimitiveValue(
                                        location,
                                        Convert.ToSingle(value.Int64Value));
                            case BasicValueType.Float64:
                                return isSourceUnsigned
                                    ? CreatePrimitiveValue(
                                        location,
                                        Convert.ToDouble(value.UInt64Value))
                                    : (ValueReference)CreatePrimitiveValue(
                                        location,
                                        Convert.ToDouble(value.Int64Value));
                            default:
                                if (!isSourceUnsigned && !isTargetUnsigned)
                                {
                                    switch (value.BasicValueType)
                                    {
                                        case BasicValueType.Int8:
                                            return CreatePrimitiveValue(
                                                location,
                                                targetBasicValueType,
                                                value.Int8Value);
                                        case BasicValueType.Int16:
                                            return CreatePrimitiveValue(
                                                location,
                                                targetBasicValueType,
                                                value.Int16Value);
                                        case BasicValueType.Int32:
                                            return CreatePrimitiveValue(
                                                location,
                                                targetBasicValueType,
                                                value.Int32Value);
                                    }
                                }
                                return CreatePrimitiveValue(
                                    location,
                                    targetBasicValueType,
                                    value.RawValue);
                        }
                    case BasicValueType.Float16:
                        switch (targetBasicValueType)
                        {
                            case BasicValueType.Int1:
                                return CreatePrimitiveValue(
                                    location,
                                    targetBasicValueType,
                                    Half.IsZero(value.Float16Value) ? 0 : 1);
                            case BasicValueType.Int8:
                            case BasicValueType.Int16:
                            case BasicValueType.Int32:
                            case BasicValueType.Int64:
                                return isTargetUnsigned
                                    ? CreatePrimitiveValue(
                                        location,
                                        targetBasicValueType,
                                        (long)(ulong)value.Float16Value)
                                    : (ValueReference)CreatePrimitiveValue(
                                        location,
                                        targetBasicValueType,
                                        (long)value.Float16Value);
                            case BasicValueType.Float32:
                                return CreatePrimitiveValue(
                                    location,
                                    (float)value.Float16Value);
                            case BasicValueType.Float64:
                                return CreatePrimitiveValue(
                                    location,
                                    (double)value.Float16Value);
                        }
                        throw location.GetNotSupportedException(
                            ErrorMessages.NotSupportedConversion,
                            value.BasicValueType,
                            targetBasicValueType);
                    case BasicValueType.Float32:
                        switch (targetBasicValueType)
                        {
                            case BasicValueType.Int1:
                                return CreatePrimitiveValue(
                                    location,
                                    targetBasicValueType,
                                    value.Float32Value != 0.0f ? 1 : 0);
                            case BasicValueType.Int8:
                            case BasicValueType.Int16:
                            case BasicValueType.Int32:
                            case BasicValueType.Int64:
                                return isTargetUnsigned
                                    ? CreatePrimitiveValue(
                                        location,
                                        targetBasicValueType,
                                        (long)(ulong)value.Float32Value)
                                    : (ValueReference)CreatePrimitiveValue(
                                        location,
                                        targetBasicValueType,
                                        (long)value.Float32Value);
                            case BasicValueType.Float16:
                                return CreatePrimitiveValue(
                                    location,
                                    (Half)value.Float32Value);
                            case BasicValueType.Float64:
                                return CreatePrimitiveValue(
                                    location,
                                    (double)value.Float32Value);
                        }
                        throw location.GetNotSupportedException(
                            ErrorMessages.NotSupportedConversion,
                            value.BasicValueType,
                            targetBasicValueType);
                    case BasicValueType.Float64:
                        switch (targetBasicValueType)
                        {
                            case BasicValueType.Int1:
                            case BasicValueType.Int8:
                            case BasicValueType.Int16:
                            case BasicValueType.Int32:
                            case BasicValueType.Int64:
                                return isTargetUnsigned
                                    ? CreatePrimitiveValue(
                                        location,
                                        targetBasicValueType,
                                        (long)(ulong)value.Float64Value)
                                    : (ValueReference)CreatePrimitiveValue(
                                        location,
                                        targetBasicValueType,
                                        (long)value.Float64Value);
                            case BasicValueType.Float16:
                                return CreatePrimitiveValue(
                                    location,
                                    (Half)value.Float64Value);
                            case BasicValueType.Float32:
                                return CreatePrimitiveValue(
                                    location,
                                    (float)value.Float64Value);
                        }
                        throw location.GetNotSupportedException(
                            ErrorMessages.NotSupportedConversion,
                            value.BasicValueType,
                            targetBasicValueType);
                    default:
                        throw location.GetNotSupportedException(
                            ErrorMessages.NotSupportedConversion,
                            value.Type,
                            targetType);
                }
            }

            return Append(new ConvertValue(
                GetInitializer(location),
                node,
                targetType,
                flags));
        }
    }
}
