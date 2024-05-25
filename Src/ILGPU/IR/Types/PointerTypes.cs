// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.IR.Serialization;
using System;
using System.Diagnostics;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents an abstract type that relies on addresses.
    /// </summary>
    public abstract class AddressSpaceType : TypeNode
    {
        #region Nested Types

        /// <summary>
        /// Converts the address space of <see cref="AddressSpaceType"/> instances.
        /// </summary>
        public sealed class AddressSpaceConverter : TypeConverter<AddressSpaceType>
        {
            /// <summary>
            /// Constructs a new address space converter.
            /// </summary>
            /// <param name="addressSpace">The target address space.</param>
            internal AddressSpaceConverter(MemoryAddressSpace addressSpace)
            {
                AddressSpace = addressSpace;
            }

            /// <summary>
            /// Returns the target address space to specialize.
            /// </summary>
            public MemoryAddressSpace AddressSpace { get; }

            /// <summary>
            /// Returns one field per address space type.
            /// </summary>
            protected override int GetNumFields(AddressSpaceType type) => 1;

            /// <summary>
            /// Converts a single <see cref="AddressSpaceType"/> into a specialized
            /// version using the target <see cref="AddressSpace"/>.
            /// </summary>
            protected override TypeNode ConvertType<TTypeContext>(
                TTypeContext typeContext,
                AddressSpaceType type) =>
                typeContext.SpecializeAddressSpaceType(
                    type,
                    AddressSpace);
        }

        #endregion

        #region Static

        /// <summary>
        /// Caches all known address space type converters.
        /// </summary>
        private static readonly AddressSpaceConverter[] TypeConverters =
        {
            new AddressSpaceConverter(MemoryAddressSpace.Generic),
            new AddressSpaceConverter(MemoryAddressSpace.Global),
            new AddressSpaceConverter(MemoryAddressSpace.Shared),
            new AddressSpaceConverter(MemoryAddressSpace.Local),
        };

        /// <summary>
        /// Returns a cached version of an <see cref="AddressSpaceConverter"/> for known
        /// address spaces.
        /// </summary>
        /// <param name="addressSpace">The address space to convert into.</param>
        /// <returns>A cached or a new converter instance.</returns>
        public static AddressSpaceConverter GetAddressSpaceConverter(
            MemoryAddressSpace addressSpace)
        {
            int index = (int)addressSpace;
            return index < 0 || index >= TypeConverters.Length
                ? new AddressSpaceConverter(addressSpace)
                : TypeConverters[index];
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new address type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="elementType">The element type.</param>
        /// <param name="addressSpace">The associated address space.</param>
        protected AddressSpaceType(
            IRTypeContext typeContext,
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
            : base(typeContext)
        {
            Debug.Assert(elementType != null, "Invalid element type");
            ElementType = elementType;
            AddressSpace = addressSpace;
            AddFlags(elementType.Flags);
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

        #region Methods

        /// <summary cref="TypeNode.Write(IIRWriter)"/>
        protected internal override void Write(IIRWriter writer)
        {
            writer.Write("ElementType", ElementType.Id);
            writer.Write("AddressSpace", AddressSpace);
        }

        #endregion

        #region Object

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ (int)AddressSpace;

        /// <summary cref="TypeNode.Equals(object?)"/>
        public override bool Equals(object? obj) =>
            obj is AddressSpaceType type &&
            type.AddressSpace == AddressSpace &&
            type.ElementType == ElementType &&
            base.Equals(obj);

        /// <inheritdoc/>
        public override string ToString() =>
            $"{ToPrefixString()}<{ElementType}, {AddressSpace}>";

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
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="elementType">The element type.</param>
        /// <param name="addressSpace">The associated address space.</param>
        internal PointerType(
            IRTypeContext typeContext,
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
            : base(typeContext, elementType, addressSpace)
        {
            if (typeContext.TargetPlatform.Is64Bit())
            {
                Size = Alignment = 8;
                BasicValueType = BasicValueType.Int64;
            }
            else
            {
                Size = Alignment = 4;
                BasicValueType = BasicValueType.Int32;
            }
            AddFlags(TypeFlags.PointerDependent);
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override bool IsPointerType => true;

        /// <inheritdoc/>
        public override TypeKind TypeKind => TypeKind.Pointer;

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        public override BasicValueType BasicValueType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a default managed view type.
        /// </summary>
        internal Type GetDefaultManagedPointerType<TTypeProvider>(
            TTypeProvider typeProvider)
            where TTypeProvider : IManagedTypeProvider =>
            ElementType.LoadManagedType(typeProvider).MakePointerType();

        /// <summary>
        /// Creates a managed pointer type.
        /// </summary>
        protected override Type GetManagedType<TTypeProvider>(
            TTypeProvider typeProvider) =>
            typeProvider.GetPointerType(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "Ptr";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x2FE10E2A;

        /// <summary cref="TypeNode.Equals(object?)"/>
        public override bool Equals(object? obj) =>
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
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="elementType">The element type.</param>
        /// <param name="addressSpace">The associated address space.</param>
        internal ViewType(
            IRTypeContext typeContext,
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
            : base(typeContext, elementType, addressSpace)
        {
            Size = Alignment = 4;
            AddFlags(TypeFlags.ViewDependent);
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override bool IsViewType => true;

        /// <inheritdoc/>
        public override TypeKind TypeKind => TypeKind.View;

        #endregion

        #region Methods

        /// <summary>
        /// Creates a default managed view type.
        /// </summary>
        internal Type GetDefaultManagedViewType<TTypeProvider>(
            TTypeProvider typeProvider)
            where TTypeProvider : IManagedTypeProvider =>
            typeof(ArrayView<>).MakeGenericType(
                ElementType.LoadManagedType(typeProvider));

        /// <summary>
        /// Creates a managed view type.
        /// </summary>
        protected override Type GetManagedType<TTypeProvider>(
            TTypeProvider typeProvider) =>
            typeProvider.GetViewType(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "View";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x11A34102;

        /// <summary cref="TypeNode.Equals(object?)"/>
        public override bool Equals(object? obj) =>
            obj is ViewType && base.Equals(obj);

        #endregion
    }
}
