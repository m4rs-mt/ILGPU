// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Types.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using System;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder : IIRTypeContext
    {
        /// <summary>
        /// Creates a primitive type.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The created primitive type.</returns>
        public PrimitiveType GetPrimitiveType(BasicValueType basicValueType) =>
            Context.GetPrimitiveType(basicValueType);

        /// <summary>
        /// Creates an intrinsic index type.
        /// </summary>
        /// <param name="dimension">The dimension of the index type.</param>
        /// <returns>The created index type.</returns>
        public TypeNode GetIndexType(int dimension) =>
            Context.GetIndexType(dimension);

        /// <summary>
        /// Creates a pointer type.
        /// </summary>
        /// <param name="elementType">The pointer element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created pointer type.</returns>
        public PointerType CreatePointerType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace) =>
            Context.CreatePointerType(elementType, addressSpace);

        /// <summary>
        /// Creates a view type.
        /// </summary>
        /// <param name="elementType">The view element type.</param>
        /// <param name="addressSpace">The address space.</param>
        /// <returns>The created view type.</returns>
        public ViewType CreateViewType(
            TypeNode elementType,
            MemoryAddressSpace addressSpace) =>
            Context.CreateViewType(elementType, addressSpace);

        /// <summary>
        /// Creates a new array type.
        /// </summary>
        /// <param name="elementType">The element type.</param>
        /// <param name="dimensions">The number of array dimensions.</param>
        /// <returns>The created array type.</returns>
        public TypeNode CreateArrayType(TypeNode elementType, int dimensions) =>
            Context.CreateArrayType(elementType, dimensions);

        /// <summary>
        /// Creates a new structure type builder with the given capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        /// <returns>The created structure builder.</returns>
        public StructureType.Builder CreateStructureType(int capacity) =>
            Context.CreateStructureType(capacity);

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The IR type.</returns>
        public TypeNode CreateType(Type type) =>
            CreateType(type, MemoryAddressSpace.Generic);

        /// <summary>
        /// Creates a new type based on a type from the .Net world.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <param name="addressSpace">The address space for pointer types.</param>
        /// <returns>The IR type.</returns>
        public TypeNode CreateType(Type type, MemoryAddressSpace addressSpace) =>
            Context.CreateType(type, addressSpace);

        /// <summary>
        /// Specializes the address space of the given <see cref="AddressSpaceType"/>.
        /// </summary>
        /// <param name="addressSpaceType">The source type.</param>
        /// <param name="addressSpace">The new address space.</param>
        /// <returns>The created specialized <see cref="AddressSpaceType"/>.</returns>
        public AddressSpaceType SpecializeAddressSpaceType(
            AddressSpaceType addressSpaceType,
            MemoryAddressSpace addressSpace) =>
            Context.SpecializeAddressSpaceType(addressSpaceType, addressSpace);

        /// <summary>
        /// Tries to specialize a view or a pointer address space.
        /// </summary>
        /// <param name="type">The pointer or view type.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <param name="specializedType">The specialized type.</param>
        /// <returns>True, if the type could be specialized.</returns>
        public bool TrySpecializeAddressSpaceType(
            TypeNode type,
            MemoryAddressSpace addressSpace,
            out TypeNode specializedType) =>
            Context.TrySpecializeAddressSpaceType(
                type,
                addressSpace,
                out specializedType);
    }
}
