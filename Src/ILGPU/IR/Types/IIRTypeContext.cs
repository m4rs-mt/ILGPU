// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IIRTypeContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections.Immutable;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents an abstract type context.
    /// </summary>
    public interface IIRTypeContext
    {
        /// <summary>
        /// Returns the void type.
        /// </summary>
        VoidType VoidType { get; }

        /// <summary>
        /// Returns the memory type.
        /// </summary>
        StringType StringType { get; }

        /// <summary>
        /// Returns the main index type.
        /// </summary>
        StructureType IndexType { get; }

        /// <summary>
        /// Resolves the primitive type that corresponds to the given <see cref="BasicValueType"/>.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The created primitive type.</returns>
        PrimitiveType GetPrimitiveType(BasicValueType basicValueType);

        /// <summary>
        /// Creates an intrinsic index type.
        /// </summary>
        /// <param name="dimension">The dimension of the index type.</param>
        /// <returns>The created index type.</returns>
        StructureType GetIndexType(int dimension);

        /// <summary>
        /// Creates a pointer type.
        /// </summary>
        /// <param name="elementType">The pointer element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created pointer type.</returns>
        PointerType CreatePointerType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace);

        /// <summary>
        /// Creates a view type.
        /// </summary>
        /// <param name="elementType">The view element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created view type.</returns>
        ViewType CreateViewType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace);

        /// <summary>
        /// Creates a new generic view type that relies on an n-dimension index.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="indexType">The index type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created view type.</returns>
        StructureType CreateGenericViewType(
            TypeNode elementType,
            StructureType indexType,
            MemoryAddressSpace addressSpace);

        /// <summary>
        /// Creates a new array type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="length">The array length.</param>
        /// <returns>The created array type.</returns>
        ArrayType CreateArrayType(TypeNode elementType, int length);

        /// <summary>
        /// Creates a new structure type that implements array functionality.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="dimension">The array dimension.</param>
        /// <returns>The created implementation structure type.</returns>
        StructureType CreateArrayImplementationType(TypeNode elementType, int dimension);

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="baseType">The base type.</param>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <returns>The created structure type.</returns>
        StructureType CreateStructureType(
            StructureType baseType,
            ImmutableArray<TypeNode> fieldTypes);

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="baseType">The base type.</param>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <param name="fieldNames">The structure field names.</param>
        /// <param name="sourceType">The source structure type.</param>
        /// <returns>The created structure type.</returns>
        StructureType CreateStructureType(
            StructureType baseType,
            ImmutableArray<TypeNode> fieldTypes,
            ImmutableArray<string> fieldNames,
            Type sourceType);

        /// <summary>
        /// Creates a new structure type.
        /// </summary>
        /// <param name="fieldTypes">The structure field types.</param>
        /// <param name="sourceType">The source structure type.</param>
        /// <returns>The created structure type.</returns>
        StructureType CreateStructureType(
            ImmutableArray<TypeNode> fieldTypes,
            StructureType sourceType);

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The IR type.</returns>
        TypeNode CreateType(Type type);

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <param name="addressSpace">The address space for pointer types.</param>
        /// <returns>The IR type.</returns>
        TypeNode CreateType(Type type, MemoryAddressSpace addressSpace);

        /// <summary>
        /// Specializes the address space of the given <see cref="AddressSpaceType"/>.
        /// </summary>
        /// <param name="addressSpaceType">The source type.</param>
        /// <param name="addressSpace">The new address space.</param>
        /// <returns>The created specialzized <see cref="AddressSpaceType"/>.</returns>
        AddressSpaceType SpecializeAddressSpaceType(
            AddressSpaceType addressSpaceType,
            MemoryAddressSpace addressSpace);

        /// <summary>
        /// Tries to specialize a view or a pointer address space.
        /// </summary>
        /// <param name="type">The pointer or view type.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <param name="specializedType">The specialized type.</param>
        /// <returns>True, iff the type could be specialized.</returns>
        bool TrySpecializeAddressSpaceType(
            TypeNode type,
            MemoryAddressSpace addressSpace,
            out TypeNode specializedType);
    }
}
