// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Memory.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a node that represents a reference to an undefined memory value.
        /// </summary>
        /// <returns>A reference to the requested value.</returns>
        public MemoryRef CreateUndefMemoryReference() =>
            CreateMemoryReference(CreateUndef(MemoryType));

        /// <summary>
        /// Creates a new parent memory reference.
        /// </summary>
        /// <param name="parent">The parent node.</param>
        /// <returns>A node that represents the parent-memory reference.</returns>
        public MemoryRef CreateMemoryReference(Value parent)
        {
            Debug.Assert(parent != null, "Invalid parent node");
            Debug.Assert(MemoryRef.IsMemoryChainMember(parent), "Invalid parent memory value");

            // Compaction of nested chains
            if (parent is MemoryRef parentMemoryValue)
                return parentMemoryValue;

            return Context.CreateInstantiated(new MemoryRef(
                Generation,
                parent,
                MemoryType));
        }

        /// <summary>
        /// Creates a local allocation.
        /// </summary>
        /// <param name="parentMemoryValue">The parent memory operation.</param>
        /// <param name="type">The type of the allocation.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>A node that represents the alloca operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateAlloca(
            MemoryRef parentMemoryValue,
            TypeNode type,
            MemoryAddressSpace addressSpace)
        {
            return CreateAlloca(
                parentMemoryValue,
                CreatePrimitiveValue(1),
                type,
                addressSpace);
        }

        /// <summary>
        /// Creates a local allocation.
        /// </summary>
        /// <param name="parentMemoryValue">The parent memory operation.</param>
        /// <param name="arrayLength">The array length (number of elements to allocate).</param>
        /// <param name="type">The type of the allocation.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>A node that represents the alloca operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateAlloca(
            MemoryRef parentMemoryValue,
            Value arrayLength,
            TypeNode type,
            MemoryAddressSpace addressSpace)
        {
            Debug.Assert(parentMemoryValue != null, "Invalid parent memory reference");
            Debug.Assert(arrayLength != null, "Invalid array length");
            Debug.Assert(arrayLength.IsInstantiatedConstant(), "Invalid array length constant");
            Debug.Assert(type != null, "Invalid alloca type");

            switch (addressSpace)
            {
                case MemoryAddressSpace.Local:
                case MemoryAddressSpace.Shared:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(addressSpace));
            }

            return Context.CreateInstantiated(new Alloca(
                Generation,
                parentMemoryValue,
                arrayLength,
                CreatePointerType(type, addressSpace),
                addressSpace));
        }

        /// <summary>
        /// Creates a load operation.
        /// </summary>
        /// <param name="parentMemoryValue">The parent memory operation.</param>
        /// <param name="source">The source address.</param>
        /// <returns>A node that represents the load operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateLoad(
            MemoryRef parentMemoryValue,
            Value source)
        {
            Debug.Assert(parentMemoryValue != null, "Invalid parent memory reference");
            Debug.Assert(source != null, "Invalid source value");
            Debug.Assert(source.Type.IsPointerType, "Invalid source pointer type");

            return Context.CreateInstantiated(new Load(
                Generation,
                parentMemoryValue,
                source));
        }

        /// <summary>
        /// Creates a store operation.
        /// </summary>
        /// <param name="parentMemoryValue">The parent memory operation.</param>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>A node that represents the store operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateStore(
            MemoryRef parentMemoryValue,
            Value target,
            Value value)
        {
            Debug.Assert(parentMemoryValue != null, "Invalid parent memory reference");
            Debug.Assert(target != null, "Invalid target value");
            Debug.Assert(target.Type.IsPointerType, "Invalid target pointer type");
            Debug.Assert(
                target.Type is PointerType pointerType && pointerType.ElementType == value.Type,
                "Not compatible element types");

            return Context.CreateInstantiated(new Store(
                Generation,
                parentMemoryValue,
                target,
                value));
        }

        /// <summary>
        /// Creates a memory barrier.
        /// </summary>
        /// <param name="parentMemoryValue">The parent memory operation.</param>
        /// <param name="kind">The type of the memory barrier.</param>
        /// <returns>A node that represents the memory barrier.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateMemoryBarrier(
            MemoryRef parentMemoryValue,
            MemoryBarrierKind kind)
        {
            Debug.Assert(parentMemoryValue != null, "Invalid parent memory reference");

            return Context.CreateInstantiated(new MemoryBarrier(
                Generation,
                parentMemoryValue,
                kind,
                VoidType));
        }

        /// <summary>
        /// Computes a new sub view from a given view.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>A node that represents the new sub view.</returns>
        public ValueReference CreateSubViewValue(
            Value source,
            Value offset,
            Value length)
        {
            Debug.Assert(source != null, "Invalid source value");
            Debug.Assert(offset != null, "Invalid offset value");
            Debug.Assert(length != null, "Invalid length value");

            Debug.Assert(source.Type.IsViewType, "Invalid source view type");
            Debug.Assert(offset.BasicValueType == IRContext.ViewIndexType, "Invalid offset type");
            Debug.Assert(length.BasicValueType == IRContext.ViewIndexType, "Invalid length type");

            return CreateUnifiedValue(new SubViewValue(
                Generation,
                source,
                offset,
                length));
        }

        /// <summary>
        /// Computes the address of a single element in the scope of a view or a pointer.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <param name="elementIndex">The element index to load.</param>
        /// <returns>A node that represents the element address.</returns>
        public ValueReference CreateLoadElementAddress(
            Value source,
            Value elementIndex)
        {
            Debug.Assert(source != null, "Invalid source value");
            Debug.Assert(elementIndex != null, "Invalid element index");

            var addressSpaceType = source.Type as AddressSpaceType;
            Debug.Assert(addressSpaceType != null, "Invalid address space type");
            Debug.Assert(elementIndex.BasicValueType == IRContext.ViewIndexType, "Incompatible index type");

            // Fold primitive pointer arithmetic that does not change anything
            if (source.Type is PointerType &&
                elementIndex is PrimitiveValue value &&
                value.RawValue == 0)
                return source;

            var pointerType = CreatePointerType(
                addressSpaceType.ElementType,
                addressSpaceType.AddressSpace);
            return CreateUnifiedValue(new LoadElementAddress(
                Generation,
                source,
                elementIndex,
                pointerType));
        }

        /// <summary>
        /// Computes the address of a single field.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="fieldIndex">The field index to load.</param>
        /// <returns>A node that represents the field address.</returns>
        public ValueReference CreateLoadFieldAddress(
            Value source,
            int fieldIndex)
        {
            Debug.Assert(source != null, "Invalid source value");

            var pointerType = source.Type as PointerType;
            Debug.Assert(pointerType != null, "Invalid source pointer type");

            var structureType = pointerType.ElementType as StructureType;
            Debug.Assert(structureType != null, "Invalid pointer to a non-struct type");
            Debug.Assert(fieldIndex >= 0 || fieldIndex < structureType.NumChildren, "Invalid field index");

            var fieldPointerType = CreatePointerType(
                structureType.Children[fieldIndex],
                pointerType.AddressSpace);
            return Context.CreateInstantiated(new LoadFieldAddress(
                Generation,
                source,
                fieldPointerType,
                fieldIndex));
        }
    }
}
