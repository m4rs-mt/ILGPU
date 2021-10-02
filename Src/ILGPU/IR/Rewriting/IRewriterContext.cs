// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRewriterContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;

namespace ILGPU.IR.Rewriting
{
    /// <summary>
    /// A rewriter context to process values.
    /// </summary>
    public interface IRewriterContext
    {
        /// <summary>
        /// Returns the associated builder.
        /// </summary>
        BasicBlock.Builder Builder { get; }

        /// <summary>
        /// Returns the associated block.
        /// </summary>
        BasicBlock Block { get; }

        /// <summary>
        /// Returns true if the given value has been converted.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True, if the given value has been converted.</returns>
        bool IsConverted(Value value);

        /// <summary>
        /// Marks the given value as converted.
        /// </summary>
        /// <param name="value">The value to mark.</param>
        /// <returns>True, if the element has been added to the set of value.</returns>
        bool MarkConverted(Value value);

        /// <summary>
        /// Replaces the given value with the new value.
        /// </summary>
        /// <typeparam name="TValue">The value type of the new value.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>Returns the new value.</returns>
        TValue Replace<TValue>(Value value, TValue newValue)
            where TValue : Value;

        /// <summary>
        /// Replaces the given value with the new value and removes it from the block.
        /// </summary>
        /// <typeparam name="TValue">The value type of the new value.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>Returns the new value.</returns>
        TValue ReplaceAndRemove<TValue>(Value value, TValue newValue)
            where TValue : Value;

        /// <summary>
        /// Removes the given value from the block.
        /// </summary>
        /// <param name="value">The current value.</param>
        void Remove(Value value);
    }

    /// <summary>
    /// Extension methods for rewriter contexts.
    /// </summary>
    public static class RewriterContextExtensions
    {
        /// <summary>
        /// Replaces the given value with the new value.
        /// </summary>
        /// <typeparam name="T">The context type.</typeparam>
        /// <param name="context">The context instance.</param>
        /// <param name="value">The current value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>Returns the new value.</returns>
        public static Value Replace<T>(
            this T context,
            Value value,
            ValueReference newValue)
            where T : IRewriterContext =>
            context.Replace(value, newValue.Resolve());

        /// <summary>
        /// Replaces the given value with the new value and removes it from the block.
        /// </summary>
        /// <typeparam name="T">The context type.</typeparam>
        /// <param name="context">The context instance.</param>
        /// <param name="value">The current value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>Returns the new value.</returns>
        public static Value ReplaceAndRemove<T>(
            this T context,
            Value value,
            ValueReference newValue)
            where T : IRewriterContext =>
            context.ReplaceAndRemove(value, newValue.Resolve());

        /// <summary>
        /// Returns the parent method.
        /// </summary>
        /// <typeparam name="T">The context type.</typeparam>
        /// <param name="context">The context instance.</param>
        /// <returns>The parent method.</returns>
        public static Method GetMethod<T>(this T context)
            where T : IRewriterContext => context.Block.Method;

        /// <summary>
        /// Returns the parent method builder.
        /// </summary>
        /// <typeparam name="T">The context type.</typeparam>
        /// <param name="context">The context instance.</param>
        /// <returns>The parent method builder.</returns>
        public static Method.Builder GetMethodBuilder<T>(this T context)
            where T : IRewriterContext => context.Builder.MethodBuilder;
    }
}
