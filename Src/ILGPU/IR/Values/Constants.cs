// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Constants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Serialization;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a constant value that will be instantiated.
    /// </summary>
    public abstract class ConstantNode : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new constant value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="constantType">The type of the constant node.</param>
        internal ConstantNode(
            in ValueInitializer initializer,
            TypeNode constantType)
            : base(initializer, constantType)
        {
            Seal();
        }

        #endregion
    }

    /// <summary>
    /// Represents an immutable null value.
    /// </summary>
    [ValueKind(ValueKind.Null)]
    public sealed class NullValue : ConstantNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new object value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="type">The object type.</param>
        internal NullValue(in ValueInitializer initializer, TypeNode type)
            : base(initializer, type)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Null;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateNull(Location, Type);

        /// <summary cref="Value.Write(IIRWriter)"/>
        protected internal override void Write(IIRWriter writer) { }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "null";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Type.ToString();

        #endregion
    }

    /// <summary>
    /// Represents a primitive value.
    /// </summary>
    [ValueKind(ValueKind.Primitive)]
    public sealed class PrimitiveValue : ConstantNode
    {
        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long rawValue;

        /// <summary>
        /// Constructs a new primitive constant.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="basicValueType">The basic value type.</param>
        /// <param name="value">The raw value.</param>
        internal PrimitiveValue(
            in ValueInitializer initializer,
            BasicValueType basicValueType,
            long value)
            : base(
                  initializer,
                  initializer.Context.GetPrimitiveType(basicValueType))
        {
            BasicValueType = basicValueType;
            rawValue = value;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Primitive;

        /// <summary>
        /// Returns the associated basic type.
        /// </summary>
        public new BasicValueType BasicValueType { get; }

        /// <summary>
        /// Returns the associated primitive type.
        /// </summary>
        public PrimitiveType PrimitiveType => Type.AsNotNullCast<PrimitiveType>();

        /// <summary>
        /// Returns the value as i1.
        /// </summary>
        public bool Int1Value => rawValue != 0;

        /// <summary>
        /// Returns the value as si8.
        /// </summary>
        public sbyte Int8Value => (sbyte)(rawValue & 0xff);

        /// <summary>
        /// Returns the value as si16.
        /// </summary>
        public short Int16Value => (short)(rawValue & 0xffff);

        /// <summary>
        /// Returns the value as si32.
        /// </summary>
        public int Int32Value => (int)(rawValue & 0xffffffff);

        /// <summary>
        /// Returns the value as si64.
        /// </summary>
        public long Int64Value => rawValue;

        /// <summary>
        /// Returns the value as u8.
        /// </summary>
        public byte UInt8Value => (byte)Int8Value;

        /// <summary>
        /// Returns the value as u16.
        /// </summary>
        public ushort UInt16Value => (ushort)Int16Value;

        /// <summary>
        /// Returns the value as u32.
        /// </summary>
        public uint UInt32Value => (uint)Int32Value;

        /// <summary>
        /// Returns the value as u64.
        /// </summary>
        public ulong UInt64Value => (ulong)Int64Value;

        /// <summary>
        /// Returns the value as f16.
        /// </summary>
        public Half Float16Value => Unsafe.As<long, Half>(ref rawValue);

        /// <summary>
        /// Returns the value as f32.
        /// </summary>
        public float Float32Value => Unsafe.As<long, float>(ref rawValue);

        /// <summary>
        /// Returns the value as f64.
        /// </summary>
        public double Float64Value => Unsafe.As<long, double>(ref rawValue);

        /// <summary>
        /// Returns true if the value is a bool.
        /// </summary>
        public bool IsBool => BasicValueType == BasicValueType.Int1;

        /// <summary>
        /// Returns true if the value is an integer.
        /// </summary>
        public bool IsInt => BasicValueType.IsInt();

        /// <summary>
        /// Returns true if the value is a float.
        /// </summary>
        public bool IsFloat => BasicValueType.IsFloat();

        /// <summary>
        /// Returns the underlying raw value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public long RawValue => rawValue;

        /// <summary>
        /// Returns true if this value represents the constant 0.
        /// </summary>
        public bool IsZero => HasValue(0, 0.0f, 0.0);

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if this constant represents the given raw integer value.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns>
        /// True, if this constant represents the given raw integer value.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasIntValue(long value) =>
            IsInt && RawValue == value;

        /// <summary>
        /// Returns true if this constant represents the given float values.
        /// </summary>
        /// <param name="f32Value">The 32-bit float value.</param>
        /// <param name="f64Value">The 64-bit float value.</param>
        /// <returns>True, if this constant the given float values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFloatValue(float f32Value, double f64Value) =>
            BasicValueType switch
            {
                BasicValueType.Float16 => Float16Value == f32Value,
                BasicValueType.Float32 => Float32Value == f32Value,
                BasicValueType.Float64 => Float64Value == f64Value,
                _ => false
            };

        /// <summary>
        /// Returns true if this constant represents one of the given values.
        /// </summary>
        /// <param name="value">The integer value.</param>
        /// <param name="f32Value">The 32-bit float value.</param>
        /// <param name="f64Value">The 64-bit float value.</param>
        /// <returns>True, if this constant represents on the given values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasValue(long value, float f32Value, double f64Value) =>
            HasIntValue(value) || HasFloatValue(f32Value, f64Value);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreatePrimitiveValue(
                Location,
                BasicValueType,
                rawValue);

        /// <summary cref="Value.Write(IIRWriter)"/>
        protected internal override void Write(IIRWriter writer)
        {
            writer.Write("BasicValueType", BasicValueType);
            writer.Write("RawValue", RawValue);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary>
        /// Returns the encapsulated value as string.
        /// </summary>
        /// <returns>The string representation of the encapsulated value.</returns>
        public string ToValueString() => ToArgString();

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "const";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            string result = BasicValueType switch
            {
                BasicValueType.Int1 => Int1Value.ToString(),
                BasicValueType.Int8 => Int8Value.ToString(),
                BasicValueType.Int16 => Int16Value.ToString(),
                BasicValueType.Int32 => Int32Value.ToString(),
                BasicValueType.Int64 => Int64Value.ToString(),
                BasicValueType.Float16 => Float16Value.ToString(),
                BasicValueType.Float32 => Float32Value.ToString(),
                BasicValueType.Float64 => Float64Value.ToString(),
                _ => $"Raw({rawValue})",
            };
            return $"{result} [{BasicValueType}]";
        }

        #endregion
    }

    /// <summary>
    /// Represents an immutable string value.
    /// </summary>
    [ValueKind(ValueKind.String)]
    public sealed class StringValue : ConstantNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new string constant.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The string value.</param>
        /// <param name="encoding">The string encoding.</param>
        internal StringValue(
            in ValueInitializer initializer,
            string value,
            Encoding encoding)
            : base(initializer, initializer.Context.StringType)
        {
            String = value;
            Encoding = encoding;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.String;

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public StringType StringType => Type.AsNotNullCast<StringType>();

        /// <summary>
        /// Returns the associated string constant.
        /// </summary>
        public string String { get; }

        /// <summary>
        /// Returns the associated encoding.
        /// </summary>
        public Encoding Encoding { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreatePrimitiveValue(Location, String);

        /// <summary cref="Value.Write(IIRWriter)"/>
        protected internal override void Write(IIRWriter writer)
        {
            writer.Write("Encoding", Encoding.CodePage);
            writer.Write("String", String);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
        protected override string ToPrefixString() =>
            "const.str." + Encoding.EncodingName.ToLowerInvariant();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => String;

        #endregion
    }
}
