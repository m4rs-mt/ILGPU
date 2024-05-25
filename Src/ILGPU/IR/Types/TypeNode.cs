// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeNode.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.IR.Serialization;
using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Util;
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
    /// An abstract type node.
    /// </summary>
    public interface ITypeNode : INode
    {
        /// <summary>
        /// The type representation in the managed world.
        /// </summary>
        /// <param name="typeProvider">The type provider to use.</param>
        Type LoadManagedType<TTypeProvider>(TTypeProvider typeProvider)
            where TTypeProvider : IManagedTypeProvider;
    }

    /// <summary>
    /// An abstract type provider to convert IR types to managed types.
    /// </summary>
    public interface IManagedTypeProvider
    {
        /// <summary>
        /// Gets the managed type for the given primitive type.
        /// </summary>
        /// <param name="primitiveType">The current primitive type.</param>
        /// <returns>The managed primitive representation.</returns>
        Type GetPrimitiveType(PrimitiveType primitiveType);

        /// <summary>
        /// Converts the given view type to a managed array representation.
        /// </summary>
        /// <param name="arrayType">The current array type.</param>
        /// <returns>The managed array representation.</returns>
        Type GetArrayType(ArrayType arrayType);

        /// <summary>
        /// Converts the given view type to a managed pointer representation.
        /// </summary>
        /// <param name="pointerType">The current pointer type.</param>
        /// <returns>The managed pointer representation.</returns>
        Type GetPointerType(PointerType pointerType);

        /// <summary>
        /// Converts the given view type to a managed view representation.
        /// </summary>
        /// <param name="viewType">The current view type.</param>
        /// <returns>The managed view representation.</returns>
        Type GetViewType(ViewType viewType);

        /// <summary>
        /// Converts the given structure type to a managed view representation.
        /// </summary>
        /// <param name="structureType">The current structure type.</param>
        /// <returns>The managed structure representation.</returns>
        Type GetStructureType(StructureType structureType);
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

        #region Nested Types

        /// <summary>
        /// A simple loop-back type provider.
        /// </summary>
        public readonly struct ScalarManagedTypeProvider : IManagedTypeProvider
        {
            /// <summary>
            /// Returns the default managed type for the given primitive one.
            /// </summary>
            public Type GetPrimitiveType(PrimitiveType primitiveType) =>
                primitiveType.BasicValueType.GetManagedType()
                ?? throw new InvalidCodeGenerationException();

            /// <summary>
            /// Returns the default managed array type for the given array type.
            /// </summary>
            public Type GetArrayType(ArrayType arrayType) =>
                arrayType.GetDefaultManagedType(this);

            /// <summary>
            /// Returns the default pointer type implementation.
            /// </summary>
            public Type GetPointerType(PointerType pointerType) =>
                pointerType.GetDefaultManagedPointerType(this);

            /// <summary>
            /// Returns the default view type implementation.
            /// </summary>
            public Type GetViewType(ViewType viewType) =>
                viewType.GetDefaultManagedViewType(this);

            /// <summary>
            /// Returns the default structure type implementation reflecting the basic
            /// type hierarchy.
            /// </summary>
            public Type GetStructureType(StructureType structureType) =>
                structureType.GetDefaultManagedType(this);
        }

        #endregion

        #region Instance

        /// <summary>
        /// The managed type representation of this IR type.
        /// </summary>
        private Type? managedType;

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
        /// Returns the parent type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// Returns the current runtime system.
        /// </summary>
        public RuntimeSystem RuntimeSystem => TypeContext.RuntimeSystem;

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
        public virtual bool IsVoidType => false;

        /// <summary>
        /// Returns true if the current type is a <see cref="StringType"/>.
        /// </summary>
        public virtual bool IsStringType => false;

        /// <summary>
        /// Returns true if the current type is a <see cref="PrimitiveType"/>.
        /// </summary>
        public virtual bool IsPrimitiveType => false;

        /// <summary>
        /// Returns true if the current type is a <see cref="AddressSpaceType"/>
        /// or a <see cref="ViewType"/>.
        /// </summary>
        public bool IsViewOrPointerType => IsViewType || IsPointerType;

        /// <summary>
        /// Returns true if the current type is a <see cref="PointerType"/>.
        /// </summary>
        public virtual bool IsPointerType => false;

        /// <summary>
        /// Returns true if the current type is a <see cref="ViewType"/>.
        /// </summary>
        public virtual bool IsViewType => false;

        /// <summary>
        /// Returns true if the current type is a <see cref="ArrayType"/>.
        /// </summary>
        public virtual bool IsArrayType => false;

        /// <summary>
        /// Returns true if the current type is an <see cref="ObjectType"/>.
        /// </summary>
        public virtual bool IsObjectType => false;

        /// <summary>
        /// Returns true if the current type is a <see cref="StructureType"/>.
        /// </summary>
        public virtual bool IsStructureType => false;

        /// <summary>
        /// Returns true if the current type is a <see cref="PaddingType"/>.
        /// </summary>
        public virtual bool IsPaddingType => false;

        /// <summary>
        /// Returns true if this type is a root object type.
        /// </summary>
        public virtual bool IsRootType => false;

        /// <summary>
        /// Returns the basic value type.
        /// </summary>
        public virtual BasicValueType BasicValueType => BasicValueType.None;

        /// <summary>
        /// Returns the <see cref="TypeKind"/> of this instance.
        /// </summary>
        public abstract TypeKind TypeKind { get; }

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

        /// <summary>
        /// The type representation in the managed world by using the default type
        /// provider instance that emits scalar managed types.
        /// </summary>
        public Type LoadManagedType() =>
            managedType ??= LoadManagedType(new ScalarManagedTypeProvider());

        /// <summary>
        /// The type representation in the managed world.
        /// </summary>
        public Type LoadManagedType<TTypeProvider>(
            TTypeProvider typeProvider)
            where TTypeProvider : IManagedTypeProvider =>
            GetManagedType(typeProvider);

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
        protected abstract Type GetManagedType<TTypeProvider>(TTypeProvider typeProvider)
            where TTypeProvider : IManagedTypeProvider;

        /// <summary>
        /// Serializes this instance's specific internals to the given <see cref="IIRWriter"/>.
        /// </summary>
        /// <param name="writer">
        /// The given serializer instance. 
        /// </param>
        protected internal abstract void Write(IIRWriter writer);

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
            var result = this.AsNotNullCast<T>();
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
                    LoadManagedType().ToString());

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
        public override bool Equals(object? obj) =>
            obj is TypeNode;

        /// <summary>
        /// Returns the string representation of this node.
        /// </summary>
        /// <returns>The string representation of this node.</returns>
        public override string ToString() => ToPrefixString();

        #endregion
    }
}

namespace ILGPU.IR.Serialization
{
    public partial interface IIRWriter
    {
        /// <summary>
        /// Serializes an IR <see cref="TypeNode"/> instance to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        public void Write(TypeNode value)
        {
            Write("Type", value.Id);
            Write("TypeKind", value.TypeKind);

            value.Write(this);
        }
    }
}
