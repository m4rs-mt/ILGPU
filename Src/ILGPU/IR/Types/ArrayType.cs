// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ArrayType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents an array type.
    /// </summary>
    public sealed class ArrayType : ObjectType, IAddressSpaceType
    {
        #region Instance

        /// <summary>
        /// Constructs a new array type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="elementType">The element type.</param>
        /// <param name="dimensions">The number of dimensions.</param>
        internal ArrayType(
            IRTypeContext typeContext,
            TypeNode elementType,
            int dimensions)
            : base(typeContext)
        {
            ElementType = elementType;
            Dimensions = dimensions;
            Size = Alignment = 4;
            AddFlags(elementType.Flags | TypeFlags.ArrayDependent);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the element type of the array.
        /// </summary>
        public TypeNode ElementType { get; }

        /// <summary>
        /// Returns the number of dimension.
        /// </summary>
        public int Dimensions { get; }

        /// <summary>
        /// Returns the associated address space.
        /// </summary>
        public MemoryAddressSpace AddressSpace => MemoryAddressSpace.Local;

        #endregion

        #region Methods

        /// <summary>
        /// Creates a managed array type.
        /// </summary>
        protected override Type GetManagedType() =>
            ElementType.ManagedType.MakeArrayType();

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "Array";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() => ElementType.GetHashCode() ^ Dimensions;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is ArrayType arrayType &&
            arrayType.ElementType == ElementType &&
            arrayType.Dimensions == Dimensions;

        /// <summary cref="TypeNode.ToString()"/>
        public override string ToString() =>
            ToPrefixString() + "<" + ElementType.ToString() +
            ", " + Dimensions + ">";

        #endregion
    }
}
