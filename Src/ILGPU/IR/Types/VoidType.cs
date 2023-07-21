// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VoidType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a void type.
    /// </summary>
    public sealed class VoidType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new void type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        internal VoidType(IRTypeContext typeContext)
            : base(typeContext)
        { }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override bool IsVoidType => true;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the void type.
        /// </summary>
        protected override Type GetManagedType() => typeof(void);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "void";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x3F671AC4;

        /// <summary cref="TypeNode.Equals(object?)"/>
        public override bool Equals(object? obj) =>
            obj is VoidType && base.Equals(obj);

        #endregion
    }
}
