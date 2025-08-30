// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: PureValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues.Construction;
using ILGPUC.IR.Rewriting;
using System;
using System.Diagnostics;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// The base interface for all pure values.
/// Pure values can only live in the Method scope.
/// </summary>
interface IPureValue : IValue, IValue<Method>
{
    /// <summary>
    /// Rewrites the current node.
    /// </summary>
    /// <typeparam name="TRewriter">
    /// The rewriter type to control rewriting of values and value classes.
    /// </typeparam>
    /// <param name="rewriter">The current rewriter.</param>
    /// <returns>The rewritten value or null.</returns>
    Value? Rewrite<TRewriter>(in TRewriter rewriter)
        where TRewriter : IPureValueRewriter, allows ref struct;
}

/// <summary>
/// A general value initializer.
/// </summary>
/// <param name="Builder">The parent builder.</param>
/// <param name="Location">The associated location.</param>
/// <param name="Method">The associated scope.</param>
readonly record struct PureValueInitializer(
    PureValueBuilder Builder,
    Location Location,
    Method Method) : ILocation
{
    /// <summary>
    /// Returns the parent module.
    /// </summary>
    public Module Module => ModuleBuilder.Module;

    /// <summary>
    /// Returns the parent module.
    /// </summary>
    public ModuleBuilder ModuleBuilder => Builder.ModuleBuilder;

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation => Builder.Generation;

    /// <summary>
    /// Formats an error message to include specific location information.
    /// </summary>
    string ILocation.FormatErrorMessage(string message) =>
        Location.FormatErrorMessage(message);

    /// <summary>
    /// Implicitly converts the given initializer into a value initializer.
    /// </summary>
    /// <param name="initializer">The initializer to convert.</param>
    public static implicit operator ValueInitializer(PureValueInitializer initializer) =>
        new(initializer.Module, initializer.Location);
}

/// <summary>
/// Represents a basic intermediate-representation value.
/// It is the base class for all values in the scope of this IR.
/// Pure values can only live in the Method scope.
/// </summary>
/// <remarks>
/// Constructs a new value.
/// </remarks>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class PureValue(in PureValueInitializer initializer, TypeValue type) :
    Value<Method>(initializer, type),
    IPureValue,
    IValueClassInformation
{
    /// <summary>
    /// Returns the pure value class.
    /// </summary>
    static ValueClass IValueClassInformation.ValueClass => ValueClass.Pure;

    /// <summary>
    /// Returns the parent scope (always a Method).
    /// </summary>
    public sealed override Method Scope { get; } = initializer.Method;

    /// <summary>
    /// Returns the scope as method.
    /// </summary>
    Method IValue<Method>.Scope => Scope;

    /// <summary>
    /// Returns the pure value class.
    /// </summary>
    public override ValueClass ValueClass => ValueClass.Pure;

    /// <inheritdoc/>
    public sealed override bool ComputeUses(GlobalValueSet visited) =>
        base.ComputeUses(visited);

    /// <summary>
    /// Visits all child nodes using DFS.
    /// </summary>
    /// <param name="callback">The callback to be invoked for all child nodes.</param>
    [Conditional("DEBUG")]
    public void AssertRecursive(Predicate<Value> callback) =>
        VisitDFS(value => value.Assert(callback(value)));

    /// <summary>
    /// Visits all child nodes using DFS.
    /// </summary>
    /// <param name="callback">The callback to be invoked for all child nodes.</param>
    public void VisitDFS(Action<Value> callback)
    {
        callback.Invoke(this);
        foreach (var child in Values)
        {
            if (child is PureValue pureValue)
            {
                // Recurse as this cannot introduce a cyclic dependency
                pureValue.VisitDFS(callback);
            }
            else
            {
                // Invoke callback and avoid recursion
                callback.Invoke(child);
            }
        }
    }

    /// <summary>
    /// Visits all child nodes using BFS.
    /// </summary>
    /// <param name="callback">The callback to be invoked for all child nodes.</param>
    public void VisitBFS(Action<Value> callback)
    {
        callback.Invoke(this);
        VisitBFSInternal(callback);
    }

    /// <summary>
    /// Visits all child nodes using BFS.
    /// </summary>
    /// <param name="callback">The callback to be invoked for all child nodes.</param>
    private void VisitBFSInternal(Action<Value> callback)
    {
        foreach (var child in Values)
            callback.Invoke(child);

        foreach (var child in Values)
        {
            if (child is PureValue pureValue)
                pureValue.VisitBFSInternal(callback);
        }
    }

    /// <summary>
    /// Rewrites the current node.
    /// </summary>
    /// <typeparam name="TRewriter">
    /// The rewriter type to control rewriting of values and value classes.
    /// </typeparam>
    /// <param name="rewriter">The current rewriter.</param>
    /// <returns>The rewritten value or null.</returns>
    public abstract Value? Rewrite<TRewriter>(in TRewriter rewriter)
        where TRewriter : IPureValueRewriter, allows ref struct;

    /// <summary>
    /// Returns the hash code of the value id.
    /// </summary>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Returns true if the given value is exactly this value.
    /// </summary>
    public sealed override bool Equals(object? obj) =>
        obj is PureValue pureValue && pureValue.Id == Id;
}
