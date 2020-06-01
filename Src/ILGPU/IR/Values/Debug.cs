// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents the kind of a debug operation.
    /// </summary>
    public enum DebugKind
    {
        /// <summary>
        /// A failed assertion.
        /// </summary>
        AssertFailed,

        /// <summary>
        /// A trace operation.
        /// </summary>
        Trace
    }

    /// <summary>
    /// Represents a generic debug operation.
    /// </summary>
    [ValueKind(ValueKind.Debug)]
    public sealed class DebugOperation : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="message">The debug message.</param>
        internal DebugOperation(
            in ValueInitializer initializer,
            DebugKind kind,
            ValueReference message)
            : base(initializer)
        {
            Kind = kind;
            Seal(message);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Debug;

        /// <summary>
        /// The debug operation kind.
        /// </summary>
        public DebugKind Kind { get; }

        /// <summary>
        /// Returns the message.
        /// </summary>
        public ValueReference Message => this[0];

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.VoidType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateDebug(
                Location,
                Kind,
                rebuilder.Rebuild(Message));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() =>
            "debug." + Kind.ToString().ToLower();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Message.ToString();

        #endregion
    }
}
