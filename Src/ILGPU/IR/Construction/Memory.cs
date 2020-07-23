// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Memory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a local allocation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The type of the allocation.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>A node that represents the alloca operation.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateAlloca(
            Location location,
            TypeNode type,
            MemoryAddressSpace addressSpace) =>
            CreateAlloca(
                location,
                type,
                addressSpace,
                CreateUndefined());

        /// <summary>
        /// Creates an array based local allocation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="arrayLength">
        /// The array length (number of elements to allocate).
        /// </param>
        /// <param name="type">The type of the allocation.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>A node that represents the alloca operation.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateStaticAllocaArray(
            Location location,
            Value arrayLength,
            TypeNode type,
            MemoryAddressSpace addressSpace) =>
            CreateAlloca(
                location,
                type,
                addressSpace,
                arrayLength);

        /// <summary>
        /// Creates a dynamic local memory allocation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The type of the allocation.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>A node that represents the alloca operation.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateDynamicAllocaArray(
            Location location,
            TypeNode type,
            MemoryAddressSpace addressSpace) =>
            CreateStaticAllocaArray(
                location,
                CreatePrimitiveValue(location, -1),
                type,
                addressSpace);

        /// <summary>
        /// Creates a local allocation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="type">The type of the allocation.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <param name="arrayLength">
        /// The array length (number of elements to allocate or undefined).
        /// </param>
        /// <returns>A node that represents the alloca operation.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        internal ValueReference CreateAlloca(
            Location location,
            TypeNode type,
            MemoryAddressSpace addressSpace,
            Value arrayLength)
        {
            location.Assert(
                addressSpace == MemoryAddressSpace.Local ||
                addressSpace == MemoryAddressSpace.Shared);

            return arrayLength is PrimitiveValue primitiveValue &&
                primitiveValue.Int32Value == 0
                ? CreateNull(
                    location,
                    CreatePointerType(type, addressSpace))
                : Append(new Alloca(
                    GetInitializer(location),
                    arrayLength,
                    type,
                    addressSpace));
        }

        /// <summary>
        /// Creates a load operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="source">The source address.</param>
        /// <returns>A node that represents the load operation.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateLoad(
            Location location,
            Value source)
        {
            location.Assert(source.Type.IsPointerType);

            return Append(new Load(
                GetInitializer(location),
                source));
        }

        /// <summary>
        /// Creates a store operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="target">The target address.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>A node that represents the store operation.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateStore(
            Location location,
            Value target,
            Value value)
        {
            location.Assert(
                target.Type is PointerType pointerType &&
                pointerType.ElementType == value.Type);

            return Append(new Store(
                GetInitializer(location),
                target,
                value));
        }

        /// <summary>
        /// Creates a memory barrier.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="kind">The type of the memory barrier.</param>
        /// <returns>A node that represents the memory barrier.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public ValueReference CreateMemoryBarrier(
            Location location,
            MemoryBarrierKind kind) =>
            Append(new MemoryBarrier(
                GetInitializer(location),
                kind));

        /// <summary>
        /// Computes a new sub view from a given view.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>A node that represents the new sub view.</returns>
        public ValueReference CreateSubViewValue(
            Location location,
            Value source,
            Value offset,
            Value length)
        {
            location.Assert(
                source.Type.IsViewType &&
                IRTypeContext.IsViewIndexType(offset.BasicValueType) &&
                IRTypeContext.IsViewIndexType(length.BasicValueType));

            return Append(new SubViewValue(
                GetInitializer(location),
                source,
                offset,
                length));
        }

        /// <summary>
        /// Computes the address of a single element in the scope of a view or a pointer.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="source">The source view.</param>
        /// <param name="elementIndex">The element index to load.</param>
        /// <returns>A node that represents the element address.</returns>
        public ValueReference CreateLoadElementAddress(
            Location location,
            Value source,
            Value elementIndex)
        {
            // Remove unnecessary pointer casts
            if (elementIndex is IntAsPointerCast cast)
                elementIndex = cast.Value;

            // Assert a valid indexing type from here on
            location.Assert(
                IRTypeContext.IsViewIndexType(elementIndex.BasicValueType));
            var addressSpaceType = source.Type as IAddressSpaceType;
            location.AssertNotNull(addressSpaceType);

            // Fold primitive pointer arithmetic that does not change anything
            return source.Type is PointerType && elementIndex.IsPrimitive(0)
                ? (ValueReference)source
                : Append(new LoadElementAddress(
                    GetInitializer(location),
                    source,
                    elementIndex));
        }

        /// <summary>
        /// Computes the address of a single field.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="source">The source.</param>
        /// <param name="fieldSpan">The associated field span (if any).</param>
        /// <returns>A node that represents the field address.</returns>
        public ValueReference CreateLoadFieldAddress(
            Location location,
            Value source,
            FieldSpan fieldSpan)
        {
            var pointerType = source.Type.As<PointerType>(location);

            // Simplify pseudo-structure accesses
            if (!pointerType.ElementType.IsStructureType && fieldSpan.Span < 2)
            {
                return source;
            }
            else if (pointerType.ElementType is StructureType structureType &&
                fieldSpan.Index == 0 && structureType.NumFields == fieldSpan.Span)
            {
                return source;
            }

            // Fold nested field addresses
            return source is LoadFieldAddress lfa
                ? CreateLoadFieldAddress(
                    location,
                    lfa.Source,
                    lfa.FieldSpan.Narrow(fieldSpan))
                : Append(new LoadFieldAddress(
                    GetInitializer(location),
                    source,
                    fieldSpan));
        }
    }
}
