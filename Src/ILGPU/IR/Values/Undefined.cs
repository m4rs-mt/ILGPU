// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Undefined.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an undefined value.
    /// </summary>
    public sealed class UndefinedValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a undefined value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="type">The phi type.</param>
        internal UndefinedValue(BasicBlock basicBlock, TypeNode type)
            : base(ValueKind.Undefined, basicBlock, type)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) => Type;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateUndefined(Type);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "undef";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => string.Empty;

        #endregion
    }
}
