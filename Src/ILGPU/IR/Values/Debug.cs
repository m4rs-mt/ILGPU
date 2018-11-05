// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Immutable;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a generic debug operation.
    /// </summary>
    public abstract class DebugOperation : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug operation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="memoryRef">The parent memory value.</param>
        /// <param name="values">All child values.</param>
        /// <param name="type">The type of the value.</param>
        protected DebugOperation(
            ValueGeneration generation,
            ValueReference memoryRef,
            ImmutableArray<ValueReference> values,
            TypeNode type)
            : base(generation, memoryRef, values, type)
        { }

        #endregion
    }

    /// <summary>
    /// Represents a failed debug assertion.
    /// </summary>
    public sealed class DebugAssertFailed : DebugOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a failed debug assertion.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="memoryRef">The parent memory value.</param>
        /// <param name="message">The assertion message.</param>
        /// <param name="voidType">The void type.</param>
        internal DebugAssertFailed(
            ValueGeneration generation,
            ValueReference memoryRef,
            ValueReference message,
            TypeNode voidType)
            : base(generation, memoryRef, ImmutableArray.Create(message), voidType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the message.
        /// </summary>
        public ValueReference Message => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateDebugAssertFailed(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(Message));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "debug.fail";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Message.ToString();

        #endregion
    }

    /// <summary>
    /// Represents a debug trace event.
    /// </summary>
    public sealed class DebugTrace : DebugOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug trace.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="memoryRef">The parent memory value.</param>
        /// <param name="message">The assertion message.</param>
        /// <param name="voidType">The void type.</param>
        internal DebugTrace(
            ValueGeneration generation,
            ValueReference memoryRef,
            ValueReference message,
            TypeNode voidType)
            : base(generation, memoryRef, ImmutableArray.Create(message), voidType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the message.
        /// </summary>
        public ValueReference Message => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateDebugTrace(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(Message));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "debug.trace";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Message.ToString();

        #endregion
    }
}
