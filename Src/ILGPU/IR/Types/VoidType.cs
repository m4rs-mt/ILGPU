// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: VoidType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;

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
        internal VoidType() { }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "void";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x3F671AC4;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is VoidType && base.Equals(obj);

        #endregion
    }
}
