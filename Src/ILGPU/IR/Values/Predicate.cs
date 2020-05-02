// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Predicate.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a conditional predicate.
    /// </summary>
    public abstract class Conditional : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new conditional node.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="arguments">The condition arguments.</param>
        internal Conditional(
            in ValueInitializer initializer,
            ValueReference condition,
            ImmutableArray<ValueReference> arguments)
            : base(initializer)
        {
            Debug.Assert(
                arguments.Length > 0,
                "Invalid condition arguments");
            Arguments = arguments;

            var builder = ImmutableArray.CreateBuilder<ValueReference>(
                arguments.Length + 1);
            builder.Add(condition);
            builder.AddRange(arguments);
            Seal(builder.MoveToImmutable());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated predicate value.
        /// </summary>
        public ValueReference Condition => this[0];

        /// <summary>
        /// Returns the arguments.
        /// </summary>
        public ImmutableArray<ValueReference> Arguments { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            Arguments[0].Type;

        #endregion
    }

    /// <summary>
    /// Represents a single if predicate.
    /// </summary>
    [ValueKind(ValueKind.Predicate)]
    public sealed class Predicate : Conditional
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
            : base(
                  initializer,
                  condition,
                  ImmutableArray.Create(trueValue, falseValue))
        {
            Debug.Assert(
                condition.Type.IsPrimitiveType &&
                condition.Type.BasicValueType == BasicValueType.Int1,
                "Invalid boolean predicate");
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Predicate;

        /// <summary>
        /// Returns the true value.
        /// </summary>
        public ValueReference TrueValue => this[1];

        /// <summary>
        /// Returns the false value.
        /// </summary>
        public ValueReference FalseValue => this[2];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreatePredicate(
                rebuilder.Rebuild(Condition),
                rebuilder.Rebuild(TrueValue),
                rebuilder.Rebuild(FalseValue));

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
