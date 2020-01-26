// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents the kind of a compare node.
    /// </summary>
    public enum CompareKind
    {
        /// <summary>
        /// An equal comparison.
        /// </summary>
        Equal,

        /// <summary>
        /// A not-equal comparison.
        /// </summary>
        NotEqual,

        /// <summary>
        /// A less-than comparison.
        /// </summary>
        LessThan,

        /// <summary>
        /// A less-equal comparison.
        /// </summary>
        LessEqual,

        /// <summary>
        /// A greater-than comparison.
        /// </summary>
        GreaterThan,

        /// <summary>
        /// A greater-equal comparison.
        /// </summary>
        GreaterEqual,
    }

    /// <summary>
    /// Represents special flags of a comparison.
    /// </summary>
    [Flags]
    public enum CompareFlags
    {
        /// <summary>
        /// No special flags.
        /// </summary>
        None,

        /// <summary>
        /// Specifies an unsigned (int) or an unordered
        /// (float) comparison.
        /// </summary>
        UnsignedOrUnordered
    }

    /// <summary>
    /// Represents a comparison.
    /// </summary>
    public sealed class CompareValue : Value
    {
        #region Static

        /// <summary>
        /// Computes a compare node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(IRContext context) =>
            context.GetPrimitiveType(BasicValueType.Int1);

        /// <summary>
        /// Inverts the given compare kind.
        /// </summary>
        /// <param name="kind">The compare kind to invert.</param>
        /// <returns>The inverted compare kind.</returns>
        public static CompareKind Invert(CompareKind kind)
        {
            switch (kind)
            {
                case CompareKind.Equal:
                    return CompareKind.NotEqual;
                case CompareKind.NotEqual:
                    return CompareKind.Equal;
                case CompareKind.LessThan:
                    return CompareKind.GreaterEqual;
                case CompareKind.LessEqual:
                    return CompareKind.GreaterThan;
                case CompareKind.GreaterThan:
                    return CompareKind.LessEqual;
                case CompareKind.GreaterEqual:
                    return CompareKind.LessThan;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>
        /// Returns true if the given kind is commutative.
        /// </summary>
        /// <param name="kind">The compare kind.</param>
        /// <returns>True, if the given kind is commutative.</returns>
        public static bool IsCommutative(CompareKind kind) =>
            kind <= CompareKind.NotEqual;

        /// <summary>
        /// Inverts the given compare kind if it is not commutative.
        /// </summary>
        /// <param name="kind">The compare kind to invert.</param>
        /// <returns>The inverted compare kind.</returns>
        public static CompareKind InvertIfNonCommutative(CompareKind kind)
        {
            if (IsCommutative(kind))
                return kind;
            return Invert(kind);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new compare value.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal CompareValue(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference left,
            ValueReference right,
            CompareKind kind,
            CompareFlags flags)
            : base(ValueKind.Compare, basicBlock, ComputeType(context))
        {
            Kind = kind;
            Flags = flags;

            Seal(ImmutableArray.Create(left, right));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the left operand.
        /// </summary>
        public ValueReference Left => this[0];

        /// <summary>
        /// Returns the right operand.
        /// </summary>
        public ValueReference Right => this[1];

        /// <summary>
        /// Returns the kind of this compare node.
        /// </summary>
        public CompareKind Kind { get; }

        /// <summary>
        /// Returns the associated flags.
        /// </summary>
        public CompareFlags Flags { get; }

        /// <summary>
        /// Returns true iff the operation has enabled unsigned or unordered semantics.
        /// </summary>
        public bool IsUnsignedOrUnordered => (Flags & CompareFlags.UnsignedOrUnordered) ==
            CompareFlags.UnsignedOrUnordered;

        /// <summary>
        /// Returns the comparison type.
        /// </summary>
        public ArithmeticBasicValueType CompareType =>
            Left.BasicValueType.GetArithmeticBasicValueType(IsUnsignedOrUnordered);

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateCompare(
                rebuilder.Rebuild(Left),
                rebuilder.Rebuild(Right),
                Kind,
                Flags);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "cmp";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var operation = "N/A";
            switch (Kind)
            {
                case CompareKind.Equal:
                    operation = "==";
                    break;
                case CompareKind.NotEqual:
                    operation = "!=";
                    break;
                case CompareKind.LessThan:
                    operation = "<";
                    break;
                case CompareKind.LessEqual:
                    operation = "<=";
                    break;
                case CompareKind.GreaterThan:
                    operation = ">";
                    break;
                case CompareKind.GreaterEqual:
                    operation = ">=";
                    break;
            }
            return $"{Left} {operation} {Right} [{Flags}]";
        }

        #endregion
    }
}
