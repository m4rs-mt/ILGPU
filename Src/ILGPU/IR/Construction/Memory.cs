// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
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
        /// Creates a local allocation.
        /// </summary>
        /// <param name="type">The type of the allocation.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>A node that represents the alloca operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateAlloca(
            TypeNode type,
            MemoryAddressSpace addressSpace)
        {
            return CreateAlloca(
                CreatePrimitiveValue(1),
                type,
                addressSpace);
        }

        /// <summary>
        /// Creates a local allocation.
        /// </summary>
        /// <param name="arrayLength">The array length (number of elements to allocate).</param>
        /// <param name="type">The type of the allocation.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>A node that represents the alloca operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateAlloca(
            Value arrayLength,
            TypeNode type,
            MemoryAddressSpace addressSpace)
        {
            Debug.Assert(arrayLength != null, "Invalid array length");
            Debug.Assert(type != null, "Invalid alloca type");

            switch (addressSpace)
            {
                case MemoryAddressSpace.Local:
                case MemoryAddressSpace.Shared:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(addressSpace));
            }

            return Append(new Alloca(
                Context,
                BasicBlock,
                arrayLength,
                type,
                addressSpace));
        }

        /// <summary>
        /// Creates a load operation.
        /// </summary>
        /// <param name="source">The source address.</param>
        /// <returns>A node that represents the load operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateLoad(Value source)
        {
            Debug.Assert(source != null, "Invalid source value");
            Debug.Assert(source.Type.IsPointerType, "Invalid source pointer type");

            return Append(new Load(
                Context,
                BasicBlock,
                source));
        }

        /// <summary>
        /// Creates a store operation.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>A node that represents the store operation.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateStore(Value target, Value value)
        {
            Debug.Assert(target != null, "Invalid target value");
            Debug.Assert(target.Type.IsPointerType, "Invalid target pointer type");
            Debug.Assert(
                target.Type is PointerType pointerType && pointerType.ElementType == value.Type,
                "Not compatible element types");

            return Append(new Store(
                Context,
                BasicBlock,
                target,
                value));
        }

        /// <summary>
        /// Creates a memory barrier.
        /// </summary>
        /// <param name="kind">The type of the memory barrier.</param>
        /// <returns>A node that represents the memory barrier.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateMemoryBarrier(
            MemoryBarrierKind kind)
        {
            return Append(new MemoryBarrier(
                Context,
                BasicBlock,
                kind));
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
            Debug.Assert(offset.BasicValueType == IRTypeContext.ViewIndexType, "Invalid offset type");
            Debug.Assert(length.BasicValueType == IRTypeContext.ViewIndexType, "Invalid length type");

            return Append(new SubViewValue(
                BasicBlock,
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
        public ValueReference CreateLoadElementAddress(Value source, Value elementIndex)
        {
            Debug.Assert(source != null, "Invalid source value");
            Debug.Assert(elementIndex != null, "Invalid element index");

            var addressSpaceType = source.Type as IAddressSpaceType;
            Debug.Assert(addressSpaceType != null, "Invalid address space type");
            Debug.Assert(elementIndex.BasicValueType == IRTypeContext.ViewIndexType, "Incompatible index type");

            // Fold primitive pointer arithmetic that does not change anything
            if (source.Type is PointerType && elementIndex.IsPrimitive(0))
                return source;

            return Append(new LoadElementAddress(
                Context,
                BasicBlock,
                source,
                elementIndex));
        }

        /// <summary>
        /// Computes the address of a single field.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="fieldSpan">The associated field span (if any).</param>
        /// <returns>A node that represents the field address.</returns>
        public ValueReference CreateLoadFieldAddress(Value source, FieldSpan fieldSpan)
        {
            Debug.Assert(source != null, "Invalid source value");

            var pointerType = source.Type as PointerType;
            Debug.Assert(pointerType != null, "Invalid source pointer type");

            // Simplify pseudo-structure accesses
            if (!pointerType.ElementType.IsStructureType && fieldSpan.Span < 2)
                return source;

            // Fold nested field addresses
            if (source is LoadFieldAddress lfa)
            {
                return CreateLoadFieldAddress(
                    lfa.Source,
                    lfa.FieldSpan.Narrow(fieldSpan));
            }

            return Append(new LoadFieldAddress(
                Context,
                BasicBlock,
                source,
                fieldSpan));
        }
    }
}
