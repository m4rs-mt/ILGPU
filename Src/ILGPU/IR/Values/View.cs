// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: View.cs
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
    /// Represents a new view.
    /// </summary>
    public sealed class NewView : UnifiedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a view.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="pointer">The underlying pointer.</param>
        /// <param name="length">The number of elements.</param>
        /// <param name="viewType">The view type.</param>
        internal NewView(
            ValueGeneration generation,
            ValueReference pointer,
            ValueReference length,
            ViewType viewType)
            : base(generation)
        {
            Debug.Assert(length.BasicValueType == BasicValueType.Int32, "Invalid length");
            Seal(ImmutableArray.Create(pointer, length), viewType);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying pointer.
        /// </summary>
        public ValueReference Pointer => this[0];

        /// <summary>
        /// Returns the view's element type.
        /// </summary>
        public TypeNode ViewElementType => (Type as ViewType).ElementType;

        /// <summary>
        /// Returns the view's address space.
        /// </summary>
        public MemoryAddressSpace ViewAddressSpace => (Type as ViewType).AddressSpace;

        /// <summary>
        /// Returns the length of the view.
        /// </summary>
        public ValueReference Length => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateNewView(
                rebuilder.Rebuild(Pointer),
                rebuilder.Rebuild(Length));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0xCF89A1D;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "newview";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"({Pointer}, {Length})";

        #endregion
    }

    /// <summary>
    /// Represents a generic property of an <see cref="ArrayView{T}"/>.
    /// </summary>
    public abstract class ViewPropertyValue : UnifiedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a view property.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="view">The underlying view.</param>
        /// <param name="type">The underlying type.</param>
        internal ViewPropertyValue(
            ValueGeneration generation,
            ValueReference view,
            TypeNode type)
            : base(generation)
        {
            Seal(ImmutableArray.Create(view), type);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying view.
        /// </summary>
        public ValueReference View => this[0];

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => View.ToString();

        #endregion
    }

    /// <summary>
    /// Represents the <see cref="ArrayView{T}.Length"/> property
    /// inside the IR.
    /// </summary>
    public sealed class GetViewLength : ViewPropertyValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new view length property.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="view">The underlying view.</param>
        /// <param name="intType">The default integer type.</param>
        internal GetViewLength(
            ValueGeneration generation,
            ValueReference view,
            PrimitiveType intType)
            : base(generation, view, intType)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGetViewLength(
                rebuilder.Rebuild(View));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "len";

        #endregion
    }
}
