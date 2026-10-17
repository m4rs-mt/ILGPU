// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IAllocationValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR;

/// <summary>
/// An abstract allocation value.
/// </summary>
interface IAllocationValue
{
    /// <summary>
    /// Returns the allocation type.
    /// </summary>
    TypeValue AllocType { get; }

    /// <summary>
    /// Returns the address space of this allocation.
    /// </summary>
    MemoryAddressSpace AddressSpace { get; }

    /// <summary>
    /// Returns the array length.
    /// </summary>
    PrimitiveValueBox? ArrayLength { get; }

    /// <summary>
    /// Returns the array length value (if applicable).
    /// </summary>
    Value? ArrayLengthValue { get; }
}

/// <summary>
/// Contains extension methods for <see cref="IAllocationValue"/> values.
/// </summary>
static class AllocationValueExtensions
{
    /// <summary>
    /// Returns true if this allocation is a static array allocation.
    /// </summary>
    /// <param name="value">The allocation value.</param>
    /// <param name="arrayLength">The number of statically known elements.</param>
    public static bool IsStaticAllocation<T>(
        this T value,
        out PrimitiveValueBox arrayLength)
        where T : IAllocationValue
    {
        arrayLength = value.ArrayLength ?? default;
        return value.ArrayLength.HasValue;
    }
}
