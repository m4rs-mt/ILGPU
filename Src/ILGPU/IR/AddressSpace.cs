// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: AddressSpace.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents an address space.
    /// </summary>
    public enum MemoryAddressSpace
    {
        /// <summary>
        /// The generic address space (any space).
        /// </summary>
        Generic,

        /// <summary>
        /// Represents the global address space.
        /// </summary>
        Global,

        /// <summary>
        /// Represents the shared address space.
        /// </summary>
        Shared,

        /// <summary>
        /// Represents the local address space.
        /// </summary>
        Local
    }
    /// <summary>
    /// Represents the base interface for all address spaces.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1040:AvoidEmptyInterfaces", Justification = "It is used as a type constraint")]
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
    /// Contains pre-defined address spaces.
    /// </summary>
    internal struct AddressSpaces
    {
        /// <summary>
        /// Represents the generic address space.
        /// </summary>
        [AddressSpace(MemoryAddressSpace.Generic)]
        public struct Generic : IAddressSpace { }

        /// <summary>
        /// Represents the global address space.
        /// </summary>
        [AddressSpace(MemoryAddressSpace.Global)]
        public struct Global : IAddressSpace { }

        /// <summary>
        /// Represents the shared address space.
        /// </summary>
        [AddressSpace(MemoryAddressSpace.Shared)]
        public struct Shared : IAddressSpace { }

        /// <summary>
        /// Represents the local address space.
        /// </summary>
        [AddressSpace(MemoryAddressSpace.Local)]
        public struct Local : IAddressSpace { }
    }

    /// <summary>
    /// Extensions to encode ILGPU address space information in the .Net type system environment.
    /// </summary>
    public static class AddressSpaceExtensions
    {
        /// <summary>
        /// Resolves the managed type for the given address space.
        /// </summary>
        /// <param name="space">The address space.</param>
        /// <returns>The .Net representation of the given address space.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type GetManagedType(this MemoryAddressSpace space)
        {
            switch (space)
            {
                case MemoryAddressSpace.Global:
                    return typeof(AddressSpaces.Global);
                case MemoryAddressSpace.Local:
                    return typeof(AddressSpaces.Local);
                case MemoryAddressSpace.Shared:
                    return typeof(AddressSpaces.Shared);
                default:
                    return typeof(AddressSpaces.Generic);
            }
        }

        /// <summary>
        /// Resolves the address-space type for the given .Net type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The resolved address space.</returns>
        public static MemoryAddressSpace GetAddressSpaceType(this Type type)
        {
            var attr = type.GetCustomAttribute<AddressSpaceAttribute>();
            if (attr != null)
                return attr.AddressSpace;
            return MemoryAddressSpace.Generic;
        }
    }
}
