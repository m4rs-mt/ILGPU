// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Cast.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract cast operation.
    /// </summary>
    public abstract class CastValue : UnifiedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type to convert the value to.</param>
        internal CastValue(
            ValueGeneration generation,
            ValueReference value,
            TypeNode targetType)
            : base(generation)
        {
            Seal(ImmutableArray.Create(value), targetType);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the operand.
        /// </summary>
        public ValueReference Value => this[0];

        /// <summary>
        /// Returns the source type to convert the value from.
        /// </summary>
        public TypeNode SourceType => Value.Type;

        /// <summary>
        /// Returns the target type to convert the value to.
        /// </summary>
        /// <remarks>This is equivalent to asking for the type.</remarks>
        public TypeNode TargetType => Type;

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is CastValue && base.Equals(obj);
        }

        /// <summary cref="UnifiedValue.GetHashCode"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x22A7C1D;
        }

        #endregion
    }

    /// <summary>
    /// Represents an abstract cast operation that works on address spaces.
    /// </summary>
    public abstract class BaseAddressSpaceCast : CastValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type to convert the value to.</param>
        internal BaseAddressSpaceCast(
            ValueGeneration generation,
            ValueReference value,
            AddressSpaceType targetType)
            : base(generation, value, targetType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public new AddressSpaceType Type => base.Type as AddressSpaceType;

        #endregion
    }

    /// <summary>
    /// Casts the type of a pointer to a different type.
    /// </summary>
    public sealed class PointerCast : BaseAddressSpaceCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new convert value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="pointerType">The target pointer type.</param>
        internal PointerCast(
            ValueGeneration generation,
            ValueReference value,
            PointerType pointerType)
            : base(generation, value, pointerType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the source element type.
        /// </summary>
        public TypeNode SourceElementType => (Value.Type as PointerType).ElementType;

        /// <summary>
        /// Returns the target element type.
        /// </summary>
        public TypeNode TargetElementType => Type.ElementType;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreatePointerCast(
                rebuilder.Rebuild(Value),
                rebuilder.Rebuild(TargetElementType));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is PointerCast && base.Equals(obj);
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x7B883A1;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "ptrcast";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{Value} -> {TargetType.ToString()}";

        #endregion
    }

    /// <summary>
    /// Cast a pointer from one address space to another.
    /// </summary>
    public sealed class AddressSpaceCast : BaseAddressSpaceCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new convert value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="addressSpaceType">The target address-space type.</param>
        internal AddressSpaceCast(
            ValueGeneration generation,
            ValueReference value,
            AddressSpaceType addressSpaceType)
            : base(generation, value, addressSpaceType)
        {
            Debug.Assert(
                value.Type.IsViewOrPointerType &&
                (value.Type as AddressSpaceType).AddressSpace != addressSpaceType.AddressSpace,
                "Invalid target address space");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target address space.
        /// </summary>
        public MemoryAddressSpace TargetAddressSpace => Type.AddressSpace;

        /// <summary>
        /// Returns true iff the current access works on a view.
        /// </summary>
        public bool IsViewCast => !IsPointerCast;

        /// <summary>
        /// Returns true iff the current access works on a pointer.
        /// </summary>
        public bool IsPointerCast => Type.IsPointerType;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateAddressSpaceCast(
                rebuilder.Rebuild(Value),
                TargetAddressSpace);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is AddressSpaceCast value)
                return value.TargetAddressSpace == TargetAddressSpace &&
                    base.Equals(obj);
            return false;
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x582A44C1;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "addrcast";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Value} -> {TargetAddressSpace}";

        #endregion
    }

    /// <summary>
    /// Casts a view from one element type to another.
    /// </summary>
    public sealed class ViewCast : BaseAddressSpaceCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="sourceView">The view to cast.</param>
        /// <param name="viewType">The target view type.</param>
        internal ViewCast(
            ValueGeneration generation,
            ValueReference sourceView,
            ViewType viewType)
            : base(generation, sourceView, viewType)
        {
            Debug.Assert(sourceView.Type.IsViewType, "Invalid view type");
            SourceElementType = (sourceView.Type as ViewType).ElementType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the source element type.
        /// </summary>
        public TypeNode SourceElementType { get; }

        /// <summary>
        /// Returns the target element type.
        /// </summary>
        public TypeNode TargetElementType => Type.ElementType;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateViewCast(
                rebuilder.Rebuild(Value),
                rebuilder.Rebuild(TargetElementType));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is ViewCast value)
                return value.TargetElementType == TargetElementType &&
                    base.Equals(obj);
            return false;
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x2AB330F7;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "vcast";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Value} -> {TargetElementType}";

        #endregion
    }

    /// <summary>
    /// Casts from one value type ot another while reinterpreting
    /// the value as another type.
    /// </summary>
    public abstract class BitCast : CastValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal BitCast(
            ValueGeneration generation,
            ValueReference source,
            PrimitiveType targetType)
            : base(generation, source, targetType)
        {
            Debug.Assert(source.Type.IsPrimitiveType, "Invalid primitive type");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target type to convert the value to.
        /// </summary>
        /// <remarks>This is equivalent to asking for the type.</remarks>
        public new PrimitiveType TargetType => Type as PrimitiveType;

        /// <summary>
        /// Returns true if this type represents a 32 bit type.
        /// </summary>
        public bool Is32Bit => TargetType.Is32Bit;

        /// <summary>
        /// Returns true if this type represents a 64 bit type.
        /// </summary>
        public bool Is64Bit => TargetType.Is64Bit;

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Value} as {TargetType}";

        #endregion
    }

    /// <summary>
    /// Casts from a float to an int while preserving bits.
    /// </summary>
    public sealed class FloatAsIntCast : BitCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal FloatAsIntCast(
            ValueGeneration generation,
            ValueReference source,
            PrimitiveType targetType)
            : base(generation, source, targetType)
        {
            var basicValueType = source.Type.BasicValueType;
            Debug.Assert(
                basicValueType == BasicValueType.Float32 ||
                basicValueType == BasicValueType.Float64, "Invalid primitive type");
            Debug.Assert(
                targetType.BasicValueType == BasicValueType.Int32 ||
                targetType.BasicValueType == BasicValueType.Int64, "Invalid primitive type");
        }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateFloatAsIntCast(
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is FloatAsIntCast && base.Equals(obj);
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x330F1AC3;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "fltasint";

        #endregion
    }

    /// <summary>
    /// Casts from an int to a float while preserving bits.
    /// </summary>
    public sealed class IntAsFloatCast : BitCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal IntAsFloatCast(
            ValueGeneration generation,
            ValueReference source,
            PrimitiveType targetType)
            : base(generation, source, targetType)
        {
            var basicValueType = source.Type.BasicValueType;
            Debug.Assert(
                basicValueType == BasicValueType.Int32 ||
                basicValueType == BasicValueType.Int64, "Invalid primitive type");
            Debug.Assert(
                targetType.BasicValueType == BasicValueType.Float32 ||
                targetType.BasicValueType == BasicValueType.Float64, "Invalid primitive type");
        }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateIntAsFloatCast(
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is IntAsFloatCast && base.Equals(obj);
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x1CC6D00F;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "intasflt";

        #endregion
    }
}
