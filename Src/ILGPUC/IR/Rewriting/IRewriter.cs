// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;

namespace ILGPUC.IR.Rewriting;

/// <summary>
/// An abstract rewriter context.
/// </summary>
interface IRewriter : IGenerationObject
{
    /// <summary>
    /// Returns true if the given value will be replaced or removed.
    /// </summary>
    bool IsReplacedOrRemoved(Value? oldValue);

    /// <summary>
    /// Tries to get a replaced value and returns the newly replaced target value in case
    /// the value had been replaced.
    /// </summary>
    /// <param name="oldValue">The old value.</param>
    /// <param name="newValue">
    /// The new value to use. Note that this value may be null, even if the function
    /// returns true. This is due to the fact that value may have been removed.
    /// </param>
    /// <returns>True if the value had been replaced.</returns>
    bool TryGetReplaced(Value? oldValue, out Value? newValue);

    /// <summary>
    /// Rewrites the given value.
    /// </summary>
    /// <param name="oldValue">The value to rebuild.</param>
    /// <returns>The rebuilt value.</returns>
    Value? Rewrite(Value oldValue);

    /// <summary>
    /// Rewrites the given value as a specific value type.
    /// </summary>
    /// <param name="oldValue">The value to rebuild.</param>
    /// <returns>The rebuilt value.</returns>
    T RewriteAs<T>(Value oldValue) where T : Value =>
        Rewrite(oldValue).AsNotNullCast<T>();
}

/// <summary>
/// An abstract rewriter context.
/// </summary>
/// <typeparam name="TBuilder">The builder type.</typeparam>
interface IRewriter<TBuilder> : IRewriter
    where TBuilder : class
{
    /// <summary>
    /// Returns the underlying builder.
    /// </summary>
    TBuilder Builder { get; }
}
