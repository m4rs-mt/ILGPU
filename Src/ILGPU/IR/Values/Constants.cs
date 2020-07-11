﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Constants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
        public PrimitiveType PrimitiveType => Type as PrimitiveType;

        /// <summary>
        /// Returns the value as i1.
        /// </summary>
        public bool Int1Value => rawValue == 0 ? false : true;

        /// <summary>
        /// Returns the value as si8.
        /// </summary>
        [CLSCompliant(false)]
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
        [CLSCompliant(false)]
        public ushort UInt16Value => (ushort)Int16Value;

        /// <summary>
        /// Returns the value as u32.
        /// </summary>
        [CLSCompliant(false)]
        public uint UInt32Value => (uint)Int32Value;

        /// <summary>
        /// Returns the value as u64.
        /// </summary>
        [CLSCompliant(false)]
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

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreatePrimitiveValue(
                Location,
                BasicValueType,
                rawValue);

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
        internal StringValue(in ValueInitializer initializer, string value)
            : base(initializer, initializer.Context.StringType)
        {
            String = value;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.String;

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public StringType StringType => Type as StringType;

        /// <summary>
        /// Returns the associated string constant.
        /// </summary>
        public string String { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreatePrimitiveValue(Location, String);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "const.str";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => String;

        #endregion
    }
}
