// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
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
using System.Runtime.CompilerServices;

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
        /// <param name="kind">The value kind.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type to convert the value to.</param>
        internal CastValue(
            ValueKind kind,
            BasicBlock basicBlock,
            ValueReference value,
            TypeNode targetType)
            : base(kind, basicBlock, targetType)
        {
            Seal(ImmutableArray.Create(value));
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
        /// <param name="kind">The value kind.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type to convert the value to.</param>
        internal BaseAddressSpaceCast(
            ValueKind kind,
            BasicBlock basicBlock,
            ValueReference value,
            AddressSpaceType targetType)
            : base(kind, basicBlock, value, targetType)
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
        #region Static

        /// <summary>
        /// Computes a pointer cast node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="sourceType">The source pointer type.</param>
        /// <param name="targetElementType">The target pointer element type.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AddressSpaceType ComputeType(
            IRContext context,
            TypeNode sourceType,
            TypeNode targetElementType)
        {
            var pointerType = sourceType as PointerType;
            Debug.Assert(pointerType != null, "Invalid pointer type");
            return context.CreatePointerType(
                targetElementType,
                pointerType.AddressSpace);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new convert value.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetElementType">The target element type.</param>
        internal PointerCast(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference value,
            TypeNode targetElementType)
            : base(
                  ValueKind.PointerCast,
                  basicBlock,
                  value,
                  ComputeType(context, value.Type, targetElementType))
        {
            TargetElementType = targetElementType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the source element type.
        /// </summary>
        public TypeNode SourceElementType => (Value.Type as PointerType).ElementType;

        /// <summary>
        /// Returns the target element type.
        /// </summary>
        public TypeNode TargetElementType { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, SourceType, TargetElementType);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreatePointerCast(
                rebuilder.Rebuild(Value),
                TargetElementType);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

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
        #region Static

        /// <summary>
        /// Computes an address-space cast node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="sourceType">The source pointer type.</param>
        /// <param name="targetAddressSpace">The target address space.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AddressSpaceType ComputeType(
            IRContext context,
            TypeNode sourceType,
            MemoryAddressSpace targetAddressSpace)
        {
            if (sourceType is ViewType viewType)
            {
                return context.CreateViewType(
                    viewType.ElementType,
                    targetAddressSpace);
            }
            else
            {
                var pointerType = sourceType as PointerType;
                return context.CreatePointerType(
                    pointerType.ElementType,
                    targetAddressSpace);
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new convert value.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetAddressSpace">The target address space.</param>
        internal AddressSpaceCast(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference value,
            MemoryAddressSpace targetAddressSpace)
            : base(
                  ValueKind.AddressSpaceCast,
                  basicBlock,
                  value,
                  ComputeType(context, value.Type, targetAddressSpace))
        {
            Debug.Assert(
                value.Type.IsViewOrPointerType &&
                (value.Type as AddressSpaceType).AddressSpace != targetAddressSpace,
                "Invalid target address space");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target address space.
        /// </summary>
        public MemoryAddressSpace TargetAddressSpace { get; }

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

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, SourceType, TargetAddressSpace);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateAddressSpaceCast(
                rebuilder.Rebuild(Value),
                TargetAddressSpace);

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
    public sealed class ViewCast : BaseAddressSpaceCast
    {
        #region Static

        /// <summary>
        /// Computes a view cast node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="sourceType">The source pointer type.</param>
        /// <param name="targetElementType">The target pointer element type.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AddressSpaceType ComputeType(
            IRContext context,
            TypeNode sourceType,
            TypeNode targetElementType)
        {
            var viewType = sourceType as ViewType;
            Debug.Assert(viewType != null, "Invalid view type");
            return context.CreateViewType(
                targetElementType,
                viewType.AddressSpace);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="sourceView">The view to cast.</param>
        /// <param name="targetElementType">The target element type.</param>
        internal ViewCast(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference sourceView,
            TypeNode targetElementType)
            : base(
                  ValueKind.ViewCast,
                  basicBlock,
                  sourceView,
                  ComputeType(context, sourceView.Type, targetElementType))
        {
            TargetElementType = targetElementType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the source element type.
        /// </summary>
        public TypeNode SourceElementType => (SourceType as ViewType).ElementType;

        /// <summary>
        /// Returns the target element type.
        /// </summary>
        public TypeNode TargetElementType { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, SourceType, TargetElementType);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateViewCast(
                rebuilder.Rebuild(Value),
                TargetElementType);

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
    /// Casts from one value type ot another while reinterpreting
    /// the value as another type.
    /// </summary>
    public abstract class BitCast : CastValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="kind">The value kind.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal BitCast(
            ValueKind kind,
            BasicBlock basicBlock,
            ValueReference source,
            PrimitiveType targetType)
            : base(kind, basicBlock, source, targetType)
        {
            Debug.Assert(source.Type.IsPrimitiveType, "Invalid primitive type");
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

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected sealed override TypeNode UpdateType(IRContext context) => TargetPrimitiveType;

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
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal FloatAsIntCast(
            BasicBlock basicBlock,
            ValueReference source,
            PrimitiveType targetType)
            : base(
                  ValueKind.FloatAsIntCast,
                  basicBlock,
                  source,
                  targetType)
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
    public sealed class IntAsFloatCast : BitCast
    {
        #region Instance

        /// <summary>
        /// Constructs a new cast value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="source">The view to cast.</param>
        /// <param name="targetType">The primitive target type.</param>
        internal IntAsFloatCast(
            BasicBlock basicBlock,
            ValueReference source,
            PrimitiveType targetType)
            : base(
                  ValueKind.IntAsFloatCast,
                  basicBlock,
                  source,
                  targetType)
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

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "intasflt";

        #endregion
    }
}
