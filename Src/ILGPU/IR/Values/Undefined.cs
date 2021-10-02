// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Undefined.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an undefined value.
    /// </summary>
    [ValueKind(ValueKind.Undefined)]
    public sealed class UndefinedValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a undefined value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal UndefinedValue(in ValueInitializer initializer)
            : base(
                  initializer,
                  ValueFlags.NotReplacable | ValueFlags.NoUses,
                  initializer.Context.VoidType)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Undefined;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateUndefined();

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
