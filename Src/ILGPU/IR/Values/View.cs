// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: View.cs
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
    /// Represents a new view.
    /// </summary>
    [ValueKind(ValueKind.NewView)]
    public sealed class NewView : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a view.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="pointer">The underlying pointer.</param>
        /// <param name="length">The number of elements.</param>
        internal NewView(
            in ValueInitializer initializer,
            ValueReference pointer,
            ValueReference length)
            : base(initializer)
        {
            Seal(pointer, length);
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
        public TypeNode ViewElementType => Type.AsNotNullCast<ViewType>().ElementType;

        /// <summary>
        /// Returns the view's address space.
        /// </summary>
        public MemoryAddressSpace ViewAddressSpace =>
            Type.AsNotNullCast<ViewType>().AddressSpace;

        /// <summary>
        /// Returns the length of the view.
        /// </summary>
        public ValueReference Length => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer)
        {
            var type = Pointer.Type.As<PointerType>(Location);
            return initializer.Context.CreateViewType(
                type.ElementType,
                type.AddressSpace);
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateNewView(
                Location,
                rebuilder.Rebuild(Pointer),
                rebuilder.Rebuild(Length));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "newview";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"({Pointer}, {Length})";

        #endregion
    }

    /// <summary>
    /// Represents a generic operation of an <see cref="ArrayView{T}"/>.
    /// </summary>
    public abstract class ViewOperationValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a generic view operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal ViewOperationValue(in ValueInitializer initializer)
            : base(initializer)
        { }

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
    /// Represents a generic property of an <see cref="ArrayView{T}"/>.
    /// </summary>
    public abstract class ViewPropertyValue : ViewOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a view property.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="view">The underlying view.</param>
        internal ViewPropertyValue(
            in ValueInitializer initializer,
            ValueReference view)
            : base(initializer)
        {
            Seal(view);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if this is a 32bit element access.
        /// </summary>
        public bool Is32BitProperty => BasicValueType <= BasicValueType.Int32;

        /// <summary>
        /// Returns true if this is a 64bit element access.
        /// </summary>
        public bool Is64BitProperty => BasicValueType == BasicValueType.Int64;

        #endregion
    }

    /// <summary>
    /// Represents the <see cref="ArrayView{T}.Length"/> property inside the IR.
    /// </summary>
    [ValueKind(ValueKind.GetViewLength)]
    public sealed class GetViewLength : ViewPropertyValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new view length property.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="view">The underlying view.</param>
        /// <param name="lengthType">The underlying length type.</param>
        internal GetViewLength(
            in ValueInitializer initializer,
            ValueReference view,
            BasicValueType lengthType)
            : base(initializer, view)
        {
            LengthType = lengthType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated length type to return.
        /// </summary>
        public BasicValueType LengthType { get; }

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GetViewLength;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.GetPrimitiveType(LengthType);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGetViewLength(
                Location,
                rebuilder.Rebuild(View),
                LengthType);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "len";

        #endregion
    }
}
