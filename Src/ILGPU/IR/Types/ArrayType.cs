// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ArrayType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents an array type.
    /// </summary>
    public sealed class ArrayType : ObjectType
    {
        #region Instance

        /// <summary>
        /// Constructs a new array type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="length">The number of elements.</param>
        /// <param name="source">The original source type (or null).</param>
        internal ArrayType(TypeNode elementType, int length, Type source)
            : base(source)
        {
            ElementType = elementType;
            Length = length;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the element type of the array.
        /// </summary>
        public TypeNode ElementType { get; }

        /// <summary>
        /// Returns the array length.
        /// </summary>
        public int Length { get; }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "Array";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() => ElementType.GetHashCode() ^ Length;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is ArrayType arrayType &&
            arrayType.ElementType == ElementType &&
            arrayType.Length == Length;

        /// <summary cref="TypeNode.ToString()"/>
        public override string ToString() =>
            ToPrefixString() + "<" + ElementType.ToString() +
            ", " + Length + ">";

        #endregion
    }
}
