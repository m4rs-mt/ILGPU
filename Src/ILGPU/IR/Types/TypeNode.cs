// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: TypeNode.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Special type flags that provide additional information about the
    /// current type and all nested elements.
    /// </summary>
    [Flags]
    public enum TypeFlags : int
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
        /// The type is either an array or contains an array.
        /// </summary>
        ArrayDependent = 1 << 2,

        /// <summary>
        /// The type depends on an address space.
        /// </summary>
        AddressSpaceDependent = PointerDependent | ViewDependent
    }

    /// <summary>
    /// An abstract type node.
    /// </summary>
    public interface ITypeNode : INode
    {
        /// <summary>
        /// The type representation in the managed world.
        /// </summary>
        Type ManagedType { get; }
    }

    /// <summary>
    /// Represents a type in the scope of the ILGPU IR.
    /// </summary>
    public abstract class TypeNode : Node, ITypeNode
    {
        #region Static

        /// <summary>
        /// Computes a properly aligned offset in bytes for the given field size.
        /// </summary>
        /// <param name="offset">The current.</param>
        /// <param name="fieldAlignment">The field size in bytes.</param>
        /// <returns>The aligned field offset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align(int offset, int fieldAlignment) =>
            (int)Align((long)offset, fieldAlignment);

        #endregion

        #region Instance

        /// <summary>
        /// The managed type representation of this IR type.
        /// </summary>
        private Type managedType;

        /// <summary>
        /// Constructs a new type.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        protected TypeNode(IRTypeContext typeContext)
            : base(Location.Unknown)
        {
            TypeContext = typeContext;

            Size = 1;
            Alignment = 1;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent ILGPU context.
        /// </summary>
        public Context Context => TypeContext.Context;

        /// <summary>
        /// Returns the parent type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// The size of the type in bytes (if the type is in its lowered representation).
        /// </summary>
        public int Size { get; protected set; }

        /// <summary>
        /// The type alignment in bytes (if the type is in its lowered representation).
        /// </summary>
        public int Alignment { get; protected set; }

        /// <summary>
        /// Returns true if the current type is a <see cref="VoidType"/>.
        /// </summary>
        public bool IsVoidType => this is VoidType;

        /// <summary>
        /// Returns true if the current type is a <see cref="StringType"/>.
        /// </summary>
        public bool IsStringType => this is StringType;

        /// <summary>
        /// Returns true if the current type is a <see cref="PrimitiveType"/>.
        /// </summary>
        public bool IsPrimitiveType => this is PrimitiveType;

        /// <summary>
        /// Returns true if the current type is a <see cref="PointerType"/>
        /// or a <see cref="ViewType"/>.
        /// </summary>
        public bool IsViewOrPointerType => this is AddressSpaceType;

        /// <summary>
        /// Returns true if the current type is a <see cref="PointerType"/>.
        /// </summary>
        public bool IsPointerType => this is PointerType;

        /// <summary>
        /// Returns true if the current type is a <see cref="ViewType"/>.
        /// </summary>
        public bool IsViewType => this is ViewType;

        /// <summary>
        /// Returns true if the current type is an <see cref="ObjectType"/>.
        /// </summary>
        public bool IsObjectType => this is ObjectType;

        /// <summary>
        /// Returns true if the current type is a <see cref="StructureType"/>.
        /// </summary>
        public bool IsStructureType => this is StructureType;

        /// <summary>
        /// Returns true if the current type is a <see cref="ArrayType"/>.
        /// </summary>
        public bool IsArrayType => this is ArrayType;

        /// <summary>
        /// Returns true if the current type is a <see cref="PaddingType"/>.
        /// </summary>
        public bool IsPaddingType => this is PaddingType;

        /// <summary>
        /// Returns true if this type is a root object type.
        /// </summary>
        public bool IsRootType =>
            this is StructureType structureType &&
            structureType.NumFields < 1;

        /// <summary>
        /// Returns the basic value type.
        /// </summary>
        public BasicValueType BasicValueType =>
            this is PrimitiveType primitiveType
            ? primitiveType.BasicValueType
            : BasicValueType.None;

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

        /// <summary>
        /// The type representation in the managed world.
        /// </summary>
        public Type ManagedType => managedType ??= GetManagedType();

        #endregion

        #region Methods

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
        /// Creates a managed type that corresponds to this IR type.
        /// </summary>
        /// <returns>The created managed type.</returns>
        protected abstract Type GetManagedType();

        /// <summary>
        /// Converts the current type to the given type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type node.</typeparam>
        /// <param name="location">The location to use for assertions.</param>
        /// <returns>The converted type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T As<T>(ILocation location)
            where T : TypeNode
        {
            var result = this as T;
            location.AssertNotNull(result);
            return result;
        }

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
                    ManagedType.ToString());

        #endregion

        #region Object

        /// <summary>
        /// Returns the hash code of this type node.
        /// </summary>
        /// <returns>The hash code of this type node.</returns>
        public override int GetHashCode() => 0;

        /// <summary>
        /// Returns true if the given object is equal to the current type.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current type.</returns>
        public override bool Equals(object obj) =>
            obj is TypeNode;

        /// <summary>
        /// Returns the string representation of this node.
        /// </summary>
        /// <returns>The string representation of this node.</returns>
        public override string ToString() => ToPrefixString();

        #endregion
    }
}
