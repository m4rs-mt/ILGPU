// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IIRTypeContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
        /// Resolves the primitive type that corresponds to the given
        /// <see cref="BasicValueType"/>.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The created primitive type.</returns>
        PrimitiveType GetPrimitiveType(BasicValueType basicValueType);

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
        /// Creates a new array type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="dimensions">The number of array dimensions.</param>
        /// <returns>The created array dimensions.</returns>
        ArrayType CreateArrayType(TypeNode elementType, int dimensions);

        /// <summary>
        /// Creates a new structure type builder with the given capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        /// <returns>The created structure builder.</returns>
        StructureType.Builder CreateStructureType(int capacity);

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
        /// <returns>The created specialized <see cref="AddressSpaceType"/>.</returns>
        AddressSpaceType SpecializeAddressSpaceType(
            AddressSpaceType addressSpaceType,
            MemoryAddressSpace addressSpace);

        /// <summary>
        /// Tries to specialize a view or a pointer address space.
        /// </summary>
        /// <param name="type">The pointer or view type.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <param name="specializedType">The specialized type.</param>
        /// <returns>True, if the type could be specialized.</returns>
        bool TrySpecializeAddressSpaceType(
            TypeNode type,
            MemoryAddressSpace addressSpace,
            [NotNullWhen(true)] out TypeNode? specializedType);
    }

    /// <summary>
    /// Extension methods for <see cref="IIRTypeContext"/> instances.
    /// </summary>
    public static class IRTypeContextExtensions
    {
        /// <summary>
        /// Creates an empty structure type.
        /// </summary>
        /// <typeparam name="TTypeContext">the parent type context.</typeparam>
        /// <param name="typeContext">The type context.</param>
        /// <returns>The type representing an empty structure.</returns>
        public static TypeNode CreateEmptyStructureType<TTypeContext>(
            this TTypeContext typeContext)
            where TTypeContext : IIRTypeContext =>
            typeContext.GetPrimitiveType(BasicValueType.Int8);

        /// <summary>
        /// Creates a new structure type builder with an initial capacity.
        /// </summary>
        /// <typeparam name="TTypeContext">the parent type context.</typeparam>
        /// <param name="typeContext">The type context.</param>
        /// <returns>The created structure builder.</returns>
        public static StructureType.Builder CreateStructureType<TTypeContext>(
            this TTypeContext typeContext)
            where TTypeContext : IIRTypeContext => typeContext.CreateStructureType(2);
    }
}
