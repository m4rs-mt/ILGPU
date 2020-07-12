// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Values.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a null value for the given type.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The target type.</param>
        /// <returns>The null reference.</returns>
        public ValueReference CreateNull(Location location, TypeNode type) =>
            type is PrimitiveType primitiveType
            ? CreatePrimitiveValue(
                location,
                primitiveType.BasicValueType,
                0)
            : (ValueReference)Append(new NullValue(
                GetInitializer(location),
                type));

        /// <summary>
        /// Creates a new primitive <see cref="Enum"/> constant.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The object value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreateEnumValue(Location location, object value)
        {
            if (value == null)
                throw location.GetArgumentNullException(nameof(value));
            var type = value.GetType();
            if (!type.IsEnum)
            {
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedType,
                    type);
            }

            var baseType = type.GetEnumUnderlyingType();
            var baseValue = Convert.ChangeType(value, baseType);
            return CreatePrimitiveValue(location, baseValue);
        }

        /// <summary>
        /// Creates a new primitive constant.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The object value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreatePrimitiveValue(Location location, object value)
        {
            if (value == null)
                throw location.GetArgumentNullException(nameof(value));
            var type = value.GetType();
            return type.GetArithmeticBasicValueType() switch
            {
                ArithmeticBasicValueType.UInt1 =>
                    CreatePrimitiveValue(location, (bool)value),
                ArithmeticBasicValueType.Int8 =>
                    CreatePrimitiveValue(location, (sbyte)value),
                ArithmeticBasicValueType.Int16 =>
                    CreatePrimitiveValue(location, (short)value),
                ArithmeticBasicValueType.Int32 =>
                    CreatePrimitiveValue(location, (int)value),
                ArithmeticBasicValueType.Int64 =>
                    CreatePrimitiveValue(location, (long)value),
                ArithmeticBasicValueType.UInt8 =>
                    CreatePrimitiveValue(location, (byte)value),
                ArithmeticBasicValueType.UInt16 =>
                    CreatePrimitiveValue(location, (ushort)value),
                ArithmeticBasicValueType.UInt32 =>
                    CreatePrimitiveValue(location, (uint)value),
                ArithmeticBasicValueType.UInt64 =>
                    CreatePrimitiveValue(location, (ulong)value),
                ArithmeticBasicValueType.Float16 =>
                    CreatePrimitiveValue(location, (Half)value),
                ArithmeticBasicValueType.Float32 =>
                    CreatePrimitiveValue(location, (float)value),
                ArithmeticBasicValueType.Float64 =>
                    CreatePrimitiveValue(location, (double)value),
                _ => type == typeof(string)
                    ? CreatePrimitiveValue(location, (string)value)
                    : throw location.GetArgumentException(nameof(value))
            };
        }

        /// <summary>
        /// Creates a new string constant.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="string">The string value.</param>
        /// <returns>A reference to the requested value.</returns>
        public ValueReference CreatePrimitiveValue(
            Location location,
            string @string)
        {
            if (@string == null)
                throw location.GetArgumentNullException(nameof(@string));
            return Append(new StringValue(
                GetInitializer(location),
                @string));
        }

        /// <summary>
        /// Creates a primitive <see cref="bool"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(Location location, bool value) =>
            Append(new PrimitiveValue(
                GetInitializer(location),
                BasicValueType.Int1,
                value ? 1 : 0));

        /// <summary>
        /// Creates a primitive <see cref="sbyte"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        [CLSCompliant(false)]
        public PrimitiveValue CreatePrimitiveValue(Location location, sbyte value) =>
            Append(new PrimitiveValue(
                GetInitializer(location),
                BasicValueType.Int8,
                value));

        /// <summary>
        /// Creates a primitive <see cref="byte"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(Location location, byte value) =>
            CreatePrimitiveValue(location, (sbyte)value);

        /// <summary>
        /// Creates a primitive <see cref="short"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(Location location, short value) =>
            Append(new PrimitiveValue(
                GetInitializer(location),
                BasicValueType.Int16,
                value));

        /// <summary>
        /// Creates a primitive <see cref="ushort"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        [CLSCompliant(false)]
        public PrimitiveValue CreatePrimitiveValue(Location location, ushort value) =>
            CreatePrimitiveValue(location, (short)value);

        /// <summary>
        /// Creates a primitive <see cref="int"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(Location location, int value) =>
            Append(new PrimitiveValue(
                GetInitializer(location),
                BasicValueType.Int32,
                value));

        /// <summary>
        /// Creates a primitive <see cref="uint"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        [CLSCompliant(false)]
        public PrimitiveValue CreatePrimitiveValue(Location location, uint value) =>
            CreatePrimitiveValue(location, (int)value);

        /// <summary>
        /// Creates a primitive <see cref="long"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(Location location, long value) =>
            Append(new PrimitiveValue(
                GetInitializer(location),
                BasicValueType.Int64,
                value));

        /// <summary>
        /// Creates a primitive <see cref="ulong"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        [CLSCompliant(false)]
        public PrimitiveValue CreatePrimitiveValue(Location location, ulong value) =>
            CreatePrimitiveValue(location, (long)value);

        /// <summary>
        /// Creates a primitive <see cref="Half"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(Location location, Half value) =>
            Append(new PrimitiveValue(
                GetInitializer(location),
                BasicValueType.Float16,
                value.RawValue));

        /// <summary>
        /// Creates a primitive <see cref="float"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(Location location, float value) =>
            Append(new PrimitiveValue(
                GetInitializer(location),
                BasicValueType.Float32,
                Unsafe.As<float, int>(ref value)));

        /// <summary>
        /// Creates a primitive <see cref="double"/> value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(Location location, double value) =>
            Context.HasFlags(ContextFlags.Force32BitFloats)
            ? CreatePrimitiveValue(location, (float)value)
            : Append(new PrimitiveValue(
                GetInitializer(location),
                BasicValueType.Float64,
                Unsafe.As<double, long>(ref value)));

        /// <summary>
        /// Creates a primitive value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The value type.</param>
        /// <param name="rawValue">The raw value (sign-extended to long).</param>
        /// <returns>The created primitive value.</returns>
        public PrimitiveValue CreatePrimitiveValue(
            Location location,
            BasicValueType type,
            long rawValue) =>
            Append(new PrimitiveValue(
                GetInitializer(location),
                type,
                rawValue));

        /// <summary>
        /// Creates a generic value.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value.</param>
        /// <param name="type">The value type.</param>
        /// <returns>The created value.</returns>
        public ValueReference CreateValue(
            Location location,
            object value,
            Type type)
        {
            if (type == null)
                throw location.GetArgumentNullException(nameof(type));
            if (value != null && type != value.GetType())
                throw location.GetArgumentException(nameof(type));
            return type.GetArithmeticBasicValueType() switch
            {
                ArithmeticBasicValueType.UInt1 =>
                    CreatePrimitiveValue(location, Convert.ToBoolean(value)),
                ArithmeticBasicValueType.Int8 =>
                    CreatePrimitiveValue(location, Convert.ToSByte(value)),
                ArithmeticBasicValueType.UInt8 =>
                    CreatePrimitiveValue(location, Convert.ToByte(value)),
                ArithmeticBasicValueType.Int16 =>
                    CreatePrimitiveValue(location, Convert.ToInt16(value)),
                ArithmeticBasicValueType.UInt16 =>
                    CreatePrimitiveValue(location, Convert.ToUInt16(value)),
                ArithmeticBasicValueType.Int32 =>
                    CreatePrimitiveValue(location, Convert.ToInt32(value)),
                ArithmeticBasicValueType.UInt32 =>
                    CreatePrimitiveValue(location, Convert.ToUInt32(value)),
                ArithmeticBasicValueType.Int64 =>
                    CreatePrimitiveValue(location, Convert.ToInt64(value)),
                ArithmeticBasicValueType.UInt64 =>
                    CreatePrimitiveValue(location, Convert.ToUInt64(value)),
                ArithmeticBasicValueType.Float16 =>
                    CreatePrimitiveValue(location, (Half)value),
                ArithmeticBasicValueType.Float32 =>
                    CreatePrimitiveValue(location, Convert.ToSingle(value)),
                ArithmeticBasicValueType.Float64 =>
                    CreatePrimitiveValue(location, Convert.ToDouble(value)),
                _ => value == null
                    ? CreateNull(location, CreateType(type))
                    : CreateObjectValue(location, value),
            };
        }
    }
}
