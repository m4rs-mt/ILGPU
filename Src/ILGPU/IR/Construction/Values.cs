// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Values.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a null value for the given type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <returns>The null reference.</returns>
        public ValueReference CreateNull(TypeNode type)
        {
            Debug.Assert(type != null, "Invalid type node");

            if (type is PrimitiveType primitiveType)
                return CreatePrimitiveValue(primitiveType.BasicValueType, 0);
            return Append(new NullValue(
                BasicBlock,
                type));
        }

        /// <summary>
        /// Creates a new primitive enum constant.
        /// </summary>
        /// <param name="value">The object value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateEnumValue(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var type = value.GetType();
            if (!type.IsEnum)
                throw new ArgumentOutOfRangeException(nameof(value));
            var baseType = type.GetEnumUnderlyingType();
            var baseValue = Convert.ChangeType(value, baseType);
            return CreatePrimitiveValue(baseValue);
        }

        /// <summary>
        /// Creates a new primitive constant.
        /// </summary>
        /// <param name="value">The object value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreatePrimitiveValue(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Boolean:
                    return CreatePrimitiveValue((bool)value);
                case TypeCode.Int16:
                    return CreatePrimitiveValue((short)value);
                case TypeCode.Int32:
                    return CreatePrimitiveValue((int)value);
                case TypeCode.Int64:
                    return CreatePrimitiveValue((long)value);
                case TypeCode.UInt16:
                    return CreatePrimitiveValue((ushort)value);
                case TypeCode.UInt32:
                    return CreatePrimitiveValue((uint)value);
                case TypeCode.UInt64:
                    return CreatePrimitiveValue((ulong)value);
                case TypeCode.Single:
                    return CreatePrimitiveValue((float)value);
                case TypeCode.Double:
                    return CreatePrimitiveValue((double)value);
                case TypeCode.String:
                    return CreatePrimitiveValue((string)value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        /// <summary>
        /// Creates a new string constant.
        /// </summary>
        /// <param name="string">The string value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreatePrimitiveValue(string @string)
        {
            if (@string == null)
                throw new ArgumentNullException(nameof(@string));
            return Append(new StringValue(
                Context,
                BasicBlock,
                @string));
        }

        /// <summary>
        /// Creates a primitive <see cref="bool"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(bool value)
        {
            return Append(new PrimitiveValue(
                Context,
                BasicBlock,
                BasicValueType.Int1,
                value ? 1 : 0));
        }

        /// <summary>
        /// Creates a primitive <see cref="sbyte"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        [CLSCompliant(false)]
        public PrimitiveValue CreatePrimitiveValue(sbyte value)
        {
            return Append(new PrimitiveValue(
                Context,
                BasicBlock,
                BasicValueType.Int8,
                value));
        }

        /// <summary>
        /// Creates a primitive <see cref="byte"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(byte value) =>
            CreatePrimitiveValue((sbyte)value);

        /// <summary>
        /// Creates a primitive <see cref="short"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(short value)
        {
            return Append(new PrimitiveValue(
                Context,
                BasicBlock,
                BasicValueType.Int16,
                value));
        }

        /// <summary>
        /// Creates a primitive <see cref="ushort"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        [CLSCompliant(false)]
        public PrimitiveValue CreatePrimitiveValue(ushort value) =>
            CreatePrimitiveValue((short)value);

        /// <summary>
        /// Creates a primitive <see cref="int"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(int value)
        {
            return Append(new PrimitiveValue(
                Context,
                BasicBlock,
                BasicValueType.Int32,
                value));
        }

        /// <summary>
        /// Creates a primitive <see cref="uint"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        [CLSCompliant(false)]
        public PrimitiveValue CreatePrimitiveValue(uint value) =>
            CreatePrimitiveValue((int)value);

        /// <summary>
        /// Creates a primitive <see cref="long"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(long value)
        {
            return Append(new PrimitiveValue(
                Context,
                BasicBlock,
                BasicValueType.Int64,
                value));
        }

        /// <summary>
        /// Creates a primitive <see cref="ulong"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        [CLSCompliant(false)]
        public PrimitiveValue CreatePrimitiveValue(ulong value) =>
            CreatePrimitiveValue((long)value);

        /// <summary>
        /// Creates a primitive <see cref="float"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(float value)
        {
            return Append(new PrimitiveValue(
                Context,
                BasicBlock,
                BasicValueType.Float32,
                Unsafe.As<float, int>(ref value)));
        }

        /// <summary>
        /// Creates a primitive <see cref="double"/> value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(double value)
        {
            if (Context.HasFlags(ContextFlags.Force32BitFloats))
                return CreatePrimitiveValue((float)value);

            return Append(new PrimitiveValue(
                Context,
                BasicBlock,
                BasicValueType.Float64,
                Unsafe.As<double, long>(ref value)));
        }

        /// <summary>
        /// Creates a primitive value.
        /// </summary>
        /// <param name="type">The value type.</param>
        /// <param name="rawValue">The raw value (sign-extended to long).</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(BasicValueType type, long rawValue)
        {
            return Append(new PrimitiveValue(
                Context,
                BasicBlock,
                type,
                rawValue));
        }

        /// <summary>
        /// Creates a generic value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="type">The value type.</param>
        /// <returns>The created value.</returns>
        public ValueReference CreateValue(object value, Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (value != null && type != value.GetType())
                throw new ArgumentOutOfRangeException(nameof(type));
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return CreatePrimitiveValue(Convert.ToBoolean(value));
                case TypeCode.SByte:
                    return CreatePrimitiveValue(Convert.ToSByte(value));
                case TypeCode.Byte:
                    return CreatePrimitiveValue(Convert.ToByte(value));
                case TypeCode.Int16:
                    return CreatePrimitiveValue(Convert.ToInt16(value));
                case TypeCode.UInt16:
                    return CreatePrimitiveValue(Convert.ToUInt16(value));
                case TypeCode.Int32:
                    return CreatePrimitiveValue(Convert.ToInt32(value));
                case TypeCode.UInt32:
                    return CreatePrimitiveValue(Convert.ToUInt32(value));
                case TypeCode.Int64:
                    return CreatePrimitiveValue(Convert.ToInt64(value));
                case TypeCode.UInt64:
                    return CreatePrimitiveValue(Convert.ToUInt64(value));
                case TypeCode.Single:
                    return CreatePrimitiveValue(Convert.ToSingle(value));
                case TypeCode.Double:
                    return CreatePrimitiveValue(Convert.ToDouble(value));
                default:
                    return value == null ?
                        CreateNull(CreateType(type)) :
                        CreateObjectValue(value);
            }
        }
    }
}
