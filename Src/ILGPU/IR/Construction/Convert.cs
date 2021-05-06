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
        /// Creates a convert operation to a 32bit integer.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <returns>A node that represents the convert operation.</returns>
        public ValueReference CreateConvertToInt32(
            Location location,
            Value node) =>
            CreateConvert(location, node, BasicValueType.Int32);

        /// <summary>
        /// Creates a convert operation to a 64bit integer.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <returns>A node that represents the convert operation.</returns>
        public ValueReference CreateConvertToInt64(
            Location location,
            Value node) =>
            CreateConvert(location, node, BasicValueType.Int64);

        /// <summary>
        /// Creates a convert operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="node">The operand.</param>
        /// <param name="basicValueType">The target basic value type.</param>
        /// <returns>A node that represents the convert operation.</returns>
        public ValueReference CreateConvert(
            Location location,
            Value node,
            BasicValueType basicValueType) =>
            CreateConvert(
                location,
                node,
                GetPrimitiveType(basicValueType));

        /// <summary>
        /// Creates a convert operation.
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
        /// Creates a convert operation.
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
            // Check for identity conversions
            if (node.Type == targetType)
                return node;

            // Check for int to pointer casts
            if (targetType is PointerType pointerType &&
                pointerType.ElementType.IsVoidType)
            {
                return CreateIntAsPointerCast(
                    location,
                    node);
            }

            location.Assert(targetType.BasicValueType != BasicValueType.None);
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
                var canSimplify = false;
                var sourceBasicType = convert.BasicValueType;
                var targetBasicType = targetPrimitiveType.BasicValueType;

                if (sourceBasicType == targetBasicType)
                {
                    // The target conversion is the same basic type as the existing
                    // conversion. This indicates a sign change, because we have
                    // already handled identity conversions.
                    location.Assert(convert.TargetType !=
                        targetBasicType.GetArithmeticBasicValueType(isTargetUnsigned));
                    canSimplify = true;
                }
                else if (targetBasicType.IsInt() && sourceBasicType.IsInt())
                {
                    // The target conversion and existing conversion are integer types,
                    // but different sizes, attempt to consolidate them.
                    //
                    // If the existing conversion converts the inner type into a
                    // larger type, we can simplify this into a single conversion
                    // - it does not matter if the target conversion makes it larger,
                    // smaller or the same integer size.
                    //
                    // Otherwise, if the existing conversion converts the inner type
                    // into a smaller (and truncated) type, normally, we could not
                    // simplify this. However, if the target conversion is making the
                    // inner type even smaller, this can be simplified because we
                    // are truncating in one hit.
                    var innerBasicType = convert.Value.BasicValueType;
                    canSimplify = innerBasicType < sourceBasicType ||
                        innerBasicType > sourceBasicType &&
                        targetBasicType < sourceBasicType;
                }

                if (canSimplify)
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
            if (node is PrimitiveValue value)
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
