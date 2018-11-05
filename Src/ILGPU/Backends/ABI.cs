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

using ILGPU.IR;
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
    /// Represents an ABI specification.
    /// </summary>
    public sealed class ABI : DisposeBase, ISizeOfABI
    {
        #region Static

        /// <summary>
        /// Contains default .Net size information about built-in blittable types.
        /// </summary>
        private static readonly Dictionary<BasicValueType, int> ManagedSizes =
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

        /// <summary>
        /// Computes a properly alligned offset in bytes for the given field size.
        /// </summary>
        /// <param name="offset">The current (and next offset).</param>
        /// <param name="fieldSize">The field size in bytes.</param>
        /// <returns>The aligned field offset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align(ref int offset, int fieldSize)
        {
            var elementAlignment = offset % fieldSize;
            var result = offset += (fieldSize - elementAlignment) % fieldSize;
            offset += fieldSize;
            return result;
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Stores ABI information.
        /// </summary>
        private readonly struct ABITypeInfo
        {
            /// <summary>
            /// Constructs a new ABI information structure.
            /// </summary>
            /// <param name="offsets">The offsets in bytes.</param>
            /// <param name="size">The size in bytes.</param>
            public ABITypeInfo(
                ImmutableArray<int> offsets,
                int size)
            {
                Debug.Assert(size > 0);

                Offsets = offsets;
                Size = size;
            }

            /// <summary>
            /// Returns the field offsets in bytes.
            /// </summary>
            public ImmutableArray<int> Offsets { get; }

            /// <summary>
            /// Returns the native size in bytes.
            /// </summary>
            public int Size { get; }
        }

        #endregion

        #region Instance

        private readonly object syncObject = new object();
        private readonly Dictionary<TypeNode, ABITypeInfo> typeInformation =
            new Dictionary<TypeNode, ABITypeInfo>();

        /// <summary>
        /// Constructs a new ABI specification.
        /// </summary>
        /// <param name="pointerType">The native pointer type.</param>
        /// <param name="pointerArithmeticType">The arithmetic type of a pointer.</param>
        /// <param name="viewSize">The size of a view type in bytes.</param>
        public ABI(
            PrimitiveType pointerType,
            ArithmeticBasicValueType pointerArithmeticType,
            int viewSize)
        {
            PointerType = pointerType;
            PointerArithmeticType = pointerArithmeticType;
            PointerSize = GetSizeOf(pointerType);
            ViewSize = viewSize;
        }

        #endregion

        #region Properties

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
        /// The size of a view type in bytes.
        /// </summary>
        public int ViewSize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves all fields offsets of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The resolved field offsets.</returns>
        public ImmutableArray<int> GetOffsetsOf(TypeNode type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            var info = ResolveABIInfo(type);
            return info.Offsets;
        }

        /// <summary>
        /// Resolves the field offset of the given field in bytes.
        /// </summary>
        /// <param name="type">The enclosing type.</param>
        /// <param name="fieldIndex">The field index.</param>
        /// <returns>The field offset in bytes.</returns>
        public int GetOffsetOf(TypeNode type, int fieldIndex)
        {
            return GetOffsetsOf(type)[fieldIndex];
        }

        /// <summary>
        /// Ressolves the native size in bytes of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The native size.</returns>
        public int GetSizeOf(TypeNode type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            var info = ResolveABIInfo(type);
            return info.Size;
        }

        /// <summary>
        /// Computes a properly alligned offset in bytes for the given field type.
        /// </summary>
        /// <param name="offset">The current (and next offset).</param>
        /// <param name="type">The field type in bytes.</param>
        /// <returns>The aligned field offset.</returns>
        public int Align(ref int offset, TypeNode type) =>
            Align(ref offset, GetSizeOf(type));

        /// <summary>
        /// Resolves ABI info for the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The resolved type information.</returns>
        private ABITypeInfo ResolveABIInfo(TypeNode type)
        {
            Debug.Assert(type != null, "Invalid type info");
            if (type.IsPointerType)
                return new ABITypeInfo(ImmutableArray<int>.Empty, PointerSize);
            if (type.IsViewType)
                return new ABITypeInfo(ImmutableArray<int>.Empty, ViewSize);
            if (ManagedSizes.TryGetValue(type.BasicValueType, out int size))
                return new ABITypeInfo(ImmutableArray<int>.Empty, size);

            lock (syncObject)
            {
                if (typeInformation.TryGetValue(type, out ABITypeInfo info))
                    return info;

                var containerType = type as ContainerType;
                Debug.Assert(containerType != null, "Not supported type");
                if (containerType.NumChildren < 1)
                {
                    // This is an empty struct -> requires special handling
                    info = new ABITypeInfo(ImmutableArray<int>.Empty, 1);
                }
                else
                    info = ResolveABIInfo(containerType);
                typeInformation.Add(type, info);

                return info;
            }
        }

        /// <summary>
        /// Resolves ABI info for the given type information.
        /// </summary>
        /// <param name="containerType">The type information.</param>
        /// <returns>The resolved ABI information.</returns>
        private ABITypeInfo ResolveABIInfo(ContainerType containerType)
        {
            var offsets = ImmutableArray.CreateBuilder<int>(containerType.NumChildren);
            int largestElement = 0;
            int offset = 0;
            for (int i = 0, e = containerType.NumChildren; i < e; ++i)
            {
                var fieldType = containerType.Children[i];
                var fieldSize = GetSizeOf(fieldType);

                // Ensure proper alignment
                var elementOffset = Align(ref offset, fieldSize);
                offsets.Add(elementOffset);

                largestElement = Math.Max(largestElement, fieldSize);
            }

            // Ensure proper padding
            var elementPadding = offset % largestElement;
            var size = offset + ((largestElement - elementPadding) % largestElement);

            return new ABITypeInfo(offsets.MoveToImmutable(), size);
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        { }

        #endregion
    }

    /// <summary>
    /// Constructs ABI instances.
    /// </summary>
    public abstract class ABIProvider
    {
        #region Nested Types

        /// <summary>
        /// Implements an X86 ABI provider.
        /// </summary>
        private sealed class X86 : ABIProvider
        {
            /// <summary cref="ABIProvider.CreateABI(IRContext)"/>
            public override ABI CreateABI(IRContext context) =>
                new ABI(
                    context.GetPrimitiveType(BasicValueType.Int32),
                    ArithmeticBasicValueType.UInt32,
                    8);
        }

        /// <summary>
        /// Implements an X64 ABI provider.
        /// </summary>
        private sealed class X64 : ABIProvider
        {
            /// <summary cref="ABIProvider.CreateABI(IRContext)"/>
            public override ABI CreateABI(IRContext context) =>
                new ABI(
                    context.GetPrimitiveType(BasicValueType.Int64),
                    ArithmeticBasicValueType.UInt64,
                    16);
        }

        #endregion

        /// <summary>
        /// Creates an ABI provider for an X86 platform.
        /// </summary>
        /// <returns>The X86 ABI provider.</returns>
        public static ABIProvider CreateX86Provider() => new X86();

        /// <summary>
        /// Creates an ABI provider for an X64 platform.
        /// </summary>
        /// <returns>The X64 ABI provider.</returns>
        public static ABIProvider CreateX64Provider() => new X64();

        /// <summary>
        /// Creates an ABI provider for the given target platform.
        /// </summary>
        /// <param name="platform">The target platform.</param>
        /// <returns>The corresponding ABI provider.</returns>
        public static ABIProvider CreateProvider(TargetPlatform platform)
        {
            switch (platform)
            {
                case TargetPlatform.X86:
                    return CreateX86Provider();
                case TargetPlatform.X64:
                    return CreateX64Provider();
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform));
            }
        }

        /// <summary>
        /// Creates a new ABI instance.
        /// </summary>
        /// <param name="context">The current IR context.</param>
        /// <returns>The ABI instance.</returns>
        public abstract ABI CreateABI(IRContext context);
    }
}
