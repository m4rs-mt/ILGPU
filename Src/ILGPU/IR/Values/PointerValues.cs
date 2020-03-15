// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: PointerValue.cs
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
    /// Represents an abstract pointer value.
    /// </summary>
    public abstract class PointerValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new pointer value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        internal PointerValue(
            BasicBlock basicBlock,
            TypeNode initialType)
            : base(basicBlock, initialType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the source address.
        /// </summary>
        public ValueReference Source => this[0];

        #endregion
    }

    /// <summary>
    /// Represents a value to compute a sub-view value.
    /// </summary>
    [ValueKind(ValueKind.SubView)]
    public sealed class SubViewValue : PointerValue
    {
        #region Static

        /// <summary>
        /// Computes a sub-view value node type.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(
            ValueReference source) => source.Type;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new sub-view computation.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="source">The source view.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        internal SubViewValue(
            BasicBlock basicBlock,
            ValueReference source,
            ValueReference offset,
            ValueReference length)
            : base(basicBlock, ComputeType(source))
        {
            Seal(ImmutableArray.Create(source, offset, length));
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.SubView;

        /// <summary>
        /// Returns the base offset.
        /// </summary>
        public ValueReference Offset => this[1];

        /// <summary>
        /// Returns the length of the sub view.
        /// </summary>
        public ValueReference Length => this[2];

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(Source);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateSubViewValue(
                rebuilder.Rebuild(Source),
                rebuilder.Rebuild(Offset),
                rebuilder.Rebuild(Length));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "subv";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Source}[{Offset} - {Length}]";

        #endregion
    }

    /// <summary>
    /// Loads an element address of a view or a pointer.
    /// </summary>
    [ValueKind(ValueKind.LoadElementAddress)]
    public sealed class LoadElementAddress : PointerValue
    {
        #region Static

        /// <summary>
        /// Computes a lea value node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="source">The source value.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(IRContext context, ValueReference source)
        {
            var sourceType = source.Type as IAddressSpaceType;
            Debug.Assert(sourceType != null, "Invalid address space type");
            if (sourceType is PointerType)
                return source.Type;
            return context.CreatePointerType(
                sourceType.ElementType,
                sourceType.AddressSpace);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new address value.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="sourceView">The source address.</param>
        /// <param name="elementIndex">The address of the referenced element.</param>
        internal LoadElementAddress(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference sourceView,
            ValueReference elementIndex)
            : base(basicBlock, ComputeType(context, sourceView))
        {
            Seal(ImmutableArray.Create(sourceView, elementIndex));
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.LoadElementAddress;

        /// <summary>
        /// Returns the associated element index.
        /// </summary>
        public ValueReference ElementIndex => this[1];

        /// <summary>
        /// Returns true if the current access works on an array.
        /// </summary>
        public bool IsArrayAccesss => Source.Type.IsArrayType;

        /// <summary>
        /// Returns true if the current access works on a view.
        /// </summary>
        public bool IsViewAccess => Source.Type.IsViewType;

        /// <summary>
        /// Returns true if the current access works on a pointer.
        /// </summary>
        public bool IsPointerAccess => Source.Type.IsPointerType;

        /// <summary>
        /// Returns true if this access targets the first element.
        /// </summary>
        public bool AccessesFirstElement => ElementIndex.IsPrimitive(0);

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, Source);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateLoadElementAddress(
                rebuilder.Rebuild(Source),
                rebuilder.Rebuild(ElementIndex));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "lea.";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            return IsPointerAccess ?
                $"{Source} + {ElementIndex}" :
                $"{Source}[{ElementIndex}]";
        }

        #endregion
    }

    /// <summary>
    /// Loads a field address of an object pointer.
    /// </summary>
    [ValueKind(ValueKind.LoadFieldAddress)]
    public sealed class LoadFieldAddress : Value
    {
        #region Static

        /// <summary>
        /// Computes a lfa value node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="source">The source value.</param>
        /// <param name="fieldSpan">The structure field span.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(
            IRContext context,
            ValueReference source,
            FieldSpan fieldSpan)
        {
            var pointerType = source.Type as PointerType;
            Debug.Assert(pointerType != null, "Invalid pointer type");

            var structureType = pointerType.ElementType as StructureType;
            var fieldType = structureType.Get(context, fieldSpan);

            return context.CreatePointerType(
                fieldType,
                pointerType.AddressSpace);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new address value.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="source">The source address.</param>
        /// <param name="fieldSpan">The structure field span.</param>
        internal LoadFieldAddress(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference source,
            FieldSpan fieldSpan)
            : base(basicBlock, ComputeType(context, source, fieldSpan))
        {
            FieldSpan = fieldSpan;
            Seal(ImmutableArray.Create(source));
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.LoadFieldAddress;

        /// <summary>
        /// Returns the source address.
        /// </summary>
        public ValueReference Source => this[0];

        /// <summary>
        /// Returns the structure type.
        /// </summary>
        public StructureType StructureType =>
            (Source.Type as PointerType).ElementType as StructureType;

        /// <summary>
        /// Returns the managed field information.
        /// </summary>
        public TypeNode FieldType => (Type as PointerType).ElementType;

        /// <summary>
        /// Returns the field span.
        /// </summary>
        public FieldSpan FieldSpan { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, Source, FieldSpan);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateLoadFieldAddress(
                rebuilder.Rebuild(Source),
                FieldSpan);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "lfa.";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{Source} -> {FieldSpan}";

        #endregion
    }
}
