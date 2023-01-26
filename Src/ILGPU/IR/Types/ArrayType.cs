// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents the type of a generic array that lives in the local address space.
    /// </summary>
    public sealed class ArrayType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new array type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="elementType">The element type.</param>
        /// <param name="numDimensions">The number of array dimensions.</param>
        internal ArrayType(
            IRTypeContext typeContext,
            TypeNode elementType,
            int numDimensions)
            : base(typeContext)
        {
            this.Assert(numDimensions > 0);
            ElementType = elementType;
            NumDimensions = numDimensions;

            Size = Alignment = 4;
            AddFlags(TypeFlags.ArrayDependent);
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override bool IsArrayType => true;

        /// <summary>
        /// Returns the underlying element type.
        /// </summary>
        public TypeNode ElementType { get; }

        /// <summary>
        /// Returns the number of array dimensions.
        /// </summary>
        public int NumDimensions { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a managed array type.
        /// </summary>
        internal Type GetDefaultManagedType<TTypeProvider>(TTypeProvider typeProvider)
            where TTypeProvider : IManagedTypeProvider =>
            ElementType.LoadManagedType(typeProvider).MakeArrayType(NumDimensions);

        /// <summary>
        /// Creates a managed array type.
        /// </summary>
        protected override Type GetManagedType<TTypeProvider>(
            TTypeProvider typeProvider) =>
            typeProvider.GetArrayType(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => $"Array{NumDimensions}D";

        /// <inheritdoc/>
        public override string ToString() => $"{ToPrefixString()}<{ElementType}>";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x3C11A78B ^ NumDimensions;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is ArrayType arrayType &&
            arrayType.ElementType == ElementType &&
            arrayType.NumDimensions == NumDimensions &&
            base.Equals(obj);

        #endregion
    }
}
