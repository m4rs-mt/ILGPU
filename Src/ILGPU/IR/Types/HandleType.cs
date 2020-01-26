// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: HandleType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a .Net runtime-specific handle type.
    /// </summary>
    public sealed class HandleType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new .Net runtime-specific handle type.
        /// </summary>
        internal HandleType() { }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary cref="TypeNode.TryResolveManagedType(out Type)"/>
        public override bool TryResolveManagedType(out Type type)
        {
            type = typeof(object);
            return true;
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "handle";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0xAA713C3;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is HandleType && base.Equals(obj);

        #endregion
    }
}
