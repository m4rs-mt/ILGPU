// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR.Rewriting;
using System;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Special type flags that provide additional information about the
/// current type and all nested elements.
/// </summary>
[Flags]
enum TypeFlags : int
{
    /// <summary>
    /// No special flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// The type is either a pointer or contains a pointer.
    /// </summary>
    PointerDependent = 1 << 0,

    /// <summary>
    /// The type is either a view or contains a view.
    /// </summary>
    ViewDependent = 1 << 1,

    /// <summary>
    /// The type is either a structure or contains a structure.
    /// </summary>
    StructureDependent = 1 << 2,

    /// <summary>
    /// The type is either an array or contains an array.
    /// </summary>
    ArrayDependent = 1 << 3,

    /// <summary>
    /// The type depends on an address space.
    /// </summary>
    AddressSpaceDependent = PointerDependent | ViewDependent
}

/// <summary>
/// Represents a type in the scope of the ILGPU IR.
/// </summary>
/// <param name="initializer">The parent value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class TypeValue(in ModuleValueInitializer initializer, TypeValue type) :
    ModuleValue(initializer, type),
    IValueClassInformation
{
    #region Static

    /// <summary>
    /// Computes a properly aligned offset in bytes for the given field size.
    /// </summary>
    /// <param name="offset">The current.</param>
    /// <param name="fieldAlignment">The field size in bytes.</param>
    /// <returns>The aligned field offset.</returns>
    public static long Align(long offset, int fieldAlignment)
    {
        var padding = (fieldAlignment - (offset % fieldAlignment)) % fieldAlignment;
        return offset + padding;
    }

    /// <summary>
    /// Computes a properly aligned offset in bytes for the given field size.
    /// </summary>
    /// <param name="offset">The current.</param>
    /// <param name="fieldAlignment">The field size in bytes.</param>
    /// <returns>The aligned field offset.</returns>
    public static int Align(int offset, int fieldAlignment) =>
        (int)Align((long)offset, fieldAlignment);

    #endregion

    #region Properties

    /// <summary>
    /// Returns the type class.
    /// </summary>
    static ValueClass IValueClassInformation.ValueClass => ValueClass.Type;

    /// <summary>
    /// Returns <see cref="ValueClass.Type"/>.
    /// </summary>
    public override ValueClass ValueClass => ValueClass.Type;

    /// <summary>
    /// The size of the type in bytes (if the type is in its lowered representation).
    /// </summary>
    public int Size { get; protected set; } = 1;

    /// <summary>
    /// The type alignment in bytes (if the type is in its lowered representation).
    /// </summary>
    public int Alignment { get; protected set; } = 1;

    /// <summary>
    /// Returns all type flags.
    /// </summary>
    public TypeFlags Flags { get; private set; }

    /// <summary>
    /// Returns true if this type corresponds to its lowered representation.
    /// </summary>
    /// <remarks>
    /// Lowered in this scope means that this type does not contains nested arrays
    /// and views. In this case the size and alignment information can be used
    /// immediately for interop purposes.
    /// </remarks>
    public bool IsLowered =>
        Size > 0 && Alignment > 0 &&
        !HasFlags(TypeFlags.ArrayDependent | TypeFlags.ViewDependent);

    #endregion

    #region Methods

    /// <inheritdoc/>
    public sealed override bool ComputeUses(GlobalValueSet visited) =>
        base.ComputeUses(visited);

    /// <summary>
    /// Returns true if the given flags are set.
    /// </summary>
    /// <param name="typeFlags">The flags to test.</param>
    /// <returns>True, if the given flags are set.</returns>
    public bool HasFlags(TypeFlags typeFlags) =>
        (Flags & typeFlags) != TypeFlags.None;

    /// <summary>
    /// Adds the given flags to the current type.
    /// </summary>
    /// <param name="typeFlags">The flags to add.</param>
    protected void AddFlags(TypeFlags typeFlags) => Flags |= typeFlags;

    /// <summary>
    /// Converts the current type to the given type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type node.</typeparam>
    /// <returns>The converted type.</returns>
    public T As<T>() where T : TypeValue => this.AsNotNullCast<T>();

    /// <summary>
    /// Rewrites the current type value instance.
    /// </summary>
    /// <typeparam name="TRewriter">
    /// The rewriter type to control rewriting of values and value classes.
    /// </typeparam>
    /// <param name="rewriter">The current rewriter.</param>
    /// <returns>The rewritten value.</returns>
    public abstract TypeValue? Rewrite<TRewriter>(in TRewriter rewriter)
        where TRewriter : ITypeRewriter, allows ref struct;

    #endregion

    #region ILocation

    /// <summary>
    /// Formats an error message to include the current debug information.
    /// </summary>
    public override string FormatErrorMessage(string message) =>
        string.Format(
            ErrorMessages.LocationTypeMessage,
                message,
                ToString(),
                ToString());

    #endregion

    #region Object

    /// <summary>
    /// Returns the hash code of this type node.
    /// </summary>
    /// <returns>The hash code of this type node.</returns>
    public override int GetHashCode() => base.GetHashCode();

    /// <summary>
    /// Returns true if this type value is equal to the given object.
    /// </summary>
    /// <returns>True if the given object is equal to this type value.</returns>
    public override bool Equals(object? obj) => obj is TypeValue;

    /// <summary>
    /// Returns the string representation of this node.
    /// </summary>
    /// <returns>The string representation of this node.</returns>
    public override string ToString() => "<type>";

    #endregion
}
