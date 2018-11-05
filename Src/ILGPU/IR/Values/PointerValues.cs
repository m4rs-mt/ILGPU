// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
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

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract pointer value.
    /// </summary>
    public abstract class PointerValue : UnifiedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new pointer value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        internal PointerValue(ValueGeneration generation)
            : base(generation)
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
    public sealed class SubViewValue : PointerValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new sub-view computation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="source">The source view.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        internal SubViewValue(
            ValueGeneration generation,
            ValueReference source,
            ValueReference offset,
            ValueReference length)
            : base(generation)
        {
            Seal(ImmutableArray.Create(source, offset, length), source.Type);
        }

        #endregion

        #region Properties

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

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateSubViewValue(
                rebuilder.Rebuild(Source),
                rebuilder.Rebuild(Offset),
                rebuilder.Rebuild(Length));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

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
    public sealed class LoadElementAddress : PointerValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new address value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="sourceView">The source address.</param>
        /// <param name="elementIndex">The address of the referenced element.</param>
        /// <param name="loadPointerType">The pointer type of this operation.</param>
        internal LoadElementAddress(
            ValueGeneration generation,
            ValueReference sourceView,
            ValueReference elementIndex,
            PointerType loadPointerType)
            : base(generation)
        {
            Seal(
                ImmutableArray.Create(sourceView, elementIndex),
                loadPointerType);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated element index.
        /// </summary>
        public ValueReference ElementIndex => this[1];

        /// <summary>
        /// Returns true iff the current access works on a view.
        /// </summary>
        public bool IsViewAccess => !IsPointerAccess;

        /// <summary>
        /// Returns true iff the current access works on a pointer.
        /// </summary>
        public bool IsPointerAccess => Source.Type.IsPointerType;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateLoadElementAddress(
                rebuilder.Rebuild(Source),
                rebuilder.Rebuild(ElementIndex));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "lea.";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            if (IsViewAccess)
                return $"{Source}[{ElementIndex}]";
            return $"{Source} + {ElementIndex}";
        }

        #endregion
    }

    /// <summary>
    /// Loads a field address of an object pointer.
    /// </summary>
    public sealed class LoadFieldAddress : InstantiatedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new address value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="source">The source address.</param>
        /// <param name="fieldPointerType">The pointer type of the field.</param>
        /// <param name="fieldIndex">The structure field index.</param>
        internal LoadFieldAddress(
            ValueGeneration generation,
            ValueReference source,
            PointerType fieldPointerType,
            int fieldIndex)
            : base(generation)
        {
            FieldIndex = fieldIndex;
            Seal(ImmutableArray.Create(source), fieldPointerType);
        }

        #endregion

        #region Properties

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
        /// Returns the field index.
        /// </summary>
        public int FieldIndex { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateLoadFieldAddress(
                rebuilder.Rebuild(Source),
                FieldIndex);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "lfa.";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{Source} -> {StructureType.GetName(FieldIndex)} [{FieldIndex}]";

        #endregion
    }
}
