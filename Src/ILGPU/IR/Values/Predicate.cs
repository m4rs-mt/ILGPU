// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Predicate.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Serialization;
using ILGPU.IR.Types;
using System;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a conditional predicate.
    /// </summary>
    [ValueKind(ValueKind.Predicate)]
    public sealed class Predicate : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new predicate.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="trueValue">The true value.</param>
        /// <param name="falseValue">The false value.</param>
        internal Predicate(
            in ValueInitializer initializer,
            ValueReference condition,
            ValueReference trueValue,
            ValueReference falseValue)
            : base(initializer)
        {
            Location.Assert(
                condition.Type.IsPrimitiveType &&
                condition.Type.BasicValueType == BasicValueType.Int1);
            Seal(condition, trueValue, falseValue);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Predicate;

        /// <summary>
        /// Returns the associated predicate value.
        /// </summary>
        public ValueReference Condition => this[0];

        /// <summary>
        /// Returns the true value.
        /// </summary>
        public ValueReference TrueValue => this[1];

        /// <summary>
        /// Returns the false value.
        /// </summary>
        public ValueReference FalseValue => this[2];

        /// <summary>
        /// Returns a span excluding the condition value reference.
        /// </summary>
        public ReadOnlySpan<ValueReference> Values => Nodes.Slice(1);

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            TrueValue.Type;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreatePredicate(
                Location,
                rebuilder.Rebuild(Condition),
                rebuilder.Rebuild(TrueValue),
                rebuilder.Rebuild(FalseValue));

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) { }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "pred";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{Condition} ? {TrueValue} : {FalseValue}";

        #endregion
    }
}
