// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AddressSpace.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents an address space.
    /// </summary>
    public enum MemoryAddressSpace : int
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
        Local = 3
    }

    /// <summary>
    /// Represents the base interface for all address spaces.
    /// </summary>
    public interface IAddressSpace { }

    /// <summary>
    /// Represents an address-space annotation.
    /// </summary>
    [CLSCompliant(false)]
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class AddressSpaceAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new address-space attribute.
        /// </summary>
        /// <param name="addressSpace">The target address space.</param>
        internal AddressSpaceAttribute(MemoryAddressSpace addressSpace)
        {
            AddressSpace = addressSpace;
        }

        /// <summary>
        /// Returns the associated address space.
        /// </summary>
        public MemoryAddressSpace AddressSpace { get; }
    }

    /// <summary>
    /// Extensions to encode ILGPU address space information in the .Net type
    /// system environment.
    /// </summary>
    public static class AddressSpaces
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

        /// <summary>
        /// Represents the generic address space.
        /// </summary>
        [AddressSpace(MemoryAddressSpace.Generic)]
        internal struct Generic : IAddressSpace { }

        /// <summary>
        /// Represents the global address space.
        /// </summary>
        [AddressSpace(MemoryAddressSpace.Global)]
        internal struct Global : IAddressSpace { }

        /// <summary>
        /// Represents the shared address space.
        /// </summary>
        [AddressSpace(MemoryAddressSpace.Shared)]
        internal struct Shared : IAddressSpace { }

        /// <summary>
        /// Represents the local address space.
        /// </summary>
        [AddressSpace(MemoryAddressSpace.Local)]
        internal struct Local : IAddressSpace { }

        /// <summary>
        /// Resolves the managed type for the given address space.
        /// </summary>
        /// <param name="space">The address space.</param>
        /// <returns>The .Net representation of the given address space.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type GetManagedType(this MemoryAddressSpace space) =>
            space switch
            {
                MemoryAddressSpace.Global => typeof(Global),
                MemoryAddressSpace.Local => typeof(Local),
                MemoryAddressSpace.Shared => typeof(Shared),
                _ => typeof(Generic),
            };

        /// <summary>
        /// Resolves the address-space type for the given .Net type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The resolved address space.</returns>
        public static MemoryAddressSpace GetAddressSpaceType(this Type type)
        {
            var attr = type.GetCustomAttribute<AddressSpaceAttribute>();
            return attr != null
                ? attr.AddressSpace
                : MemoryAddressSpace.Generic;
        }
    }
}
