// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: PaddingType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a padding type.
    /// </summary>
    public sealed class PaddingType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new padding type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="primitiveType">The primitive type to use for padding.</param>
        internal PaddingType(IRTypeContext typeContext, PrimitiveType primitiveType)
            : base(typeContext)
        {
            PrimitiveType = primitiveType;
            Size = PrimitiveType.Size;
            Alignment = PrimitiveType.Alignment;
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override bool IsPaddingType => true;

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        public new BasicValueType BasicValueType => PrimitiveType.BasicValueType;

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        public PrimitiveType PrimitiveType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the corresponding managed basic value type.
        /// </summary>
        protected override Type GetManagedType<TTypeProvider>(
            TTypeProvider typeProvider) =>
            typeProvider.GetPrimitiveType(PrimitiveType);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() =>
            BasicValueType.ToString();

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x2AB11613 ^ (int)BasicValueType;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is PaddingType paddingType &&
            paddingType.BasicValueType == BasicValueType;

        #endregion
    }
}
