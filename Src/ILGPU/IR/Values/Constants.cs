// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Constants.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a constant value that will be instantiated.
    /// </summary>
    public abstract class InstantiatedConstantNode : InstantiatedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new constant value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="type">The constant type.</param>
        internal InstantiatedConstantNode(
            ValueGeneration generation,
            TypeNode type)
            : base(generation, true)
        {
            Seal(ImmutableArray<ValueReference>.Empty, type);
        }

        #endregion
    }

    /// <summary>
    /// Represents an immutable null value.
    /// </summary>
    public sealed class NullValue : InstantiatedConstantNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new object value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="type">The object type.</param>
        internal NullValue(
            ValueGeneration generation,
            TypeNode type)
            : base(generation, type)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateNull(
                rebuilder.Rebuild(Type));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

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
    public sealed class PrimitiveValue : InstantiatedConstantNode
    {
        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long rawValue;

        /// <summary>
        /// Constructs a new primitive constant.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="type">The primitive type.</param>
        /// <param name="value">The raw value.</param>
        internal PrimitiveValue(
            ValueGeneration generation,
            PrimitiveType type,
            long value)
            : base(generation, type)
        {
            rawValue = value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public new PrimitiveType Type => base.Type as PrimitiveType;

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
        /// Returns the value as f32.
        /// </summary>
        public float Float32Value => Unsafe.As<long, float>(ref rawValue);

        /// <summary>
        /// Returns the value as f64.
        /// </summary>
        public double Float64Value => Unsafe.As<long, double>(ref rawValue);

        /// <summary>
        /// Returns true iff the value is an integer.
        /// </summary>
        public bool IsInt => BasicValueType.IsInt();

        /// <summary>
        /// Returns true iff the value is a float.
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
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreatePrimitiveValue(
                BasicValueType,
                RawValue);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

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
            string result;
            switch (BasicValueType)
            {
                case BasicValueType.Int1:
                    result = Int1Value.ToString();
                    break;
                case BasicValueType.Int8:
                    result = Int8Value.ToString();
                    break;
                case BasicValueType.Int16:
                    result = Int16Value.ToString();
                    break;
                case BasicValueType.Int32:
                    result = Int32Value.ToString();
                    break;
                case BasicValueType.Int64:
                    result = Int64Value.ToString();
                    break;
                case BasicValueType.Float32:
                    result = Float32Value.ToString();
                    break;
                case BasicValueType.Float64:
                    result = Float64Value.ToString();
                    break;
                default:
                    result = $"Raw({rawValue})";
                    break;
            }
            return $"{result} [{BasicValueType}]";
        }

        #endregion
    }

    /// <summary>
    /// Represents an immutable string value.
    /// </summary>
    public sealed class StringValue : UnifiedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new string constant.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="stringType">The string type.</param>
        /// <param name="value">The string value.</param>
        internal StringValue(
            ValueGeneration generation,
            StringType stringType,
            string value)
            : base(generation, false)
        {
            String = value;

            Seal(ImmutableArray<ValueReference>.Empty, stringType);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public new StringType Type => base.Type as StringType;

        /// <summary>
        /// Returns the associated string constant.
        /// </summary>
        public string String { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreatePrimitiveValue(String);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals" />
        public override bool Equals(object obj)
        {
            if (obj is StringValue stringValue)
                return stringValue.String == String &&
                    base.Equals(obj);
            return false;
        }

        /// <summary cref="UnifiedValue.GetHashCode" />
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ String.GetHashCode();
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "const.str";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => String;

        #endregion
    }

    /// <summary>
    /// Represents the native size of a specific type.
    /// </summary>
    public sealed class SizeOfValue : InstantiatedConstantNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new sizeof value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="targetType">The target type of the size of computation.</param>
        /// <param name="type">The resulting int type.</param>
        internal SizeOfValue(
            ValueGeneration generation,
            TypeNode targetType,
            TypeNode type)
            : base(generation, type)
        {
            TargetType = targetType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target type.
        /// </summary>
        public TypeNode TargetType { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateSizeOf(
                rebuilder.Rebuild(TargetType));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "sizeof";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => TargetType.ToString();

        #endregion
    }

    /// <summary>
    /// Represents an undefined value of any type.
    /// </summary>
    public sealed class UndefValue : InstantiatedConstantNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new undefined value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="type">The type.</param>
        internal UndefValue(
            ValueGeneration generation,
            TypeNode type)
            : base(generation, type)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateUndef(
                rebuilder.Rebuild(Type));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "undef";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Type.ToString();

        #endregion
    }
}
