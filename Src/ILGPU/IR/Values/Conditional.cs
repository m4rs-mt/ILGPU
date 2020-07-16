// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Conditional.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a conditional predicate.
    /// </summary>
    public abstract class Conditional : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new predicate node.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal Conditional(in ValueInitializer initializer)
            : base(initializer)
        { }
        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated predicate value.
        /// </summary>
        public ValueReference Condition => this[0];

        #endregion

        #region Methods

        /// <summary>
        /// Returns the i-th argument.
        /// </summary>
        /// <param name="index">The argument index.</param>
        /// <returns>The i-th argument value.</returns>
        public ValueReference GetArgument(int index) => this[index + 1];

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            GetArgument(0).Type;

        #endregion
    }

    /// <summary>
    /// Represents a single if predicate.
    /// </summary>
    [ValueKind(ValueKind.IfPredicate)]
    public sealed class IfPredicate : Conditional
    {
        #region Instance

        /// <summary>
        /// Constructs a new predicate.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="trueValue">The true value.</param>
        /// <param name="falseValue">The false value.</param>
        internal IfPredicate(
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
        public override ValueKind ValueKind => ValueKind.IfPredicate;

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
            builder.CreateIfPredicate(
                Location,
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

    /// <summary>
    /// Represents a single switch predicate.
    /// </summary>
    [ValueKind(ValueKind.SwitchPredicate)]
    public sealed class SwitchPredicate : Conditional
    {
        #region Nested Types

        /// <summary>
        /// An instance builder for switch branches.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private ValueList builder;

            /// <summary>
            /// Initializes a new call builder.
            /// </summary>
            /// <param name="irBuilder">The current IR builder.</param>
            /// <param name="location">The current location.</param>
            /// <param name="condition">The switch condition value.</param>
            /// <param name="capacity">The initial builder capacity.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Builder(
                IRBuilder irBuilder,
                Location location,
                Value condition,
                int capacity)
            {
                builder = ValueList.Create(capacity + 1);
                builder.Add(condition);
                IRBuilder = irBuilder;
                Location = location;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public IRBuilder IRBuilder { get; }

            /// <summary>
            /// Returns the current location.
            /// </summary>
            public Location Location { get; }

            /// <summary>
            /// The number of switch values.
            /// </summary>
            public int Count => builder.Count - 1;

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given value to the switch builder.
            /// </summary>
            /// <param name="value">The value to add.</param>
            public void Add(Value value)
            {
                IRBuilder.AssertNotNull(value);
                builder.Add(value);
            }

            /// <summary>
            /// Constructs a new value that represents the current predicate.
            /// </summary>
            /// <returns>The resulting value reference.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ValueReference Seal() =>
                IRBuilder.CreateSwitchPredicate(Location, ref builder);

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new switch predicate.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="values">The selection values and the condition.</param>
        internal SwitchPredicate(
            in ValueInitializer initializer,
            ref ValueList values)
            : base(initializer)
        {
            var condition = values[0];
            Location.Assert(
                condition.Type.IsPrimitiveType &&
                condition.Type.BasicValueType.IsInt());
            Seal(ref values);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.SwitchPredicate;

        /// <summary>
        /// Returns all selection values.
        /// </summary>
        public ReadOnlySpan<ValueReference> Values => Nodes.Slice(1);

        /// <summary>
        /// Returns the default value.
        /// </summary>
        public ValueReference DefaultValue => Values[0];

        /// <summary>
        /// Returns the number of actual switch cases without the default case.
        /// </summary>
        public int NumCasesWithoutDefault => Values.Length - 1;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the case value for the i-th case.
        /// </summary>
        /// <param name="i">The index of the i-th value.</param>
        /// <returns>The resulting value.</returns>
        [SuppressMessage(
            "Microsoft.Usage",
            "CA2233: OperationsShouldNotOverflow",
            Justification = "Exception checks avoided for performance reasons")]
        public ValueReference GetCaseValue(int i)
        {
            Location.Assert(i < Values.Length - 1);
            return Values[i + 1];
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder)
        {
            var conditionalBuilder = builder.CreateSwitchPredicate(
                Location,
                Condition,
                Values.Length);
            foreach (Value value in Values)
                conditionalBuilder.Add(rebuilder.Rebuild(value));
            return conditionalBuilder.Seal();
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "switch";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            Values.ToString(new ValueReference.ToReferenceFormatter());

        #endregion
    }
}
