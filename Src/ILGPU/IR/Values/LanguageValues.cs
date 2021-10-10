﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: LanguageValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPU.Util.FormatString.FormatExpression>;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents the kind of inline language.
    /// </summary>
    public enum LanguageKind
    {
        /// <summary>
        /// Inline PTX assembly.
        /// </summary>
        PTX,
    }

    /// <summary>
    /// Represents an inline lanaguage statement.
    /// </summary>
    [ValueKind(ValueKind.LanguageEmit)]
    public sealed class LanguageEmitValue : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new inline language statement.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="languageKind">The language kind.</param>
        /// <param name="expressions">The list of all format expressions.</param>
        /// <param name="hasOutput">Indicates if the first argument is output.</param>
        /// <param name="arguments">The arguments to format.</param>
        /// <param name="voidType">The void type.</param>
        internal LanguageEmitValue(
            in ValueInitializer initializer,
            LanguageKind languageKind,
            FormatArray expressions,
            bool hasOutput,
            ref ValueList arguments,
            VoidType voidType)
            : base(initializer, voidType)
        {
#if DEBUG
            foreach (var argument in arguments)
                this.Assert(argument.BasicValueType != BasicValueType.None);
            foreach (var expression in expressions)
            {
                this.Assert(
                    !expression.HasArgument ||
                    expression.Argument >= 0 && expression.Argument < arguments.Count);
            }
#endif

            LanguageKind = languageKind;
            HasOutput = hasOutput;
            Expressions = expressions;
            Seal(ref arguments);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.LanguageEmit;

        /// <summary>
        /// Returns the language kind.
        /// </summary>
        public LanguageKind LanguageKind { get; }

        /// <summary>
        /// Returns true if the first argument is an output argument.
        /// </summary>
        public bool HasOutput { get; }

        /// <summary>
        /// Returns the underlying native format expressions.
        /// </summary>
        public FormatArray Expressions { get; }

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
            return builder.CreateLanguageEmitPTX(
                Location,
                Expressions,
                HasOutput,
                ref arguments);
        }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "emit";

        /// <summary cref="Value.ToArgString"/>
        [SuppressMessage(
            "Globalization",
            "CA1307:Specify StringComparison",
            Justification = "string.Replace(string, string, StringComparison) not " +
            "available in net471")]
        protected override string ToArgString() =>
            ToStringExpression().Replace(
                Environment.NewLine,
                string.Empty) +
            " " + base.ToArgString();

        /// <summary>
        /// Converts the internal format expressions into a string for debugging purposes.
        /// </summary>
        /// <returns>The converted string.</returns>
        private string ToStringExpression()
        {
            var result = new StringBuilder();
            foreach (var expression in Expressions)
            {
                if (expression.HasArgument)
                {
                    var argument = this[expression.Argument];
                    string argumentFormat = argument.Type.ToString();
                    result.Append('{');
                    result.Append(expression.Argument);
                    result.Append(':');
                    result.Append(argumentFormat);
                    result.Append('}');
                }
                else
                {
                    result.Append(expression.String);
                }
            }
            return result.ToString();
        }

        #endregion
    }
}
