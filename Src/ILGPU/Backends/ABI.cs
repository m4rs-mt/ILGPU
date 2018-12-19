// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ABI.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents an ABI that can resolve native size information.
    /// </summary>
    public interface ISizeOfABI
    {
        /// <summary>
        /// Resolves the native size in bytes of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The native size.</returns>
        int GetSizeOf(TypeNode type);
    }

    /// <summary>
    /// Represents a generic ABI specification.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class ABI : ISizeOfABI
    {
        #region Static

        /// <summary>
        /// Computes a properly alligned offset in bytes for the given field size.
        /// </summary>
        /// <param name="offset">The current.</param>
        /// <param name="fieldAlignment">The field size in bytes.</param>
        /// <returns>The aligned field offset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align(int offset, int fieldAlignment)
        {
            var padding = (fieldAlignment - (offset % fieldAlignment)) % fieldAlignment;
            return offset + padding;
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Stores ABI information.
        /// </summary>
        protected readonly struct ABITypeInfo
        {
            /// <summary>
            /// Constructs a new ABI information structure.
            /// </summary>
            /// <param name="offsets">The offsets in bytes.</param>
            /// <param name="alignment">The alignment in bytes.</param>
            /// <param name="size">The size in bytes.</param>
            public ABITypeInfo(
                ImmutableArray<int> offsets,
                int alignment,
                int size)
            {
                Debug.Assert(size > 0);

                Offsets = offsets;
                Alignment = alignment;
                Size = size;
            }

            /// <summary>
            /// Returns the field offsets in bytes.
            /// </summary>
            public ImmutableArray<int> Offsets { get; }

            /// <summary>
            /// Returns the native alignment in bytes.
            /// </summary>
            public int Alignment { get; }

            /// <summary>
            /// Returns the native size in bytes.
            /// </summary>
            public int Size { get; }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Contains default size information about built-in types.
        /// </summary>
        private readonly Dictionary<BasicValueType, int> basicTypeInformation =
            new Dictionary<BasicValueType, int>()
            {
                { BasicValueType.Int1, 1 },
                { BasicValueType.Int8, 1 },
                { BasicValueType.Int16, 2 },
                { BasicValueType.Int32, 4 },
                { BasicValueType.Int64, 8 },

                { BasicValueType.Float32, 4 },
                { BasicValueType.Float64, 8 },
            };

        private readonly Dictionary<TypeNode, ABITypeInfo> typeInformation =
            new Dictionary<TypeNode, ABITypeInfo>();

        /// <summary>
        /// Constructs a new ABI specification.
        /// </summary>
        /// <param name="typeContext">The parent type context.</param>
        /// <param name="targetPlatform">The target platform</param>
        /// <param name="viewTypeInfoProvider">The ABI info object provider for a view.</param>
        protected ABI(
            IRTypeContext typeContext,
            TargetPlatform targetPlatform,
            Func<int, ABITypeInfo> viewTypeInfoProvider)
        {
            Debug.Assert(typeContext != null, "Invalid type context");
            TypeContext = typeContext;
            TargetPlatform = targetPlatform;

            PointerArithmeticType = targetPlatform == TargetPlatform.X64 ?
                ArithmeticBasicValueType.UInt64 :
                ArithmeticBasicValueType.UInt32;
            PointerType = typeContext.GetPrimitiveType(
                PointerArithmeticType.GetBasicValueType());
            PointerSize = GetSizeOf(PointerType);

            ViewTypeInfo = viewTypeInfoProvider(PointerSize);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// Returns the associated target platform.
        /// </summary>
        public TargetPlatform TargetPlatform { get; }

        /// <summary>
        /// Returns type of a native pointer.
        /// </summary>
        public PrimitiveType PointerType { get; }

        /// <summary>
        /// Returns the arithmetic type of a native pointer.
        /// </summary>
        public ArithmeticBasicValueType PointerArithmeticType { get; }

        /// <summary>
        /// The size of a pointer type in bytes.
        /// </summary>
        public int PointerSize { get; }

        /// <summary>
        /// Returns the associated view-type information.
        /// </summary>
        private ABITypeInfo ViewTypeInfo { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Defines a new type information for the specified basic value type.
        /// </summary>
        /// <param name="basicValueType">The type to define.</param>
        /// <param name="size">New size information</param>
        protected void DefineBasicTypeInformation(BasicValueType basicValueType, int size)
        {
            basicTypeInformation[basicValueType] = size;
        }

        /// <summary>
        /// Resolves all fields offsets of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The resolved field offsets.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ImmutableArray<int> GetOffsetsOf(TypeNode type)
        {
            Debug.Assert(type != null, "Invalid type");
            var info = ResolveABIInfo(type);
            return info.Offsets;
        }

        /// <summary>
        /// Resolves the field offset of the given field in bytes.
        /// </summary>
        /// <param name="type">The enclosing type.</param>
        /// <param name="fieldIndex">The field index.</param>
        /// <returns>The field offset in bytes.</returns>
        public int GetOffsetOf(TypeNode type, int fieldIndex) =>
            GetOffsetsOf(type)[fieldIndex];

        /// <summary>
        /// Ressolves the native size in bytes of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The native size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSizeOf(TypeNode type)
        {
            Debug.Assert(type != null, "Invalid type");
            var info = ResolveABIInfo(type);
            return info.Size;
        }

        /// <summary>
        /// Ressolves the native alignment in bytes of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The native alignment.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAlignmentOf(TypeNode type)
        {
            Debug.Assert(type != null, "Invalid type");
            var info = ResolveABIInfo(type);
            return info.Alignment;
        }

        /// <summary>
        /// Ressolves the native alignment and size in bytes of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="size">The native size in bytes.</param>
        /// <param name="alignment">The type alignment in bytes.</param>
        /// <returns>The native alignment.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetAlignmentAndSizeOf(
            TypeNode type,
            out int size,
            out int alignment)
        {
            Debug.Assert(type != null, "Invalid type");
            var info = ResolveABIInfo(type);
            size = info.Size;
            alignment = info.Alignment;
        }

        /// <summary>
        /// Computes a properly alligned offset in bytes for the given field type.
        /// </summary>
        /// <param name="offset">The current (and next offset).</param>
        /// <param name="type">The field type in bytes.</param>
        /// <returns>The aligned field offset.</returns>
        public int Align(int offset, TypeNode type) =>
            Align(offset, GetAlignmentOf(type));

        /// <summary>
        /// Resolves ABI info for the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The resolved type information.</returns>
        private ABITypeInfo ResolveABIInfo(TypeNode type)
        {
            Debug.Assert(type != null, "Invalid type info");
            if (type.IsPointerType)
                return new ABITypeInfo(ImmutableArray<int>.Empty, PointerSize, PointerSize);
            if (type.IsViewType)
                return ViewTypeInfo;
            if (basicTypeInformation.TryGetValue(type.BasicValueType, out int size))
                return new ABITypeInfo(ImmutableArray<int>.Empty, size, size);

            if (typeInformation.TryGetValue(type, out ABITypeInfo info))
                return info;

            var containerType = type as ContainerType;
            Debug.Assert(containerType != null, "Not supported type");
            if (containerType.NumChildren < 1)
            {
                // This is an empty struct -> requires special handling
                info = new ABITypeInfo(ImmutableArray<int>.Empty, 1, 1);
            }
            else
                info = ResolveABIInfo(containerType);
            typeInformation.Add(type, info);

            return info;
        }

        /// <summary>
        /// Resolves ABI info for the given type information.
        /// </summary>
        /// <param name="containerType">The type information.</param>
        /// <returns>The resolved ABI information.</returns>
        private ABITypeInfo ResolveABIInfo(ContainerType containerType)
        {
            var offsets = ImmutableArray.CreateBuilder<int>(containerType.NumChildren);
            int alignment = 0;
            int offset = 0;
            for (int i = 0, e = containerType.NumChildren; i < e; ++i)
            {
                var fieldType = containerType.Children[i];
                var fieldSize = GetSizeOf(fieldType);
                var fieldAlignment = GetAlignmentOf(fieldType);

                // Ensure proper alignment
                var elementOffset = Align(offset, fieldAlignment);
                offsets.Add(elementOffset);

                offset = elementOffset + fieldSize;
                alignment = Math.Max(alignment, fieldAlignment);
            }

            // Ensure proper padding
            int size = Align(offset, alignment);

            return new ABITypeInfo(
                offsets.MoveToImmutable(),
                alignment,
                size);
        }

        #endregion
    }
}
