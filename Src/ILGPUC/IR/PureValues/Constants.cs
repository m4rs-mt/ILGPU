// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Constants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents a constant value that will be instantiated.
/// </summary>
abstract class ConstantNode : PureValue
{
    /// <summary>
    /// Constructs a new constant value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="constantType">The type of the constant node.</param>
    public ConstantNode(
        in PureValueInitializer initializer,
        TypeValue constantType)
        : base(initializer, constantType)
    {
        Seal();
    }
}

/// <summary>
/// Represents an immutable null value.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">The object type.</param>
sealed partial class NullValue(in PureValueInitializer initializer, TypeValue type) :
    ConstantNode(initializer, type)
{
    /// <inheritdoc cref="PureValue.Rewrite{TRewriter}(in TRewriter)"/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateNull(Location, rewriter.RewriteAs<TypeValue>(Type));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "null";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Type.ToString();
}

/// <summary>
/// Represents a box for a primitive value.
/// </summary>
/// <param name="RawValue">The underlying raw value.</param>
/// <param name="BasicValueType">The associated basic value type.</param>
readonly record struct PrimitiveValueBox(BasicValueType BasicValueType, long RawValue)
{
    /// <summary>
    /// Returns the value as i1.
    /// </summary>
    public bool Int1Value => RawValue != 0;

    /// <summary>
    /// Returns the value as si8.
    /// </summary>
    public sbyte Int8Value => (sbyte)(RawValue & 0xff);

    /// <summary>
    /// Returns the value as si16.
    /// </summary>
    public short Int16Value => (short)(RawValue & 0xffff);

    /// <summary>
    /// Returns the value as si32.
    /// </summary>
    public int Int32Value => (int)(RawValue & 0xffffffff);

    /// <summary>
    /// Returns the value as si64.
    /// </summary>
    public long Int64Value => RawValue;

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
    public Half Float16Value
    {
        get
        {
            long rawValue = RawValue;
            return Unsafe.As<long, Half>(ref rawValue);
        }
    }

    /// <summary>
    /// Returns the value as f32.
    /// </summary>
    public float Float32Value
    {
        get
        {
            long rawValue = RawValue;
            return Unsafe.As<long, float>(ref rawValue);
        }
    }

    /// <summary>
    /// Returns the value as f64.
    /// </summary>
    public double Float64Value
    {
        get
        {
            long rawValue = RawValue;
            return Unsafe.As<long, double>(ref rawValue);
        }
    }

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
    /// Returns true if this value represents the constant 0.
    /// </summary>
    public bool IsZero => HasValue(0, 0.0f, 0.0);

    /// <summary>
    /// Returns true if the given value is a primitive value with the specified raw
    /// value.
    /// </summary>
    /// <param name="rawValue">The expected raw value.</param>
    /// <returns>
    /// True, if the given value is a primitive value with the specified raw value.
    /// </returns>
    public bool IsPrimitiveValue(long rawValue) => rawValue == RawValue;

    /// <summary>
    /// Returns true if this constant represents the given raw integer value.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns>
    /// True, if this constant represents the given raw integer value.
    /// </returns>
    public bool HasIntValue(long value) => IsInt && RawValue == value;

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

    /// <summary>
    /// Returns the string representation of this box.
    /// </summary>
    public override string ToString()
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
            _ => $"Raw({RawValue})",
        };
        return $"{result} [{BasicValueType}]";
    }
}

/// <summary>
/// Represents a primitive value.
/// </summary>
sealed partial class PrimitiveValue : ConstantNode
{
    /// <summary>
    /// Constructs a new primitive constant.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="box">The underlying primitive value box.</param>
    internal PrimitiveValue(
        in PureValueInitializer initializer,
        in PrimitiveValueBox box)
        : base(
              initializer,
              initializer.ModuleBuilder.GetPrimitiveType(box.BasicValueType))
    {
        Box = box;
    }

    /// <summary>
    /// Returns the associated primitive type.
    /// </summary>
    public PrimitiveType PrimitiveType => GetTypeAs<PrimitiveType>();

    /// <summary>
    /// Returns the underlying primitive value box.
    /// </summary>
    public PrimitiveValueBox Box { get; }

    /// <summary>
    /// Returns the value as i1.
    /// </summary>
    public bool Int1Value => Box.Int1Value;

    /// <summary>
    /// Returns the value as si8.
    /// </summary>
    public sbyte Int8Value => Box.Int8Value;

