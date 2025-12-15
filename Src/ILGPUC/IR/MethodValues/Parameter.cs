// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Parameter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Rewriting;

namespace ILGPUC.IR.MethodValues;

/// <summary>
/// Represents a function parameter.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">The parameter type.</param>
/// <param name="name">The parameter name (for debugging purposes).</param>
/// <param name="index">The preliminary parameter index.</param>
sealed partial class Parameter(
    in MethodValueInitializer initializer,
    TypeValue type,
    string? name,
    int index) : MethodValue(initializer, type), IDumpable
{
    /// <summary>
    /// Returns the parameter name (for debugging purposes).
    /// </summary>
    public string Name { get; } = name ?? "param";

    /// <summary>
    /// Returns the parameter index.
    /// </summary>
    public int Index { get; private set; } = index;

    /// <summary>
    /// Seals this parameter using the index provided.
    /// </summary>
    /// <param name="index">The parameter index.</param>
    internal void SealParameter(int index)
    {
        Index = index;

        Seal(Type);
    }

    /// <summary>
    /// Rewrites the current node.
    /// </summary>
    /// <typeparam name="TRewriter">
    /// The rewriter type to control rewriting of values and value classes.
    /// </typeparam>
    /// <param name="rewriter">The current rewriter.</param>
    /// <returns>The rewritten value.</returns>
    public Parameter Rewrite<TRewriter>(in TRewriter rewriter)
        where TRewriter : IMethodRewriter, allows ref struct =>
        rewriter.Builder.CreateParameter(
            rewriter.RewriteAs<TypeValue>(Type),
            Name);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => $"{Name}_{Index}";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Type} @ {Method.ToReferenceString()}";

    /// <summary>
    /// Return the parameter string.
    /// </summary>
    /// <returns>The parameter string.</returns>
    internal string ToParameterString() => $"{Type} {ToReferenceString()}";
}
