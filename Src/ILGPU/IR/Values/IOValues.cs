// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IOValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPU.IR.Values.WriteToOutput.FormatExpression>;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract Input/Output (IO) value with side effects.
    /// </summary>
    public abstract class IOValue : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="staticType">The static type.</param>
        internal IOValue(in ValueInitializer initializer, TypeNode staticType)
            : base(initializer, staticType)
        { }

        #endregion
    }

    /// <summary>
    /// Represents a console output.
    /// </summary>
    [ValueKind(ValueKind.WriteToOutput)]
    public sealed class WriteToOutput : IOValue
    {
        #region Nested Types

        /// <summary>
        /// Represents a single format command.
        /// </summary>
        public readonly struct FormatExpression
        {
            /// <summary>
            /// Constructs a new format expression.
            /// </summary>
            /// <param name="string">The string expression.</param>
            public FormatExpression(string @string)
            {
                String = @string ?? string.Empty;
                Argument = -1;
            }

            /// <summary>
            /// Constructs a new format expression.
            /// </summary>
            /// <param name="argument">The argument reference.</param>
            public FormatExpression(int argument)
            {
                String = null;
                Argument = argument;
            }

            /// <summary>
            /// Returns the string to output (if any).
            /// </summary>
            public string String { get; }

            /// <summary>
            /// Returns the argument reference to output.
            /// </summary>
            public int Argument { get; }

            /// <summary>
            /// Returns true if the current expression has an argument reference.
            /// </summary>
            public readonly bool HasArgument => String is null;
        }

        /// <summary>
        /// Represents an write argument collection.
        /// </summary>
        public readonly ref struct ArgumentCollection
        {
            #region Nested Types

            /// <summary>
            /// Returns an enumerator to enumerate all values in argument collection.
            /// </summary>
            public ref struct Enumerator
            {
                private FormatArray.Enumerator enumerator;

                /// <summary>
                /// Constructs a new use enumerator.
                /// </summary>
                /// <param name="writeToOutput">The parent write node.</param>
                internal Enumerator(WriteToOutput writeToOutput)
                {
                    WriteToOutput = writeToOutput;
                    enumerator = writeToOutput.Expressions.GetEnumerator();
                    Current = default;
                }

                /// <summary>
                /// Returns the associated node.
                /// </summary>
                public WriteToOutput WriteToOutput { get; }

                /// <summary>
                /// Returns the current use.
                /// </summary>
                public Value Current { get; private set; }

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext()
                {
                    while (enumerator.MoveNext())
                    {
                        if (!enumerator.Current.HasArgument)
                            continue;
                        Current = WriteToOutput[enumerator.Current.Argument];
                        return true;
                    }
                    return false;
                }
            }

            #endregion

            /// <summary>
            /// Constructs a new argument collection.
            /// </summary>
            /// <param name="writeToOutput">The parent write node.</param>
            internal ArgumentCollection(WriteToOutput writeToOutput)
            {
                WriteToOutput = writeToOutput;
            }

            /// <summary>
            /// Returns the associated node.
            /// </summary>
            public WriteToOutput WriteToOutput { get; }

            /// <summary>
            /// Returns an enumerator to enumerate all uses in the context
            /// of the parent scope.
            /// </summary>
            /// <returns>The enumerator.</returns>
            public readonly Enumerator GetEnumerator() => new Enumerator(WriteToOutput);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new debug operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="expressions">The list of all format expressions.</param>
        /// <param name="arguments">The arguments to format.</param>
        /// <param name="voidType">The void type.</param>
        internal WriteToOutput(
            in ValueInitializer initializer,
            FormatArray expressions,
            ref ValueList arguments,
            VoidType voidType)
            : base(initializer, voidType)
        {
            foreach (var argument in arguments)
                this.Assert(argument.BasicValueType != BasicValueType.None);
            foreach (var expression in expressions)
            {
                this.Assert(
                    !expression.HasArgument ||
                    expression.Argument >= 0 && expression.Argument < arguments.Count);
            }

            Expressions = expressions;
            Seal(ref arguments);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.WriteToOutput;

        /// <summary>
        /// Returns the underlying native format expressions.
        /// </summary>
        public FormatArray Expressions { get; }

        /// <summary>
        /// Returns all direct argument references for further processing.
        /// </summary>
        public ArgumentCollection Arguments => new ArgumentCollection(this);

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder)
        {
            var arguments = ValueList.Create(Count);
            foreach (Value argument in this)
                arguments.Add(rebuilder[argument]);
            return builder.CreateWriteToOutput(
                Location,
                Expressions,
                ref arguments);
        }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Methods

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "write";

        #endregion
    }
}
