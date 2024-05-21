// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Cast.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract cast operation.
    /// </summary>
    public abstract class CastValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The value to convert.</param>
        internal CastValue(
            in ValueInitializer initializer,
            ValueReference value)
            : base(initializer)
        {
            Seal(value);
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

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Value.ToString();

        #endregion
    }

    /// <summary>
    /// An abstract base class for converting pointers to integers and vice versa.
    /// </summary>
    public abstract class PointerIntCast : CastValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="source">The value to cast.</param>
        protected PointerIntCast(
            in ValueInitializer initializer,
            ValueReference source)
            : base(initializer, source)
        { }

        #endregion
    }

    /// <summary>
    /// Casts from an integer to a raw pointer value.
    /// </summary>
    [ValueKind(ValueKind.IntAsPointerCast)]
    public sealed class IntAsPointerCast : PointerIntCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="source">The view to cast.</param>
        internal IntAsPointerCast(
            in ValueInitializer initializer,
            ValueReference source)
            : base(initializer, source)
        {
            initializer.Assert(
                source.Type.BasicValueType.IsInt());
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.IntAsPointerCast;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer)
        {
            var context = initializer.Context;
            return context.CreatePointerType(
                context.VoidType,
                MemoryAddressSpace.Generic);
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateIntAsPointerCast(
                Location,
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer) { }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "intasptr";

        #endregion
    }

    /// <summary>
    /// Casts from a pointer value to an integer.
    /// </summary>
    [ValueKind(ValueKind.PointerAsIntCast)]
    public sealed class PointerAsIntCast : PointerIntCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="source">The pointer to cast.</param>
        /// <param name="targetType">The target int type to cast to.</param>
        internal PointerAsIntCast(
            in ValueInitializer initializer,
            ValueReference source,
            PrimitiveType targetType)
            : base(initializer, source)
        {
            initializer.Assert(
                source.Type.IsPointerType &&
                targetType.BasicValueType.IsInt());

            TargetType = targetType;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.PointerAsIntCast;

        /// <summary>
        /// Returns the target type to convert the value to.
        /// </summary>
        public new PrimitiveType TargetType { get; }

        /// <summary>
        /// Returns the target basic value type.
        /// </summary>
        public BasicValueType TargetBasicValueType => TargetType.BasicValueType;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            TargetType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreatePointerAsIntCast(
                Location,
                rebuilder.Rebuild(Value),
                TargetBasicValueType);

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer) { }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => $"ptras<{TargetType}>";

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
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The value to convert.</param>
        internal BaseAddressSpaceCast(
            in ValueInitializer initializer,
            ValueReference value)
            : base(initializer, value)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public new AddressSpaceType Type => base.Type.AsNotNullCast<AddressSpaceType>();

        /// <summary>
        /// Returns the source type.
        /// </summary>
        public new AddressSpaceType SourceType =>
            base.SourceType.AsNotNullCast<AddressSpaceType>();

        #endregion
    }

    /// <summary>
    /// Casts the type of a pointer to a different type.
    /// </summary>
    [ValueKind(ValueKind.PointerCast)]
    public sealed class PointerCast : BaseAddressSpaceCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new convert value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetElementType">The target element type.</param>
        internal PointerCast(
            in ValueInitializer initializer,
            ValueReference value,
            TypeNode targetElementType)
            : base(initializer, value)
        {
            TargetElementType = targetElementType;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.PointerCast;

        /// <summary>
        /// Returns the source element type.
        /// </summary>
        public TypeNode SourceElementType =>
            Value.Type.AsNotNullCast<PointerType>().ElementType;

        /// <summary>
        /// Returns the target element type.
        /// </summary>
        public TypeNode TargetElementType { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer)
        {
            var pointerType = SourceType.As<PointerType>(Location);
            return initializer.Context.CreatePointerType(
                TargetElementType,
                pointerType.AddressSpace);
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreatePointerCast(
                Location,
                rebuilder.Rebuild(Value),
                TargetElementType);

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer) { }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "ptrcast";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Value} -> {TargetType}";

        #endregion
    }

    /// <summary>
    /// Cast a pointer from one address space to another.
    /// </summary>
    [ValueKind(ValueKind.AddressSpaceCast)]
    public sealed class AddressSpaceCast : BaseAddressSpaceCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new convert value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetAddressSpace">The target address space.</param>
        internal AddressSpaceCast(
            in ValueInitializer initializer,
            ValueReference value,
            MemoryAddressSpace targetAddressSpace)
            : base(initializer, value)
        {
            TargetAddressSpace = targetAddressSpace;
            initializer.Assert(
                value.Type.IsViewOrPointerType &&
                value.Type.AsNotNullCast<AddressSpaceType>().AddressSpace !=
                targetAddressSpace);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.AddressSpaceCast;

        /// <summary>
        /// Returns the target address space.
        /// </summary>
        public MemoryAddressSpace TargetAddressSpace { get; }

        /// <summary>
        /// Returns true if the current access works on a view.
        /// </summary>
        public bool IsViewCast => !IsPointerCast;

        /// <summary>
        /// Returns true if the current access works on a pointer.
        /// </summary>
        public bool IsPointerCast => Type.IsPointerType;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer)
        {
            if (SourceType is ViewType viewType)
            {
                return initializer.Context.CreateViewType(
                    viewType.ElementType,
                    TargetAddressSpace);
            }
            else
            {
                var pointerType = SourceType.AsNotNullCast<PointerType>();
                return initializer.Context.CreatePointerType(
                    pointerType.ElementType,
                    TargetAddressSpace);
            }
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAddressSpaceCast(
                Location,
                rebuilder.Rebuild(Value),
                TargetAddressSpace);

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer) =>
            writer.Write(TargetAddressSpace);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "addrcast";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Value} -> {TargetAddressSpace}";

        #endregion
    }

    /// <summary>
    /// Casts a view from one element type to another.
    /// </summary>
    [ValueKind(ValueKind.ViewCast)]
    public sealed class ViewCast : BaseAddressSpaceCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="sourceView">The view to cast.</param>
        /// <param name="targetElementType">The target element type.</param>
        internal ViewCast(
            in ValueInitializer initializer,
            ValueReference sourceView,
            TypeNode targetElementType)
            : base(initializer, sourceView)
        {
            TargetElementType = targetElementType;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.ViewCast;

        /// <summary>
        /// Returns the source element type.
        /// </summary>
        public TypeNode SourceElementType =>
            SourceType.AsNotNullCast<ViewType>().ElementType;

        /// <summary>
        /// Returns the target element type.
        /// </summary>
        public TypeNode TargetElementType { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer)
        {
            var viewType = SourceType.As<ViewType>(Location);
            return initializer.Context.CreateViewType(
                TargetElementType,
                viewType.AddressSpace);
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateViewCast(
                Location,
                rebuilder.Rebuild(Value),
                TargetElementType);

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer) { }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "vcast";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Value} -> {TargetElementType}";

        #endregion
    }

    /// <summary>
    /// Casts an array to a linear array view.
    /// </summary>
    [ValueKind(ValueKind.ArrayToViewCast)]
    public sealed class ArrayToViewCast : CastValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="sourceArray">The source array to cast to a view.</param>
        internal ArrayToViewCast(
            in ValueInitializer initializer,
            ValueReference sourceArray)
            : base(initializer, sourceArray)
        {
            this.Assert(sourceArray.Type.IsArrayType);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.ArrayToViewCast;

        /// <summary>
        /// Returns the array type of the source value.
        /// </summary>
        public new ArrayType SourceType => base.SourceType.AsNotNullCast<ArrayType>();

        /// <summary>
        /// Returns the array element type.
        /// </summary>
        public TypeNode ElementType => SourceType.ElementType;

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.CreateViewType(
                ElementType,
                MemoryAddressSpace.Generic);

        /// <inheritdoc/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateArrayToViewCast(
                Location,
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer) { }

        /// <inheritdoc/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <inheritdoc/>
        protected override string ToPrefixString() => "arrayToView";

        /// <inheritdoc/>
        protected override string ToArgString() => $"{Value} -> View<{ElementType}>";

        #endregion
    }

    /// <summary>
    /// Casts from one value type to another while reinterpreting
    /// the value as another type.
    /// </summary>
    public abstract class BitCast : CastValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal BitCast(
            in ValueInitializer initializer,
            ValueReference source,
            PrimitiveType targetType)
            : base(initializer, source)
        {
            initializer.Assert(source.Type.IsPrimitiveType);
            TargetPrimitiveType = targetType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target type to convert the value to.
        /// </summary>
        public PrimitiveType TargetPrimitiveType { get; }

        /// <summary>
        /// Returns true if this type represents a 32 bit type.
        /// </summary>
        public bool Is32Bit => TargetPrimitiveType.Is32Bit;

        /// <summary>
        /// Returns true if this type represents a 64 bit type.
        /// </summary>
        public bool Is64Bit => TargetPrimitiveType.Is64Bit;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected sealed override TypeNode ComputeType(
            in ValueInitializer initializer) => TargetPrimitiveType;

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Value} as {TargetType}";

        #endregion
    }

    /// <summary>
    /// Casts from a float to an int while preserving bits.
    /// </summary>
    [ValueKind(ValueKind.FloatAsIntCast)]
    public sealed class FloatAsIntCast : BitCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal FloatAsIntCast(
            in ValueInitializer initializer,
            ValueReference source,
            PrimitiveType targetType)
            : base(
                  initializer,
                  source,
                  targetType)
        {
            var basicValueType = source.Type.BasicValueType;
            initializer.Assert(
                basicValueType == BasicValueType.Float16 ||
                basicValueType == BasicValueType.Float32 ||
                basicValueType == BasicValueType.Float64);
            initializer.Assert(
                targetType.BasicValueType == BasicValueType.Int16 ||
                targetType.BasicValueType == BasicValueType.Int32 ||
                targetType.BasicValueType == BasicValueType.Int64);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.FloatAsIntCast;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateFloatAsIntCast(
                Location,
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer) { }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "fltasint";

        #endregion
    }

    /// <summary>
    /// Casts from an int to a float while preserving bits.
    /// </summary>
    [ValueKind(ValueKind.IntAsFloatCast)]
    public sealed class IntAsFloatCast : BitCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal IntAsFloatCast(
            in ValueInitializer initializer,
            ValueReference source,
            PrimitiveType targetType)
            : base(
                  initializer,
                  source,
                  targetType)
        {
            var basicValueType = source.Type.BasicValueType;
            initializer.Assert(
                basicValueType == BasicValueType.Int16 ||
                basicValueType == BasicValueType.Int32 ||
                basicValueType == BasicValueType.Int64);
            initializer.Assert(
                targetType.BasicValueType == BasicValueType.Float16 ||
                targetType.BasicValueType == BasicValueType.Float32 ||
                targetType.BasicValueType == BasicValueType.Float64);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.IntAsFloatCast;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateIntAsFloatCast(
                Location,
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer) { }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "intasflt";

        #endregion
    }
}
