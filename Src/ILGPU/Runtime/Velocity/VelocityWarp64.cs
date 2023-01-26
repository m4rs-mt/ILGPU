// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityWarp64.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static ILGPU.Runtime.Velocity.VelocityWarpOperations64;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a 64bit-wide extra-wide warp.
    /// </summary>
    internal readonly partial struct VelocityWarp64 : IEquatable<VelocityWarp64>
    {
        #region Runtime Constants

        /// <summary>
        /// The constant short vector.
        /// </summary>
        public static VelocityWarp64 GetConstI(long value) => new VelocityWarp64(
            new Vector<long>(value),
            new Vector<long>(value));

        /// <summary>
        /// The constant short vector.
        /// </summary>
        public static VelocityWarp64 GetConstU(ulong value) => new VelocityWarp64(
            new Vector<ulong>(value),
            new Vector<ulong>(value));

        /// <summary>
        /// The constant float vector.
        /// </summary>
        public static VelocityWarp64 GetConstF(double value) => new VelocityWarp64(
            new Vector<double>(value),
            new Vector<double>(value));

        #endregion

        #region Static

        /// <summary>
        /// True if this vector is a 128 bit vector.
        /// </summary>
        public static readonly bool IsVector128 =
            Vector<ulong>.Count == Vector128<ulong>.Count;

        /// <summary>
        /// True if this vector is a 256 bit vector.
        /// </summary>
        public static readonly bool IsVector256 =
            Vector<ulong>.Count == Vector256<ulong>.Count;

        /// <summary>
        /// Represents the raw vector length for a single sub-warp element.
        /// </summary>
        public static readonly int RawVectorLength = Vector<ulong>.Count;

        /// <summary>
        /// Represents the number of elements of this warp.
        /// </summary>
        public static readonly int Length = RawVectorLength * 2;

        private static VelocityWarp64 CreateLaneIndexVector()
        {
            Span<ulong> indices = stackalloc ulong[Length];
            for (int i = 0; i < indices.Length; ++i)
                indices[i] = (ulong)i;
            return new VelocityWarp64(
                new Vector<ulong>(indices[..RawVectorLength]),
                new Vector<ulong>(indices.Slice(RawVectorLength, RawVectorLength)));
        }

        /// <summary>
        /// The static lane index vector.
        /// </summary>
        public static readonly VelocityWarp64 LaneIndexVector =
            CreateLaneIndexVector();

        /// <summary>
        /// Constructs a velocity warp from a given lane mask.
        /// </summary>
        /// <param name="mask">The lane mask.</param>
        /// <returns>The constructed velocity warp.</returns>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public static VelocityWarp64 FromMask(VelocityLaneMask mask)
        {
            if (IsVector128)
            {
                // We know that there will be 4 lanes
                return new VelocityWarp64(
                    Vector128.Create(
                        mask.GetActivityMaskL(0),
                        mask.GetActivityMaskL(1)).AsVector(),
                    Vector128.Create(
                        mask.GetActivityMaskL(2),
                        mask.GetActivityMaskL(3)).AsVector());
            }

            if (IsVector256)
            {
                // We know that there will be 8 lanes
                return new VelocityWarp64(
                    Vector256.Create(
                        mask.GetActivityMaskL(0),
                        mask.GetActivityMaskL(1),
                        mask.GetActivityMaskL(2),
                        mask.GetActivityMaskL(3)).AsVector(),
                    Vector256.Create(
                        mask.GetActivityMaskL(4),
                        mask.GetActivityMaskL(5),
                        mask.GetActivityMaskL(6),
                        mask.GetActivityMaskL(7)).AsVector());
            }

            // Use generic stack-based method
            Span<ulong> target = stackalloc ulong[Length];
            for (int index = 0; index < target.Length; ++index)
                target[index] = mask.GetActivityMaskL(index);
            return new VelocityWarp64(
                new Vector<ulong>(target[..RawVectorLength]),
                new Vector<ulong>(target.Slice(RawVectorLength, RawVectorLength)));
        }

        /// <summary>
        /// Converts the given velocity warp into a lane mask.
        /// </summary>
        /// <param name="warp">The warp to convert into a lane mask.</param>
        /// <returns>The converted lane mask.</returns>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public static VelocityLaneMask ToMask(VelocityWarp64 warp)
        {
            var lowerRawVec = warp.lowerData;
            var upperRawVec = warp.upperData;
            if (IsVector128)
            {
                var lowerVec = lowerRawVec.AsVector128();
                var upperVec = upperRawVec.AsVector128();

                var mask0 = VelocityLaneMask.Get(0, lowerVec.GetElement(0));
                var mask1 = VelocityLaneMask.Get(1, lowerVec.GetElement(1));
                var mask2 = VelocityLaneMask.Get(2, upperVec.GetElement(0));
                var mask3 = VelocityLaneMask.Get(3, upperVec.GetElement(1));
                return mask0 | mask1 | mask2 | mask3;
            }

            if (IsVector256)
            {
                // We know that there will be 8 lanes
                var lowerVec = lowerRawVec.AsVector256();
                var upperVec = upperRawVec.AsVector256();

                var mask0 = VelocityLaneMask.Get(0, lowerVec.GetElement(0));
                var mask1 = VelocityLaneMask.Get(1, lowerVec.GetElement(1));
                var mask2 = VelocityLaneMask.Get(2, lowerVec.GetElement(2));
                var mask3 = VelocityLaneMask.Get(3, lowerVec.GetElement(3));

                var mask4 = VelocityLaneMask.Get(0, upperVec.GetElement(0));
                var mask5 = VelocityLaneMask.Get(1, upperVec.GetElement(1));
                var mask6 = VelocityLaneMask.Get(2, upperVec.GetElement(2));
                var mask7 = VelocityLaneMask.Get(3, upperVec.GetElement(3));
                return mask0 | mask1 | mask2 | mask3 | mask4 | mask5 | mask6 | mask7;
            }

            // Use generic method
            var mask = VelocityLaneMask.None;
            for (int index = 0; index < RawVectorLength; ++index)
                mask |= VelocityLaneMask.Get(index, warp.lowerData[index]);
            for (int index = 0; index < RawVectorLength; ++index)
            {
                mask |= VelocityLaneMask.Get(
                    index + RawVectorLength,
                    warp.upperData[index]);
            }
            return mask;
        }

        #endregion

        #region Instance

        private readonly Vector<ulong> lowerData;
        private readonly Vector<ulong> upperData;

        /// <summary>
        /// Creates a new warp instance using the given data vectors.
        /// </summary>
        public unsafe VelocityWarp64(ulong* data)
            : this(new ReadOnlySpan<ulong>(data, Length))
        { }

        /// <summary>
        /// Creates a new warp instance using the given data vectors.
        /// </summary>
        public VelocityWarp64(ReadOnlySpan<long> data)
            : this(
                new Vector<long>(data[..RawVectorLength]),
                new Vector<long>(data.Slice(RawVectorLength, RawVectorLength)))
        { }

        /// <summary>
        /// Creates a new warp instance using the given data vectors.
        /// </summary>
        public VelocityWarp64(ReadOnlySpan<ulong> data)
            : this(
                new Vector<ulong>(data[..RawVectorLength]),
                new Vector<ulong>(data.Slice(RawVectorLength, RawVectorLength)))
        { }

        /// <summary>
        /// Creates a new warp instance using the given data vectors.
        /// </summary>
        public VelocityWarp64(Vector<ulong> lower, Vector<ulong> upper)
        {
            lowerData = lower;
            upperData = upper;
        }

        /// <summary>
        /// Creates a new warp instance using the given data vectors.
        /// </summary>
        public VelocityWarp64(Vector<long> lower, Vector<long> upper)
            : this(lower.As<long, ulong>(), upper.As<long, ulong>())
        { }

        /// <summary>
        /// Creates a new warp instance using the given data vectors.
        /// </summary>
        public VelocityWarp64(Vector<double> lower, Vector<double> upper)
            : this(lower.As<double, ulong>(), upper.As<double, ulong>())
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the value of the specified lane using specialized implementations.
        /// </summary>
        public ulong this[int laneIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => laneIndex < RawVectorLength
                ? GetLowerElement(laneIndex)
                : GetUpperElement(laneIndex - RawVectorLength);
        }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T LoadFromPtr<T>(int laneIndex)
            where T : unmanaged
        {
            void* ptr = GetElementPtr(laneIndex);
            return Unsafe.Read<T>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void StoreToPtr<T>(int laneIndex, T value)
            where T : unmanaged
        {
            void* ptr = GetElementPtr(laneIndex);
            Unsafe.Write(ptr, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetElementF(int laneIndex) =>
            laneIndex < RawVectorLength
            ? GetLowerElementF(laneIndex)
            : GetUpperElementF(laneIndex - RawVectorLength);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* GetElementPtr(int laneIndex) =>
            laneIndex < RawVectorLength
            ? GetLowerElementPtr(laneIndex)
            : GetUpperElementPtr(laneIndex - RawVectorLength);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetLowerElement(int laneIndex)
        {
            if (IsVector128)
                return lowerData.AsVector128().GetElement(laneIndex);
            if (IsVector256)
                return lowerData.AsVector256().GetElement(laneIndex);
            return lowerData[laneIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetLowerElementF(int laneIndex) =>
            Interop.IntAsFloat(GetLowerElement(laneIndex));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* GetLowerElementPtr(int laneIndex) =>
            new IntPtr((long)GetLowerElement(laneIndex)).ToPointer();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetUpperElement(int laneIndex)
        {
            if (IsVector128)
                return upperData.AsVector128().GetElement(laneIndex);
            if (IsVector256)
                return upperData.AsVector256().GetElement(laneIndex);
            return upperData[laneIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetUpperElementF(int laneIndex) =>
            Interop.IntAsFloat(GetUpperElement(laneIndex));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* GetUpperElementPtr(int laneIndex) =>
            new IntPtr((long)GetUpperElement(laneIndex)).ToPointer();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<T> LowerAs<T>() where T : struct => lowerData.As<ulong, T>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<T> UpperAs<T>() where T : struct => upperData.As<ulong, T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 Mask(VelocityWarp64 mask) =>
            new VelocityWarp64(
                lowerData & mask.lowerData,
                upperData & mask.upperData);

        #endregion

        #region Atomics

        internal interface IAtomicOperation<T>
            where T : unmanaged
        {
            T Atomic(ref T target, T value);
        }

        private readonly struct AtomicScalarOperation<T, TOperation> : IScalarUOperation
            where T : unmanaged
            where TOperation : struct, IAtomicOperation<T>
        {
            private readonly VelocityWarp64 target;
            private readonly VelocityLaneMask mask;

            public AtomicScalarOperation(
                VelocityWarp64 targetWarp,
                VelocityLaneMask warpMask)
            {
                target = targetWarp;
                mask = warpMask;
            }

            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public unsafe ulong Apply(int index, ulong value)
            {
                ulong targetAddress = target[index];
                ref T managedRef = ref Unsafe.AsRef<T>((void*)targetAddress);

                TOperation op = default;
                T convertedValue = Unsafe.As<ulong, T>(ref value);

                if (!mask.IsActive(index))
                    return 0UL;
                T result = op.Atomic(ref managedRef, convertedValue);
                return Unsafe.As<T, ulong>(ref result);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal VelocityWarp64 Atomic<T, TOperation>(
            VelocityWarp64 target,
            VelocityLaneMask mask)
            where T : unmanaged
            where TOperation : struct, IAtomicOperation<T> =>
            this.ApplyScalarUOperation(
                new AtomicScalarOperation<T, TOperation>(target, mask));

        private readonly struct AtomicCompareExchangeOperation : IScalarUOperation
        {
            private readonly VelocityWarp64 target;
            private readonly VelocityWarp64 compare;
            private readonly VelocityLaneMask mask;

            public AtomicCompareExchangeOperation(
                VelocityWarp64 targetWarp,
                VelocityWarp64 compareWarp,
                VelocityLaneMask warpMask)
            {
                target = targetWarp;
                compare = compareWarp;
                mask = warpMask;
            }

            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public unsafe ulong Apply(int index, ulong value)
            {
                ulong targetAddress = target[index];
                ref ulong managedRef = ref Unsafe.AsRef<ulong>((void*)targetAddress);
                ulong compareVal = compare[index];

                if (!mask.IsActive(index))
                    return compareVal;
                return ILGPU.Atomic.CompareExchange(
                    ref managedRef,
                    compare[index],
                    value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 AtomicCompareExchange(
            VelocityWarp64 target,
            VelocityWarp64 compare,
            VelocityLaneMask mask) =>
            this.ApplyScalarUOperation(
                new AtomicCompareExchangeOperation(target, compare, mask));

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given warp is equal to the current one in terms of its
        /// lane values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(VelocityWarp64 other) =>
            lowerData.Equals(other.lowerData) && upperData.Equals(other.upperData);

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one in terms of
        /// its lane values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) =>
            obj is VelocityWarp64 other && Equals(other);

        /// <summary>
        /// Returns the hash code of the underlying vector data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() =>
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            HashCode.Combine(lowerData.GetHashCode(), upperData.GetHashCode());
#else
            lowerData.GetHashCode() ^ upperData.GetHashCode();
#endif

        /// <summary>
        /// Returns the string representation of the underlying warp data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => $"({lowerData}, {upperData})";

        #endregion

    }
}
