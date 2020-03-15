// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
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
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a new view.
    /// </summary>
    [ValueKind(ValueKind.NewView)]
    public sealed class NewView : Value
    {
        #region Static

        /// <summary>
        /// Computes a view node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="pointerType">The underlying pointer type.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(
            IRContext context,
            TypeNode pointerType)
        {
            var type = pointerType as PointerType;
            return context.CreateViewType(
                type.ElementType,
                type.AddressSpace);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a view.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="pointer">The underlying pointer.</param>
        /// <param name="length">The number of elements.</param>
        internal NewView(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference pointer,
            ValueReference length)
            : base(basicBlock, ComputeType(context, pointer.Type))
        {
            Debug.Assert(length.BasicValueType == BasicValueType.Int32, "Invalid length");
            Seal(ImmutableArray.Create(pointer, length));
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.NewView;

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

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, Pointer.Type);

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

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "newview";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"({Pointer}, {Length})";

        #endregion
    }

    /// <summary>
    /// Represents a generic property of an <see cref="ArrayView{T}"/>.
    /// </summary>
    public abstract class ViewPropertyValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a view property.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        /// <param name="view">The underlying view.</param>
        internal ViewPropertyValue(
            BasicBlock basicBlock,
            ValueReference view,
            TypeNode initialType)
            : base(basicBlock, initialType)
        {
            Seal(ImmutableArray.Create(view));
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
    [ValueKind(ValueKind.GetViewLength)]
    public sealed class GetViewLength : ViewPropertyValue
    {
        #region Static

        /// <summary>
        /// Computes a view length node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(IRContext context) =>
            context.GetPrimitiveType(BasicValueType.Int32);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new view length property.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicblock">The parent basic block.</param>
        /// <param name="view">The underlying view.</param>
        internal GetViewLength(
            IRContext context,
            BasicBlock basicblock,
            ValueReference view)
            : base(
                  basicblock,
                  view,
                  ComputeType(context))
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GetViewLength;

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context);

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
