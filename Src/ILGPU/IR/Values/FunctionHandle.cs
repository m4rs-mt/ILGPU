// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionHandle.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a function handle to a <see cref="TopLevelFunction"/>
    /// that is valid accross transformations.
    /// </summary>
    [Serializable]
    public readonly struct FunctionHandle : IEquatable<FunctionHandle>
    {
        #region Static

        /// <summary>
        /// An empty function handle.
        /// </summary>
        public static readonly FunctionHandle Empty = default;

        /// <summary>
        /// Reconstructs a function handle from a serialization string.
        /// </summary>
        /// <param name="serializationString">The handle serialization string.</param>
        /// <returns>The deserialized function handle.</returns>
        public static FunctionHandle Deserialize(string serializationString)
        {
            if (string.IsNullOrWhiteSpace(serializationString))
                throw new ArgumentNullException(nameof(serializationString));
            var parts = serializationString.Split('/');
            if (parts.Length != 2 || !long.TryParse(parts[1], out long id))
                throw new ArgumentOutOfRangeException(nameof(serializationString));
            return new FunctionHandle(id, parts[0]);
        }

        /// <summary>
        /// Creates an empty named function handle.
        /// </summary>
        /// <param name="name">The name of the function reference.</param>
        /// <returns>The created function handle.</returns>
        public static FunctionHandle Create(string name) =>
            new FunctionHandle(0, name);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new function handle.
        /// </summary>
        /// <param name="id">The unique id of the refernce.</param>
        /// <param name="name">The name of the function reference.</param>
        internal FunctionHandle(long id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            if (id < 0)
                throw new ArgumentOutOfRangeException(nameof(id));
            Name = name;
            Id = id;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the unique id.
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// Returns true if this handle is empty.
        /// </summary>
        public bool IsEmpty => Name == null || Id < 1;

        /// <summary>
        /// Returns the name of the referenced function.
        /// </summary>
        public string Name { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a serialization string.
        /// </summary>
        /// <returns>The created serialization string.</returns>
        public string Serialize() => Name + "/" + Id;

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given handle is equal to this handle.
        /// </summary>
        /// <param name="other">The other handle.</param>
        /// <returns>True, iff the given handle is equal to this handle.</returns>
        public bool Equals(FunctionHandle other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to this handle.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to this handle.</returns>
        public override bool Equals(object obj)
        {
            if (obj is FunctionHandle handle)
                return handle == this;
            return false;
        }

        /// <summary>
        /// Returns the hash code of this handle.
        /// </summary>
        /// <returns>The hash code of this handle.</returns>
        public override int GetHashCode() => Id.GetHashCode() ^ Name?.GetHashCode() ?? 0;

        /// <summary>
        /// Returns the string representation of this handle.
        /// </summary>
        /// <returns>The string representation of this handle.</returns>
        public override string ToString() => IsEmpty ? "<Empty>" : Name + "_" + Id;

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and the second handle are the same.
        /// </summary>
        /// <param name="first">The first handle.</param>
        /// <param name="second">The second handle.</param>
        /// <returns>True, iff the first and the second handle are the same.</returns>
        public static bool operator ==(FunctionHandle first, FunctionHandle second)
        {
            return first.Id == second.Id && first.Name == second.Name;
        }

        /// <summary>
        /// Returns true iff the first and the second handle are not the same.
        /// </summary>
        /// <param name="first">The first handle.</param>
        /// <param name="second">The second handle.</param>
        /// <returns>True, iff the first and the second handle are not the same.</returns>
        public static bool operator !=(FunctionHandle first, FunctionHandle second)
        {
            return first.Id != second.Id || first.Name != second.Name;
        }

        #endregion
    }

    /// <summary>
    /// Represents a function declaration for a <see cref="TopLevelFunction"/>.
    /// </summary>
    public readonly struct FunctionDeclaration : IEquatable<FunctionDeclaration>
    {
        #region Instance

        /// <summary>
        /// Constructs a new function declaration with an implicit handle.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="returnType">The return type.</param>
        public FunctionDeclaration(
            string name,
            TypeNode returnType)
            : this(name, returnType, TopLevelFunctionFlags.None)
        { }

        /// <summary>
        /// Constructs a new function declaration with an implicit handle.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="returnType">The return type.</param>
        /// <param name="flags">Custom function flags.</param>
        public FunctionDeclaration(
            string name,
            TypeNode returnType,
            TopLevelFunctionFlags flags)
            : this(FunctionHandle.Create(name), returnType, flags)
        { }

        /// <summary>
        /// Constructs a new function declaration.
        /// </summary>
        /// <param name="handle">The function handle (may be an empty handle).</param>
        /// <param name="returnType">The return type.</param>
        public FunctionDeclaration(
            FunctionHandle handle,
            TypeNode returnType)
            : this(handle, returnType, TopLevelFunctionFlags.None)
        { }

        /// <summary>
        /// Constructs a new function declaration.
        /// </summary>
        /// <param name="handle">The function handle (may be an empty handle).</param>
        /// <param name="returnType">The return type.</param>
        /// <param name="flags">Custom function flags.</param>
        public FunctionDeclaration(
            FunctionHandle handle,
            TypeNode returnType,
            TopLevelFunctionFlags flags)
            : this(handle, returnType, null, flags)
        { }

        /// <summary>
        /// Constructs a new function declaration.
        /// </summary>
        /// <param name="handle">The function handle (may be an empty handle).</param>
        /// <param name="returnType">The return type.</param>
        /// <param name="source">The source method.</param>
        public FunctionDeclaration(
            FunctionHandle handle,
            TypeNode returnType,
            MethodBase source)
            : this(handle, returnType, source, TopLevelFunctionFlags.None)
        { }

        /// <summary>
        /// Constructs a new function declaration.
        /// </summary>
        /// <param name="handle">The function handle (may be an empty handle).</param>
        /// <param name="returnType">The return type.</param>
        /// <param name="source">The source method.</param>
        /// <param name="flags">Custom function flags.</param>
        public FunctionDeclaration(
            FunctionHandle handle,
            TypeNode returnType,
            MethodBase source,
            TopLevelFunctionFlags flags)
        {
            Handle = handle;
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            Source = source;
            Flags = flags;

            if (flags == TopLevelFunctionFlags.None)
            {
                if (Source != null)
                {
                    if ((Source.MethodImplementationFlags & MethodImplAttributes.AggressiveInlining)
                        == MethodImplAttributes.AggressiveInlining)
                        Flags |= TopLevelFunctionFlags.AggressiveInlining;
                    if ((Source.MethodImplementationFlags & MethodImplAttributes.NoInlining)
                        == MethodImplAttributes.NoInlining)
                        Flags |= TopLevelFunctionFlags.NoInlining;
                }
                else if (source.Module.Name == Context.AssemblyModuleName)
                    Flags |= TopLevelFunctionFlags.AggressiveInlining;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated function flags.
        /// </summary>
        public TopLevelFunctionFlags Flags { get; }

        /// <summary>
        /// Returns true if this function is an external function.
        /// </summary>
        public bool IsExternal => (Flags & TopLevelFunctionFlags.External) ==
            TopLevelFunctionFlags.External;

        /// <summary>
        /// Returns true if the associated handle is not empty.
        /// </summary>
        public bool HasHandle => !Handle.IsEmpty;

        /// <summary>
        /// Returns the associated handle.
        /// </summary>
        public FunctionHandle Handle { get; }

        /// <summary>
        /// Returns the return type.
        /// </summary>
        public TypeNode ReturnType { get; }

        /// <summary>
        /// Returns true if the associated source method is not null.
        /// </summary>
        public bool HasSource => Source != null;

        /// <summary>
        /// Returns the managed source method.
        /// </summary>
        public MethodBase Source { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Specializes the current function declaration by specializing
        /// an empty function handle.
        /// </summary>
        /// <param name="handle">The handle to specialize.</param>
        /// <returns>The specialized function declaration.</returns>
        public FunctionDeclaration Specialize(FunctionHandle handle)
        {
            if (handle.IsEmpty)
                throw new ArgumentNullException(nameof(handle));
            if (HasHandle)
                throw new InvalidOperationException();
            return new FunctionDeclaration(handle, ReturnType, Source, Flags);
        }

        /// <summary>
        /// Specializes the current function declaration by specializing
        /// the return type.
        /// </summary>
        /// <param name="returnType">The return type to specialize.</param>
        /// <returns>The specialized function declaration.</returns>
        public FunctionDeclaration Specialize(TypeNode returnType) =>
            new FunctionDeclaration(Handle, returnType, Source, Flags);

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given declaration is equal to this declaration.
        /// </summary>
        /// <param name="other">The other declaration.</param>
        /// <returns>True, iff the given declaration is equal to this declaration.</returns>
        public bool Equals(FunctionDeclaration other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to this declaration.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to this declaration.</returns>
        public override bool Equals(object obj)
        {
            if (obj is FunctionDeclaration declaration)
                return declaration == this;
            return false;
        }

        /// <summary>
        /// Returns the hash code of this declaration.
        /// </summary>
        /// <returns>The hash code of this declaration.</returns>
        public override int GetHashCode() =>
            Handle.GetHashCode() ^ ReturnType.GetHashCode();

        /// <summary>
        /// Returns the string representation of this declaration.
        /// </summary>
        /// <returns>The string representation of this declaration.</returns>
        public override string ToString()
        {
            var baseExpression = Handle.ToString() + " -> " + ReturnType.ToString();
            if (Flags != TopLevelFunctionFlags.None)
                baseExpression += " [ " + Flags + " ]";
            return baseExpression;
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and the second declaration are the same.
        /// </summary>
        /// <param name="first">The first declaration.</param>
        /// <param name="second">The second declaration.</param>
        /// <returns>True, iff the first and the second declaration are the same.</returns>
        public static bool operator ==(FunctionDeclaration first, FunctionDeclaration second)
        {
            return first.Handle == second.Handle &&
                first.ReturnType == second.ReturnType;
        }

        /// <summary>
        /// Returns true iff the first and the second declaration are not the same.
        /// </summary>
        /// <param name="first">The first declaration.</param>
        /// <param name="second">The second declaration.</param>
        /// <returns>True, iff the first and the second declaration are not the same.</returns>
        public static bool operator !=(FunctionDeclaration first, FunctionDeclaration second)
        {
            return first.Handle != second.Handle ||
                first.ReturnType != second.ReturnType;
        }

        #endregion
    }
}
