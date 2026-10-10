// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodHandle.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Rewriting;
using System;
using System.Diagnostics;
using System.Reflection;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents a method handle to an <see cref="Method"/>
/// that is valid across transformations.
/// </summary>
/// <param name="Id">The unique id of the reference.</param>
/// <param name="Name">The name of the method reference.</param>
[Serializable]
readonly record struct MethodHandle(Guid Id, string? Name = null)
{
    /// <summary>
    /// Creates an empty named method handle.
    /// </summary>
    /// <param name="name">The name of the method reference.</param>
    /// <returns>The created method handle.</returns>
    public static MethodHandle Create(string name) => new(Guid.NewGuid(), name);

    /// <summary>
    /// Returns true if this handle is empty.
    /// </summary>
    public bool IsEmpty => Name is null || Id == default;

    /// <summary>
    /// Returns the string representation of this handle.
    /// </summary>
    /// <returns>The string representation of this handle.</returns>
    public override string ToString() => IsEmpty ? "<Empty>" : Name + "_" + Id;

    /// <summary>
    /// Returns the string representation of this handle.
    /// </summary>
    /// <returns>The string representation of this handle.</returns>
    public string ToRefString() => Name is null ? "<Empty>" : Name;
}

/// <summary>
/// Represents a method declaration of a <see cref="Method"/>.
/// </summary>
readonly struct MethodDeclaration : IEquatable<MethodDeclaration>
{
    /// <summary>
    /// This method must never be inlined.
    /// </summary>
    public const MethodFlags MustNotInlineFlags =
        MethodFlags.NoInline |
        MethodFlags.External |
        MethodFlags.Intrinsic |
        MethodFlags.EntryPoint;

    #region Instance

    /// <summary>
    /// Constructs a new method declaration.
    /// </summary>
    /// <param name="handle">The method handle (may be an empty handle).</param>
    /// <param name="returnType">The return type.</param>
    /// <param name="flags">Custom method flags.</param>
    public MethodDeclaration(
        MethodHandle handle,
        TypeValue returnType,
        MethodFlags flags)
        : this(handle, returnType, null, flags)
    { }

    /// <summary>
    /// Constructs a new method declaration.
    /// </summary>
    /// <param name="handle">The method handle (may be an empty handle).</param>
    /// <param name="returnType">The return type.</param>
    /// <param name="source">The source method.</param>
    public MethodDeclaration(
        MethodHandle handle,
        TypeValue returnType,
        MethodBase source)
        : this(handle, returnType, source, MethodFlags.None)
    { }

    /// <summary>
    /// Constructs a new method declaration.
    /// </summary>
    /// <param name="handle">The method handle (may be an empty handle).</param>
    /// <param name="returnType">The return type.</param>
    /// <param name="source">The source method.</param>
    /// <param name="flags">Custom method flags.</param>
    public MethodDeclaration(
        MethodHandle handle,
        TypeValue returnType,
        MethodBase? source,
        MethodFlags flags)
    {
        Handle = handle;
        ReturnType = returnType;
        Source = source;
        Flags = flags;

        if (flags == MethodFlags.None && Source != null)
            Flags = Method.ResolveMethodFlags(Source);
    }

    /// <summary>
    /// Constructs a new method declaration with exactly the given flags, which
    /// are not resolved from the source method when they are empty.
    /// </summary>
    /// <param name="declaration">The declaration to copy.</param>
    /// <param name="flags">The method flags.</param>
    private MethodDeclaration(in MethodDeclaration declaration, MethodFlags flags)
    {
        Handle = declaration.Handle;
        ReturnType = declaration.ReturnType;
        Source = declaration.Source;
        Flags = flags;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated method flags.
    /// </summary>
    public MethodFlags Flags { get; }

    /// <summary>
    /// Returns true if the associated handle is not empty.
    /// </summary>
    public bool HasHandle => !Handle.IsEmpty;

    /// <summary>
    /// Returns the associated handle.
    /// </summary>
    public MethodHandle Handle { get; }

    /// <summary>
    /// Returns the return type.
    /// </summary>
    public TypeValue ReturnType { get; }

    /// <summary>
    /// Returns the managed source method.
    /// </summary>
    public MethodBase? Source { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Rewrites this method declaration using the rewriter provided.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type.</typeparam>
    /// <param name="rewriter">The rewriter to use.</param>
    /// <returns>The rewritten method declaration.</returns>
    public MethodDeclaration Rewrite<TRewriter>(TRewriter rewriter)
        where TRewriter : IModuleRewriter
    {
        var returnType = rewriter.RewriteAs<TypeValue>(ReturnType);
        return new(Handle, returnType, Source, Flags);
    }

    /// <summary>
    /// Returns true if this declaration has the given method flags.
    /// </summary>
    /// <param name="flags">The flags to check.</param>
    /// <returns>True, if this declaration has the given method flags.</returns>
    public bool HasFlags(MethodFlags flags) =>
        (Flags & flags) != 0;

    /// <summary>
    /// Adds the given flags to this declaration.
    /// </summary>
    /// <param name="flags">The flags to add.</param>
    public MethodDeclaration AddFlags(MethodFlags flags) =>
        new(Handle, ReturnType, Source, Flags | flags);

    /// <summary>
    /// Removes the given flags from this declaration.
    /// </summary>
    /// <param name="flags">The flags to remove.</param>
    public MethodDeclaration RemoveFlags(MethodFlags flags) =>
        new(this, Flags & ~flags);

    #endregion

    #region IEquatable

    /// <summary>
    /// Returns true if the given declaration is equal to this declaration.
    /// </summary>
    /// <param name="other">The other declaration.</param>
    /// <returns>
    /// True, if the given declaration is equal to this declaration.
    /// </returns>
    public bool Equals(MethodDeclaration other) => this == other;

    #endregion

    #region Object

    /// <summary>
    /// Returns true if the given object is equal to this declaration.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>True, if the given object is equal to this declaration.</returns>
    public override bool Equals(object? obj) =>
        obj is MethodDeclaration declaration && declaration == this;

    /// <summary>
    /// Returns the hash code of this declaration.
    /// </summary>
    /// <returns>The hash code of this declaration.</returns>
    public override int GetHashCode() => Handle.GetHashCode();

    /// <summary>
    /// Returns the string representation of this declaration.
    /// </summary>
    /// <returns>The string representation of this declaration.</returns>
    public override string ToString()
    {
        var baseExpression = Handle.ToString() + " -> " + ReturnType.ToString();
        if (Flags != MethodFlags.None)
            baseExpression += " [ " + Flags + " ]";
        return baseExpression;
    }

    #endregion

    #region Operators

    /// <summary>
    /// Returns true if the first and the second declaration are the same.
    /// </summary>
    /// <param name="first">The first declaration.</param>
    /// <param name="second">The second declaration.</param>
    /// <returns>
    /// True, if the first and the second declaration are the same.
    /// </returns>
    public static bool operator ==(MethodDeclaration first, MethodDeclaration second) =>
        first.Handle == second.Handle;

    /// <summary>
    /// Returns true if the first and the second declaration are not the same.
    /// </summary>
    /// <param name="first">The first declaration.</param>
    /// <param name="second">The second declaration.</param>
    /// <returns>
    /// True, if the first and the second declaration are not the same.
    /// </returns>
    public static bool operator !=(MethodDeclaration first, MethodDeclaration second) =>
        !(first == second);

    #endregion
}
