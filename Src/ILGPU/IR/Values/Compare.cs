// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;

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
    [ValueKind(ValueKind.Compare)]
    public sealed class CompareValue : Value
    {
        #region Static

        /// <summary>
        /// A mapping to inverted compare kinds.
        /// </summary>
        private static readonly ImmutableArray<CompareKind> Inverted =
            ImmutableArray.Create(
                CompareKind.NotEqual,
                CompareKind.Equal,
                CompareKind.GreaterEqual,
                CompareKind.GreaterThan,
                CompareKind.LessEqual,
                CompareKind.LessThan);

        /// <summary>
        /// A mapping to swapped compare kinds to swap the operands.
        /// </summary>
        private static readonly ImmutableArray<CompareKind> Swapped =
            ImmutableArray.Create(
                CompareKind.Equal,
                CompareKind.NotEqual,
                CompareKind.GreaterThan,
                CompareKind.GreaterEqual,
                CompareKind.LessThan,
                CompareKind.LessEqual);

        /// <summary>
        /// A mapping to string representations.
        /// </summary>
        private static readonly ImmutableArray<string> StringOperations =
            ImmutableArray.Create(
                "==",
                "!=",
                "<",
                "<=",
                ">",
                ">=");

        /// <summary>
        /// Updates the compare flags according to the potentially updated operation
        /// kind.
        /// </summary>
        /// <param name="kind">The current operation kind.</param>
        /// <param name="newKind">The new (potentially updated) operation kind.</param>
        /// <param name="leftType">The left basic value type.</param>
        /// <param name="rightType">The right basic value type.</param>
        /// <param name="flags">The current flags to be updated.</param>
        /// <returns>The value of <paramref name="newKind"/>.</returns>
        private static CompareKind UpdateFlags(
            CompareKind kind,
            CompareKind newKind,
            BasicValueType leftType,
            BasicValueType rightType,
            ref CompareFlags flags)
        {
            // If the comparison was swapped or inverted, and we are comparing floats,
            // toggle between ordered/unordered float comparisons
            if (kind != newKind && leftType.IsFloat() && rightType.IsFloat())
                flags ^= CompareFlags.UnsignedOrUnordered;
            return newKind;
        }

        /// <summary>
        /// Inverts the given compare kind.
        /// </summary>
        /// <param name="kind">The compare kind to invert.</param>
        /// <param name="leftType">The basic value type of the left operand..</param>
        /// <param name="rightType">The basic value type of the right operand.</param>
        /// <param name="flags">The compare flags that might be adjusted.</param>
        /// <returns>The inverted compare kind.</returns>
        public static CompareKind Invert(
            CompareKind kind,
            BasicValueType leftType,
            BasicValueType rightType,
            ref CompareFlags flags) =>
            UpdateFlags(
                kind,
                Inverted[(int)kind],
                leftType,
                rightType,
                ref flags);

        /// <summary>
        /// Adjusts the given compare kind and the associated flags for swapping the
        /// operands of a compare operation.
        /// </summary>
        /// <param name="kind">The compare kind to invert.</param>
        /// <param name="leftType">The basic value type of the left operand..</param>
        /// <param name="rightType">The basic value type of the right operand.</param>
        /// <param name="flags">The compare flags that might be adjusted.</param>
        /// <returns>The adjusted compare kind.</returns>
        public static CompareKind SwapOperands(
            CompareKind kind,
            BasicValueType leftType,
            BasicValueType rightType,
            ref CompareFlags flags) =>
            UpdateFlags(
                kind,
                Swapped[(int)kind],
                leftType,
                rightType,
                ref flags);

        /// <summary>
        /// Returns true if the given kind is commutative.
        /// </summary>
        /// <param name="kind">The compare kind.</param>
        /// <returns>True, if the given kind is commutative.</returns>
        public static bool IsCommutative(CompareKind kind) =>
            kind <= CompareKind.NotEqual;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new compare value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal CompareValue(
            in ValueInitializer initializer,
            ValueReference left,
            ValueReference right,
            CompareKind kind,
            CompareFlags flags)
            : base(initializer)
        {
            Kind = kind;
            Flags = flags;

            Seal(left, right);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Compare;

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
        /// Returns true if the operation has enabled unsigned or unordered semantics.
        /// </summary>
        public bool IsUnsignedOrUnordered =>
            (Flags & CompareFlags.UnsignedOrUnordered) ==
            CompareFlags.UnsignedOrUnordered;

        /// <summary>
        /// Returns the comparison type.
        /// </summary>
        public ArithmeticBasicValueType CompareType =>
            Left.BasicValueType.GetArithmeticBasicValueType(IsUnsignedOrUnordered);

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.GetPrimitiveType(BasicValueType.Int1);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateCompare(
                Location,
                rebuilder.Rebuild(Left),
                rebuilder.Rebuild(Right),
                Kind,
                Flags);

        /// <summary cref="Value.Write(IRWriter)"/>
        protected internal override void Write(IRWriter writer)
        {
            writer.Write(Kind);
            writer.Write(Flags);
        }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "cmp";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var operation = StringOperations[(int)Kind];
            return $"{Left} {operation} {Right} [{Flags}]";
        }

        #endregion
    }
}
