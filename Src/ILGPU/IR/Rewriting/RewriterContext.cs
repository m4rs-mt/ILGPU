// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: RewriterContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Rewriting
{
    /// <summary>
    /// Helper class to process value instances.
    /// </summary>
    public readonly struct RewriterContext : IRewriterContext
    {
        #region Instance

        /// <summary>
        /// Constructs a new value processor.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="converted">The set of converted value.</param>
        internal RewriterContext(
            BasicBlock.Builder builder,
            HashSet<Value> converted)
        {
            Debug.Assert(builder != null, "Invalid builder");

            Builder = builder;
            Converted = converted;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated builder.
        /// </summary>
        public BasicBlock.Builder Builder { get; }

        /// <summary>
        /// Returns the associated block.
        /// </summary>
        public readonly BasicBlock Block => Builder.BasicBlock;

        /// <summary>
        /// The set of all converted nodes.
        /// </summary>
        private HashSet<Value> Converted { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Specializes the build by setting a new block builder.
        /// </summary>
        /// <param name="newBuilder">The new builder to use.</param>
        /// <returns>The specialized rewriter context.</returns>
        public readonly RewriterContext SpecializeBuilder(
            BasicBlock.Builder newBuilder) =>
            new RewriterContext(newBuilder, Converted);

        /// <summary>
        /// Returns true if the given value has been converted.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True, if the given value has been converted.</returns>
        public readonly bool IsConverted(Value value) => Converted.Contains(value);

        /// <summary>
        /// Marks the given value as converted.
        /// </summary>
        /// <param name="value">The value to mark.</param>
        /// <returns>True, if the element has been added to the set of value.</returns>
        public readonly bool MarkConverted(Value value) => Converted.Add(value);

        /// <summary>
        /// Replaces the given value with the new value.
        /// </summary>
        /// <typeparam name="TValue">The value type of the new value.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>Returns the new value.</returns>
        public readonly TValue Replace<TValue>(Value value, TValue newValue)
            where TValue : Value
        {
            value.Replace(newValue);
            MarkConverted(newValue);
            return newValue;
        }

        /// <summary>
        /// Replaces the given value with the new value and removes it from the block.
        /// </summary>
        /// <typeparam name="TValue">The value type of the new value.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>Returns the new value.</returns>
        public readonly TValue ReplaceAndRemove<TValue>(Value value, TValue newValue)
            where TValue : Value
        {
            var replaced = Replace(value, newValue);
            Builder.Remove(value);
            return replaced;
        }

        /// <summary>
        /// Removes the given value from the block.
        /// </summary>
        /// <param name="value">The current value.</param>
        public readonly void Remove(Value value)
        {
            MarkConverted(value);
            Builder.Remove(value);
        }

        #endregion
    }

    /// <summary>
    /// Provides rewriter context instances.
    /// </summary>
    /// <typeparam name="TContext">The context instance type.</typeparam>
    /// <typeparam name="T">The custom data type.</typeparam>
    public interface IRewriterContextProvider<TContext, T>
        where TContext : struct, IRewriterContext
    {
        /// <summary>
        /// Creates a new rewriter context.
        /// </summary>
        /// <param name="builder">The current block builder.</param>
        /// <param name="converted">The set of converted values.</param>
        /// <param name="data">The user defined data instance.</param>
        /// <returns>The created rewriter context.</returns>
        TContext CreateContext(
            BasicBlock.Builder builder,
            HashSet<Value> converted,
            T data);
    }

    /// <summary>
    /// Provides <see cref="RewriterContext"/> instances.
    /// </summary>
    /// <typeparam name="T">The custom data type.</typeparam>
    public readonly struct RewriterContextProvider<T> :
        IRewriterContextProvider<RewriterContext, T>
    {
        /// <summary>
        /// Creates a new <see cref="RewriterContext"/>.
        /// </summary>
        public RewriterContext CreateContext(
            BasicBlock.Builder builder,
            HashSet<Value> converted,
            T _) =>
            new RewriterContext(builder, converted);
    }
}
