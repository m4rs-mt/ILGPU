// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PointerType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents an abstract type that relies on addresses.
    /// </summary>
    public abstract class AddressSpaceType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new address type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="addressSpace">The associated address space.</param>
        protected AddressSpaceType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
        {
            ElementType = elementType;
            AddressSpace = addressSpace;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying element type.
        /// </summary>
        public TypeNode ElementType { get; }

        /// <summary>
        /// Returns the associated address space.
        /// </summary>
        public MemoryAddressSpace AddressSpace { get; }

        #endregion

        #region Object

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ (int)AddressSpace;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is AddressSpaceType type &&
            type.AddressSpace == AddressSpace &&
            type.ElementType == ElementType &&
            base.Equals(obj);

        /// <summary cref="TypeNode.ToString"/>
        public override string ToString() =>
            ToPrefixString() + "<" + ElementType.ToString() + ", " +
            AddressSpace.ToString() + ">";

        #endregion
    }

    /// <summary>
    /// Represents the type of a generic pointer.
    /// </summary>
    public sealed class PointerType : AddressSpaceType
    {
        #region Instance

        /// <summary>
        /// Constructs a new pointer type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="addressSpace">The associated address space.</param>
        internal PointerType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
            : base(elementType, addressSpace)
        { }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary cref="TypeNode.Rebuild(IRBuilder, IRTypeRebuilder)"/>
        protected internal override TypeNode Rebuild(IRBuilder builder, IRTypeRebuilder rebuilder) =>
            builder.CreatePointerType(
                rebuilder.Rebuild(ElementType),
                AddressSpace);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "Ptr";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x2FE10E2A;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is PointerType && base.Equals(obj);

        #endregion
    }

    /// <summary>
    /// Represents the type of a generic view.
    /// </summary>
    public sealed class ViewType : AddressSpaceType
    {
        #region Instance

        /// <summary>
        /// Constructs a new view type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="addressSpace">The associated address space.</param>
        internal ViewType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
            : base(elementType, addressSpace)
        { }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary cref="TypeNode.Rebuild(IRBuilder, IRTypeRebuilder)"/>
        protected internal override TypeNode Rebuild(IRBuilder builder, IRTypeRebuilder rebuilder) =>
            builder.CreateViewType(
                rebuilder.Rebuild(ElementType),
                AddressSpace);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "View";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x11A34102;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is ViewType && base.Equals(obj);

        #endregion
    }
}
