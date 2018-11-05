// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Predicate.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a conditional predicate.
    /// </summary>
    public abstract class Conditional : UnifiedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new conditional node.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="arguments">The condition arguments.</param>
        internal Conditional(
            ValueGeneration generation,
            ValueReference condition,
            ImmutableArray<ValueReference> arguments)
            : base(generation)
        {
            Debug.Assert(
                arguments.Length > 0,
                "Invalid condition arguments");
            Arguments = arguments;
            var builder = ImmutableArray.CreateBuilder<ValueReference>(arguments.Length + 1);
            builder.Add(condition);
            builder.AddRange(arguments);
            Seal(builder.MoveToImmutable(), arguments[0].Type);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated predicate value.
        /// </summary>
        public ValueReference Condition => this[0];

        /// <summary>
        /// Returns true if the current value is higher order.
        /// </summary>
        public bool IsHigherOrder =>
            Type != null ? Type.IsFunctionType : false;

        /// <summary>
        /// Returns the branches.
        /// </summary>
        public ImmutableArray<ValueReference> Arguments { get; }

        /// <summary cref="Value.Type"/>
        public override TypeNode Type => Arguments[0].Type;

        #endregion
    }

    /// <summary>
    /// Represents a single if predicate.
    /// </summary>
    public sealed class Predicate : Conditional
    {
        #region Instance

        /// <summary>
        /// Constructs a new predicate.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="trueValue">The true value.</param>
        /// <param name="falseValue">The false value.</param>
        internal Predicate(
            ValueGeneration generation,
            ValueReference condition,
            ValueReference trueValue,
            ValueReference falseValue)
            : base(generation, condition, ImmutableArray.Create(trueValue, falseValue))
        {
            Debug.Assert(
                condition.Type.IsPrimitiveType &&
                condition.Type.BasicValueType == BasicValueType.Int1,
                "Invalid boolean predicate");
        }

        #endregion

        #region Properties

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
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreatePredicate(
                rebuilder.Rebuild(Condition),
                rebuilder.Rebuild(TrueValue),
                rebuilder.Rebuild(FalseValue));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "pred";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Condition} ? {TrueValue} : {FalseValue}";

        #endregion
    }

    /// <summary>
    /// Represents a value selection node. It selects an appropriate value
    /// from an array of values. If the value is out range the default value
    /// is returned.
    /// </summary>
    public sealed class SelectPredicate : Conditional
    {
        #region Instance

        /// <summary>
        /// Constructs a new predicate.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="value">The selection value.</param>
        /// <param name="arguments">The selection arguments. The last argument is the default argument.</param>
        internal SelectPredicate(
            ValueGeneration generation,
            ValueReference value,
            ImmutableArray<ValueReference> arguments)
            : base(generation, value, arguments)
        {
            Debug.Assert(
                value.Type.IsPrimitiveType &&
                value.Type.BasicValueType.IsInt(),
                "Invalid integer selection value");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the default argument.
        /// </summary>
        public ValueReference DefaultArgument => Arguments[0];

        /// <summary>
        /// Returns the number of actual switch cases without the default case.
        /// </summary>
        public int NumCasesWithoutDefault => Arguments.Length - 1;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the case value for the i-th case.
        /// </summary>
        /// <param name="i">The index of the i-th case.</param>
        /// <returns>The resulting argument.</returns>
        [SuppressMessage("Microsoft.Usage", "CA2233: OperationsShouldNotOverflow",
            Justification = "Exception checks avoided for performance reasons")]
        public ValueReference GetCaseArgument(int i)
        {
            Debug.Assert(i < Arguments.Length - 1, "Invalid case argument");
            return Arguments[i + 1];
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
        {
            var args = ImmutableArray.CreateBuilder<ValueReference>(Arguments.Length);
            foreach (var arg in Arguments)
                args.Add(rebuilder.Rebuild(arg));

            return builder.CreateSelectPredicate(
                rebuilder.Rebuild(Condition),
                args.MoveToImmutable());
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "select";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var result = new StringBuilder();
            result.Append(Condition.ToString());
            result.Append(" ");
            for (int i = 1, e = Arguments.Length; i < e; ++i)
            {
                result.Append(Arguments[i].ToString());
                if (i + 1 < e)
                    result.Append(", ");
            }
            result.Append(" - default: ");
            result.Append(Arguments[0].ToString());
            return result.ToString();
        }

        #endregion
    }
}