    /// <summary>
    /// Returns the value as si16.
    /// </summary>
    public short Int16Value => Box.Int16Value;

    /// <summary>
    /// Returns the value as si32.
    /// </summary>
    public int Int32Value => Box.Int32Value;

    /// <summary>
    /// Returns the value as si64.
    /// </summary>
    public long Int64Value => Box.Int64Value;

    /// <summary>
    /// Returns the value as u8.
    /// </summary>
    public byte UInt8Value => Box.UInt8Value;

    /// <summary>
    /// Returns the value as u16.
    /// </summary>
    public ushort UInt16Value => Box.UInt16Value;

    /// <summary>
    /// Returns the value as u32.
    /// </summary>
    public uint UInt32Value => Box.UInt32Value;

    /// <summary>
    /// Returns the value as u64.
    /// </summary>
    public ulong UInt64Value => Box.UInt64Value;

    /// <summary>
    /// Returns the value as f16.
    /// </summary>
    public Half Float16Value => Box.Float16Value;

    /// <summary>
    /// Returns the value as f32.
    /// </summary>
    public float Float32Value => Box.Float32Value;

    /// <summary>
    /// Returns the value as f64.
    /// </summary>
    public double Float64Value => Box.Float64Value;

    /// <summary>
    /// Returns true if the value is a bool.
    /// </summary>
    public bool IsBool => Box.IsBool;

    /// <summary>
    /// Returns true if the value is an integer.
    /// </summary>
    public bool IsInt => Box.IsInt;

    /// <summary>
    /// Returns true if the value is a float.
    /// </summary>
    public bool IsFloat => Box.IsFloat;

    /// <summary>
    /// Returns the underlying raw value.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public long RawValue => Box.RawValue;

    /// <summary>
    /// Returns true if this value represents the constant 0.
    /// </summary>
    public bool IsZero => Box.IsZero;

    /// <summary>
    /// Returns true if the given value is a primitive value with the specified raw
    /// value.
    /// </summary>
    /// <param name="rawValue">The expected raw value.</param>
    /// <returns>
    /// True, if the given value is a primitive value with the specified raw value.
    /// </returns>
    public override bool IsPrimitiveValue(long rawValue) =>
        Box.IsPrimitiveValue(rawValue);

    /// <summary>
    /// Returns true if this constant represents the given raw integer value.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns>
    /// True, if this constant represents the given raw integer value.
    /// </returns>
    public bool HasIntValue(long value) => Box.HasIntValue(value);

    /// <summary>
    /// Returns true if this constant represents the given float values.
    /// </summary>
    /// <param name="f32Value">The 32-bit float value.</param>
    /// <param name="f64Value">The 64-bit float value.</param>
    /// <returns>True, if this constant the given float values.</returns>
    public bool HasFloatValue(float f32Value, double f64Value) =>
        Box.HasFloatValue(f32Value, f64Value);

    /// <summary>
    /// Returns true if this constant represents one of the given values.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <param name="f32Value">The 32-bit float value.</param>
    /// <param name="f64Value">The 64-bit float value.</param>
    /// <returns>True, if this constant represents on the given values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasValue(long value, float f32Value, double f64Value) =>
        Box.HasValue(value, f32Value, f64Value);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreatePrimitiveValue(Location, Box);

    /// <summary>
    /// Returns the encapsulated value as string.
    /// </summary>
    /// <returns>The string representation of the encapsulated value.</returns>
    public string ToValueString() => ToArgString();

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "const";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Box.ToString();
}

/// <summary>
/// Represents an immutable string value.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="value">The string value.</param>
/// <param name="encoding">The string encoding.</param>
sealed partial class StringValue(
    in PureValueInitializer initializer,
    string value,
    Encoding encoding) :
    ConstantNode(initializer, initializer.ModuleBuilder.StringType)
{
    /// <summary>
    /// Returns the associated type.
    /// </summary>
    public StringType StringType => GetTypeAs<StringType>();

    /// <summary>
    /// Returns the associated string constant.
    /// </summary>
    public string String { get; } = value;

    /// <summary>
    /// Returns the associated encoding.
    /// </summary>
    public Encoding Encoding { get; } = encoding;

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreatePrimitiveValue(Location, String, Encoding);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        "const.str." + Encoding.EncodingName.ToLowerInvariant();

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => String;
}
