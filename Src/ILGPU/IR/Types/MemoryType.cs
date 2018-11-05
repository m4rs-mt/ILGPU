// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: MemoryType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a memory type.
    /// </summary>
    public sealed class MemoryType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new memory type.
        /// </summary>
        internal MemoryType() { }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary cref="TypeNode.Rebuild(IRBuilder, IRTypeRebuilder)"/>
        protected internal override TypeNode Rebuild(IRBuilder builder, IRTypeRebuilder rebuilder) =>
            builder.MemoryType;

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "mem";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x50CF810A;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is MemoryType && base.Equals(obj);

        #endregion
    }
}
