// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Memory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Diagnostics.CodeAnalysis;

namespace ILGPUC.IR.BasicBlockValues.Construction;

partial class BasicBlockBuilder
{
    /// <summary>
    /// Creates a <see cref="GCInit"/> node that marks the point in this block where the
    /// managed object backed by <paramref name="global"/> is initialized.
    /// </summary>
    /// <param name="location">The current source location.</param>
    /// <param name="global">
    /// The module-level <see cref="Global"/> that holds the per-thread Local memory
    /// for the object being initialized.
    /// </param>
    /// <returns>The new <see cref="GCInit"/> node.</returns>
    public GCInit? CreateGCInit(Location location, Global? global)
    {
        if (global is null) return null;
        return Append(new GCInit(GetInitializer(location), global));
    }

    /// <summary>
    /// Creates a local allocation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="type">The type of the allocation.</param>
    /// <param name="arrayLength">
    /// The array length (number of elements to allocate or undefined).
    /// </param>
    /// <returns>A node that represents the alloca operation.</returns>
    public Alloca? CreateAlloca(
        Location location,
        TypeValue type,
        Value? arrayLength = null)
    {
        arrayLength ??= CreatePrimitiveValue(location, 1);

        if (arrayLength is PrimitiveValue primitiveValue)
        {
            if (primitiveValue.HasIntValue(0L))
                return null;

            var box = primitiveValue.Box;
            return CreateAlloca(location, type, box);
        }

        return Append(new Alloca(
            GetInitializer(location),
                arrayLengthValue: arrayLength,
                arrayLength: null,
                type));
    }

    /// <summary>
    /// Creates a local allocation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="type">The type of the allocation.</param>
    /// <param name="arrayLength">
    /// The array length (number of elements to allocate or undefined).
    /// </param>
    /// <returns>A node that represents the alloca operation.</returns>
    public Alloca? CreateAlloca(
        Location location,
        TypeValue type,
        PrimitiveValueBox? arrayLength)
    {
        if (!arrayLength.HasValue) return null;
        if (arrayLength.Value.HasIntValue(0L)) return null;

        return Append(new Alloca(
            GetInitializer(location),
                arrayLengthValue: null,
                arrayLength: arrayLength,
                type));
    }

    /// <summary>
    /// Creates a load operation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="source">The source address.</param>
    /// <returns>A node that represents the load operation.</returns>
    [return: NotNullIfNotNull(nameof(source))]
    public BasicBlockValue? CreateLoad(Location location, Value? source)
    {
        if (source is null) return null;

        location.Assert(source.Type is PointerType);

        return Append(new Load(GetInitializer(location), source));
    }

    /// <summary>
    /// Creates a store operation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="target">The target address.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>A node that represents the store operation.</returns>
    public BasicBlockValue? CreateStore(Location location, Value? target, Value? value)
    {
        if (target is null || value is null) return null;

        location.Assert(
            target.Type is PointerType pointerType &&
            pointerType.ElementType.Equals(value.Type));

        return Append(new Store(GetInitializer(location), target, value));
    }

    /// <summary>
    /// Creates a memory barrier.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="kind">The type of the memory barrier.</param>
    /// <returns>A node that represents the memory barrier.</returns>
    public BasicBlockValue CreateMemoryBarrier(
        Location location,
        MemoryBarrierKind kind) =>
        Append(new MemoryBarrier(GetInitializer(location), kind));

    /// <summary>
    /// Creates a new atomic operation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="target">The target address.</param>
    /// <param name="value">The target value.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="flags">The operation flags.</param>
    /// <returns>A node that represents the atomic operation.</returns>
    public Value? CreateAtomic(
        Location location,
        Value? target,
        Value? value,
        GenericAtomicKind kind,
        AtomicFlags flags)
    {
        if (target is null || value is null) return null;

        location.Assert(
            target.Type is PointerType type &&
            type.ElementType.Equals(value.Type));

        return Append(new GenericAtomic(
            GetInitializer(location),
            target,
            value,
            kind,
            flags));
    }

    /// <summary>
    /// Creates a new atomic compare-and-swap operation
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="target">The parent memory operation.</param>
    /// <param name="value">The target value.</param>
    /// <param name="compareValue">The comparison value.</param>
    /// <param name="flags">The operation flags.</param>
    /// <returns>
    /// A node that represents the atomic compare-and-swap operation.
    /// </returns>
    public Value? CreateAtomicCAS(
        Location location,
        Value? target,
        Value? value,
        Value? compareValue,
        AtomicFlags flags)
    {
        if (target is null || value is null || compareValue is null) return null;

        location.Assert(
            target.Type is PointerType type &&
            type.ElementType.Equals(value.Type) &&
            value.Type.Equals(compareValue.Type));

        return Append(new AtomicCAS(
            GetInitializer(location),
            target,
            value,
            compareValue,
            flags));
    }

    /// <summary>
    /// Creates a custom atomic operation driven by a user-provided binary
    /// operation lambda. Lowered to a CAS loop by
    /// <see cref="IR.Transformations.LowerCustomAtomic"/>.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="target">The pointer to the atomic target.</param>
    /// <param name="value">The value passed to the operation lambda.</param>
    /// <param name="operation">
    /// The IR <see cref="Method"/> representing the user's binary operation.
    /// </param>
    /// <param name="flags">The operation flags.</param>
    /// <returns>A node that represents the custom atomic operation.</returns>
    public Value? CreateCustomAtomic(
        Location location,
        Value? target,
        Value? value,
        Value? operation,
        AtomicFlags flags)
    {
        if (target is null || value is null || operation is null) return null;

        location.Assert(
            target.Type is PointerType type &&
            type.ElementType.Equals(value.Type));

        return Append(new CustomAtomic(
            GetInitializer(location),
            target,
            value,
            operation,
            flags));
    }
}
