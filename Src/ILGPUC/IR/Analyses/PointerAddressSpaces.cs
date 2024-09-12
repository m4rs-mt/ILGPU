// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerAddressSpaces.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An analysis to determine safe address-space information for all values.
    /// </summary>
    public class PointerAddressSpaces :
        GlobalFixPointAnalysis<PointerAddressSpaces.AddressSpaceInfo, Forwards>
    {
        #region Nested Types

        /// <summary>
        /// The analysis flags.
        /// </summary>
        [Flags]
        public enum AnalysisFlags : int
        {
            /// <summary>
            /// Performs a conservative analysis.
            /// </summary>
            None = 0 << 0,

            /// <summary>
            /// Ignores generic address-space types during the analysis.
            /// </summary>
            IgnoreGenericAddressSpace = 1 << 0,
        }

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

            #region Static

            /// <summary>
            /// Determines an address-space information instance based on type
            /// information.
            /// </summary>
            /// <param name="type">The source type.</param>
            /// <returns>
            /// The resolved address-space information for the given type.
            /// </returns>
            public static AddressSpaceInfo FromType(TypeNode type)
            {
                // Check for address-space dependencies
                if (!type.HasFlags(TypeFlags.AddressSpaceDependent))
                    return default;

                // Determine the unified address space of all elements
                if (type is AddressSpaceType addressSpaceType)
                    return addressSpaceType.AddressSpace;

                // Unify all address space flags from each dependent field
                var structureType = type.AsNotNullCast<StructureType>();
                var result = new AddressSpaceInfo();
                foreach (var (fieldType, _) in structureType)
                    result = Merge(result, FromType(fieldType));
                return result;
            }

            /// <summary>
            /// Creates an <see cref="AnalysisValue{T}"/> instance based on the given
            /// type information.
            /// information.
            /// </summary>
            /// <param name="type">The source type.</param>
            /// <returns>
            /// The resolved analysis value holding detailed address-space information
            /// for the given type.
            /// </returns>
            public static AnalysisValue<AddressSpaceInfo> AnalysisValueFromType(
                TypeNode type)
            {
                var unifiedAddressSpace = FromType(type);
                if (unifiedAddressSpace.Flags != AddressSpaceFlags.None &&
                    type is StructureType structureType)
                {
                    var childData = new AddressSpaceInfo[structureType.NumFields];
                    foreach (var (fieldType, fieldAccess) in structureType)
                        childData[fieldAccess.Index] = FromType(fieldType);
                    return new AnalysisValue<AddressSpaceInfo>(
                        unifiedAddressSpace,
                        childData);
                }
                return AnalysisValue.Create(unifiedAddressSpace, type);
            }

            /// <summary>
            /// Merges two information objects.
            /// </summary>
            /// <param name="first">The first info object.</param>
            /// <param name="second">The second info object.</param>
            /// <returns>
            /// Merged address space information based on both operands.
            /// </returns>
            public static AddressSpaceInfo Merge(
                AddressSpaceInfo first,
                AddressSpaceInfo second) =>
                new AddressSpaceInfo(first.Flags | second.Flags);

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
            public override readonly bool Equals(object? obj) =>
                obj is AddressSpaceInfo info && Equals(info);

            /// <summary>
            /// Returns the hash code of this instance.
            /// </summary>
            /// <returns>The hash code of this instance.</returns>
            public override readonly int GetHashCode() => (int)Flags;

            /// <summary>
            /// Returns the string representation of this instance.
            /// </summary>
            /// <returns>The string representation of this instance.</returns>
            public override readonly string ToString() =>
                Flags == AddressSpaceFlags.None
                ? "<None>"
                : UnifiedAddressSpace.ToString();

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
        /// An implementation of an <see cref="IAnalysisValueSourceContext{T}"/> that
        /// provides initial <see cref="AddressSpaceInfo"/> information for each
        /// parameter.
        /// </summary>
        public readonly struct ConstParameterValueContext :
            IAnalysisValueSourceContext<AddressSpaceInfo>
        {
            /// <summary>
            /// Constructs a new parameter value context.
            /// </summary>
            /// <param name="addressSpace">
            /// The target address space to use for each parameter.
            /// </param>
            public ConstParameterValueContext(MemoryAddressSpace addressSpace)
            {
                AddressSpace = addressSpace;
            }

            /// <summary>
            /// Returns the target address space to use for each parameter.
            /// </summary>
            public MemoryAddressSpace AddressSpace { get; }

            /// <summary>
            /// Returns the initial <see cref="AddressSpaceInfo"/> for the given value.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private AddressSpaceInfo GetInitialAddressSpace(Value value) =>
                value is Parameter parameter &&
                parameter.Type.HasFlags(TypeFlags.AddressSpaceDependent)
                ? AddressSpace
                : default(AddressSpaceInfo);

            /// <summary>
            /// Returns address-space information based on <see cref="AddressSpace"/>
            /// in the case of a parameter.
            /// </summary>
            public readonly AnalysisValue<AddressSpaceInfo> this[Value value] =>
                AnalysisValue.Create(GetInitialAddressSpace(value), value.Type);
        }

        /// <summary>
        /// An implementation of an <see cref="IAnalysisValueSourceContext{T}"/> that
        /// provides initial <see cref="AddressSpaceInfo"/> information for each
        /// parameter using an automated deduction phase.
        /// </summary>
        public readonly struct AutomaticParameterValueContext :
            IAnalysisValueSourceContext<AddressSpaceInfo>
        {
            /// <summary>
            /// Returns <see cref="AddressSpaceInfo"/> instances based on the
            /// automatically determined address-space type information from each
            /// parameter.
            /// </summary>
            public readonly AnalysisValue<AddressSpaceInfo> this[Value value] =>
                AddressSpaceInfo.AnalysisValueFromType(value.Type);
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new pointer analysis instance using the default analysis flags.
        /// </summary>
        /// <returns>The created analysis instance.</returns>
        public static PointerAddressSpaces Create() =>
            Create(AnalysisFlags.None);

        /// <summary>
        /// Creates a new pointer analysis instance.
        /// </summary>
        /// <param name="flags">The analysis flags.</param>
        /// <returns>The created analysis instance.</returns>
        public static PointerAddressSpaces Create(AnalysisFlags flags) =>
            new PointerAddressSpaces(flags);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new analysis implementation.
        /// </summary>
        /// <param name="flags">The analysis flags.</param>
        protected PointerAddressSpaces(AnalysisFlags flags)
            : base(defaultValue: default)
        {
            Flags = flags;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current analysis flags.
        /// </summary>
        public AnalysisFlags Flags { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the analysis has the given flags.
        /// </summary>
        /// <param name="flags">The flags.</param>
        /// <returns>True, if the analysis has the given flags.</returns>
        protected bool HasFlags(AnalysisFlags flags) =>
            (Flags & flags) != AnalysisFlags.None;

        /// <summary>
        /// Returns initial and address space information.
        /// </summary>
        /// <param name="node">The IR node.</param>
        /// <returns>The initial address space information.</returns>
        protected AddressSpaceInfo GetInitialInfo(Value node)
        {
            if (!(node.Type is AddressSpaceType type))
                return default;
            return
                type.AddressSpace != MemoryAddressSpace.Generic ||
                !HasFlags(AnalysisFlags.IgnoreGenericAddressSpace)
                ? type.AddressSpace
                : default(AddressSpaceInfo);
        }

        /// <summary>
        /// Creates initial analysis data.
        /// </summary>
        protected override AnalysisValue<AddressSpaceInfo> CreateData(Value node) =>
            CreateValue(GetInitialInfo(node), node.Type);

        /// <summary>
        /// Returns the unified address-space flags.
        /// </summary>
        protected override AddressSpaceInfo Merge(
            AddressSpaceInfo first,
            AddressSpaceInfo second) =>
            AddressSpaceInfo.Merge(first, second);

        /// <summary>
        /// Returns no analysis value.
        /// </summary>
        protected override AnalysisValue<AddressSpaceInfo>? TryMerge<TContext>(
            Value value,
            TContext context) => null;

        /// <summary>
        /// Tries to convert the given type into an <see cref="AddressSpaceType"/>
        /// and returns the determined address space.
        /// </summary>
        protected override AnalysisValue<AddressSpaceInfo>?
            TryProvide(TypeNode typeNode) =>
            typeNode is AddressSpaceType spaceType
            ? AnalysisValue.Create<AddressSpaceInfo>(spaceType.AddressSpace, typeNode)
            : default(AnalysisValue<AddressSpaceInfo>?);

        #endregion
    }
}
