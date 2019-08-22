// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a generic debug operation.
    /// </summary>
    public abstract class DebugOperation : MemoryValue
    {
        #region Static

        /// <summary>
        /// Computes a debug node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(IRContext context) =>
            context.VoidType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new debug operation.
        /// </summary>
        /// <param name="kind">The value kind.</param>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="values">All child values.</param>
        protected DebugOperation(
            ValueKind kind,
            IRContext context,
            BasicBlock basicBlock,
            ImmutableArray<ValueReference> values)
            : base(
                  kind,
                  basicBlock,
                  values,
                  ComputeType(context))
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected sealed override TypeNode UpdateType(IRContext context) =>
            ComputeType(context);

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
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="message">The assertion message.</param>
        internal DebugAssertFailed(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference message)
            : base(
                  ValueKind.DebugAssertFailed,
                  context,
                  basicBlock,
                  ImmutableArray.Create(message))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the message.
        /// </summary>
        public ValueReference Message => this[0];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateDebugAssertFailed(
                rebuilder.Rebuild(Message));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

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
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="message">The assertion message.</param>
        internal DebugTrace(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference message)
            : base(
                  ValueKind.DebugTrace,
                  context,
                  basicBlock,
                  ImmutableArray.Create(message))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the message.
        /// </summary>
        public ValueReference Message => this[0];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateDebugTrace(
                rebuilder.Rebuild(Message));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "debug.trace";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Message.ToString();

        #endregion
    }
}
