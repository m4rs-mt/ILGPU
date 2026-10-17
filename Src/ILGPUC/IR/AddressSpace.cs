// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AddressSpace.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPUC.IR;

/// <summary>
/// Represents an address space.
/// </summary>
enum MemoryAddressSpace : int
{
    /// <summary>
    /// The generic address space (any space).
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Represents the global address space.
    /// </summary>
    Global = 1,

    /// <summary>
    /// Represents the shared address space.
    /// </summary>
    Shared = 2,

    /// <summary>
    /// Represents the local address space.
    /// </summary>
    Local = 3,

    /// <summary>
    /// Represents the constant address space.
    /// </summary>
    Constant = 4,
}

/// <summary>
/// Extensions to encode ILGPU address space information in the .Net type
/// system environment.
/// </summary>
static class AddressSpaces
{
    /// <summary>
    /// A readonly array of all address spaces.
    /// </summary>
    public static readonly ImmutableArray<MemoryAddressSpace> Spaces =
        ImmutableArray.Create(
            MemoryAddressSpace.Generic,
            MemoryAddressSpace.Global,
            MemoryAddressSpace.Local,
            MemoryAddressSpace.Shared);
}
