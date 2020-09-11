// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AddressSpaces.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An analysis to determine safe address-space information for all values.
    /// </summary>
    public sealed class PointerAddressSpaces
    {
        #region Nested Types

        /// <summary>
        /// Represents different address spaces that can coexist via flags.
        /// </summary>
        [Flags]
        public enum AddressSpaceFlags : int
        {
            /// <summary>
            /// No specific address spaces.
            /// </summary>
            None = 0,

            /// <summary cref="MemoryAddressSpace.Generic"/>
            Generic = 1 << MemoryAddressSpace.Generic,

            /// <summary cref="MemoryAddressSpace.Global"/>
            Global = 1 << MemoryAddressSpace.Global,

            /// <summary cref="MemoryAddressSpace.Shared"/>
            Shared = 1 << MemoryAddressSpace.Shared,

            /// <summary cref="MemoryAddressSpace.Local"/>
            Local = 1 << MemoryAddressSpace.Local,
        }

        /// <summary>
        /// An internal address-space information object used to manage
        /// <see cref="AddressSpaceFlags"/> flags.
        /// </summary>
        public readonly struct AddressSpaceInfo : IEquatable<AddressSpaceInfo>
        {
            #region Nested Types

            /// <summary>
            /// Iterates over all internally stored address spaces.
            /// </summary>
            public struct Enumerator : IEnumerator<MemoryAddressSpace>
            {
                #region Instance

                private int index;

                internal Enumerator(in AddressSpaceInfo info)
                {
                    Info = info;
                    Current = MemoryAddressSpace.Generic;

                    index = -1;
                }

                #endregion

                #region Properties

                /// <summary>
                /// Returns the parent flags.
                /// </summary>
                public AddressSpaceInfo Info { get; }

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary>
                /// Returns the current address space.
                /// </summary>
                public MemoryAddressSpace Current { get; private set; }

                #endregion

                #region Methods

                /// <summary cref="IDisposable.Dispose"/>
                void IDisposable.Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext()
                {
                    do
                    {
                        Current = (MemoryAddressSpace)(++index);
                        if (Info.HasAddressSpace(Current))
                            return true;
                    }
                    while (index <= (int)MemoryAddressSpace.Local);
                    return false;
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();

                #endregion
            }

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new address-space information object.
            /// </summary>
            /// <param name="flags">The associated flags.</param>
            public AddressSpaceInfo(AddressSpaceFlags flags)
            {
                Flags = flags;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the underlying address-space flags.
            /// </summary>
            public AddressSpaceFlags Flags { get; }

            /// <summary>
            /// Returns the most generic address space that is compatible with all
            /// internally gathered address spaces.
            /// </summary>
            public MemoryAddressSpace UnifiedAddressSpace =>
                Flags == AddressSpaceFlags.None || !Utilities.IsPowerOf2((int)Flags)
                ? MemoryAddressSpace.Generic
                : HasFlags(AddressSpaceFlags.Global)
                ? MemoryAddressSpace.Global
                : HasFlags(AddressSpaceFlags.Shared)
                ? MemoryAddressSpace.Shared
                : HasFlags(AddressSpaceFlags.Local)
                ? MemoryAddressSpace.Local
                : MemoryAddressSpace.Generic;

            #endregion

            #region Methods

            /// <summary>
            /// Returns true if the given flags are set.
            /// </summary>
            /// <param name="flags">The flags to test.</param>
            /// <returns>True, if the given flags are set.</returns>
            public readonly bool HasFlags(AddressSpaceFlags flags) =>
                (Flags & flags) == flags;

            /// <summary>
            /// Returns true if the current instance is associated with the given
            /// address space.
            /// </summary>
            /// <param name="addressSpace">The address space to test.</param>
            /// <returns>
            /// True, if the current instance is associated with the given address space.
            /// </returns>
            public readonly bool HasAddressSpace(MemoryAddressSpace addressSpace) =>
                HasFlags((AddressSpaceFlags)(1 << (int)addressSpace));

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true if the given info object is equal to the current instance.
            /// </summary>
            /// <param name="other">The other info object.</param>
            /// <returns>
            /// True, if the given info object is equal to the current instance.
            /// </returns>
            public readonly bool Equals(AddressSpaceInfo other) =>
                Flags == other.Flags;

            #endregion

            #region IEnumerable

            /// <summary>
            /// Returns an enumerator to iterate over all address spaces.
            /// </summary>
            /// <returns>The enumerator instance.</returns>
            public readonly Enumerator GetEnumerator() => new Enumerator(this);

            #endregion

            #region Object

            /// <summary>
            /// Returns true if the given object is equal to the current instance.
            /// </summary>
            /// <param name="obj">The other object.</param>
            /// <returns>
            /// True, if the given object is equal to the current instance.
            /// </returns>
            public override readonly bool Equals(object obj) =>
                obj is AddressSpaceInfo info && Equals(info);

            /// <summary>
            /// Returns the hash code of this instance.
            /// </summary>
            /// <returns>The hash code of this instance.</returns>
            public override readonly int GetHashCode() => (int)Flags;

            #endregion

            #region Operators

            /// <summary>
            /// Converts nullable <see cref="MemoryAddressSpace"/> values to information
            /// instances.
            /// </summary>
            /// <param name="addressSpace">The address space to convert.</param>
            public static implicit operator AddressSpaceInfo(
                MemoryAddressSpace? addressSpace) =>
                !addressSpace.HasValue
                ? new AddressSpaceInfo()
                : new AddressSpaceInfo(
                    (AddressSpaceFlags)(1 << (int)addressSpace.Value));

            /// <summary>
            /// Returns true if the first and second information instances are the same.
            /// </summary>
            /// <param name="first">The first instance.</param>
            /// <param name="second">The second instance.</param>
            /// <returns>True, if the first and second instances are the same.</returns>
            public static bool operator ==(
                AddressSpaceInfo first,
                AddressSpaceInfo second) =>
                first.Equals(second);

            /// <summary>
            /// Returns true if the first and second information instances are not the
            /// same.
            /// </summary>
            /// <param name="first">The first instance.</param>
            /// <param name="second">The second instance.</param>
            /// <returns>
            /// True, if the first and second instances are not the same.
            /// </returns>
            public static bool operator !=(
                AddressSpaceInfo first,
                AddressSpaceInfo second) =>
                !first.Equals(second);

            #endregion
        }

        /// <summary>
        /// Models the internal address-space analysis.
        /// </summary>
        private sealed class AnalysisImplementation :
            GlobalFixPointAnalysis<AddressSpaceInfo, Forwards>
        {
            /// <summary>
            /// Constructs a new analysis implementation.
            /// </summary>
            /// <param name="globalAddressSpace">
            /// The global address space for all input parameters.
            /// </param>
            public AnalysisImplementation(MemoryAddressSpace globalAddressSpace)
                : base(
                      defaultValue: default,
                      initialValue: globalAddressSpace)
            { }

            /// <summary>
            /// Returns initial and address space information.
            /// </summary>
            /// <param name="node">The IR node.</param>
            /// <returns>The initial address space information.</returns>
            private static AddressSpaceInfo GetInitialSpace(Value node) =>
                node.Type is IAddressSpaceType addressSpaceType
                ? addressSpaceType.AddressSpace
                : new AddressSpaceInfo();

            /// <summary>
            /// Creates initial analysis data.
            /// </summary>
            protected override AnalysisValue<AddressSpaceInfo> CreateData(Value node) =>
                Create(GetInitialSpace(node), node.Type);

            /// <summary>
            /// Returns the unified address-space flags.
            /// </summary>
            protected override AddressSpaceInfo Merge(
                AddressSpaceInfo first,
                AddressSpaceInfo second) =>
                new AddressSpaceInfo(first.Flags | second.Flags);

            /// <summary>
            /// Returns no analysis value.
            /// </summary>
            protected override AnalysisValue<AddressSpaceInfo>? TryMerge<TContext>(
                Value value,
                TContext context) => null;

            protected override AnalysisValue<AddressSpaceInfo>?
                TryProvide(TypeNode typeNode) =>
                typeNode is IAddressSpaceType spaceType
                ? Create(spaceType.AddressSpace, typeNode)
                : default;
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new address space analysis.
        /// </summary>
        /// <param name="rootMethod">The root (entry) method.</param>
        /// <param name="globalAddressSpace">
        /// The initial address space information of all pointers and views of the root
        /// method.
        /// </param>
        public static PointerAddressSpaces Create(
            Method rootMethod,
            MemoryAddressSpace globalAddressSpace) =>
            new PointerAddressSpaces(rootMethod, globalAddressSpace);

        #endregion

        #region Instance

        /// <summary>
        /// Stores a method value-address-space mapping.
        /// </summary>
        private readonly Dictionary<
            Method,
            AnalysisValueMapping<AddressSpaceInfo>> addressSpaces;

        /// <summary>
        /// Constructs a new address-spaces analysis.
        /// </summary>
        private PointerAddressSpaces(
            Method rootMethod,
            MemoryAddressSpace globalAddressSpace)
        {
            var impl = new AnalysisImplementation(globalAddressSpace);
            addressSpaces = impl.AnalyzeGlobal(rootMethod);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns address-space information for the given value.
        /// </summary>
        /// <param name="value">The value to get the information for.</param>
        /// <returns>Address-space information.</returns>
        public AddressSpaceInfo this[Value value] =>
            addressSpaces.TryGetValue(value.Method, out var mapping) &&
            mapping.TryGetValue(value, out var info)
            ? info.Data
            : default;

        #endregion
    }
}
