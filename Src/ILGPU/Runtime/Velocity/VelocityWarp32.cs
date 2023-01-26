// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityWarp32.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using static ILGPU.Runtime.Velocity.VelocityWarpOperations32;
using static ILGPU.Runtime.Velocity.VelocityWarpVerifier;
using Arm64Intrinsics = System.Runtime.Intrinsics.Arm.AdvSimd.Arm64;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// A velocity runtime warp based on 32-bit unsigned integer values.
    /// </summary>
    internal readonly partial struct VelocityWarp32 : IEquatable<VelocityWarp32>
    {
        #region Runtime Helper Methods

        /// <summary>
        /// The constant short vector.
        /// </summary>
        public static VelocityWarp32 GetConstI(int value) => new Vector<int>(value);

        /// <summary>
        /// The constant short vector.
        /// </summary>
        public static VelocityWarp32 GetConstU(uint value) => new Vector<uint>(value);

        /// <summary>
        /// The constant float vector.
        /// </summary>
        public static VelocityWarp32 GetConstF(float value) => new Vector<float>(value);

        #endregion

        #region Static

        /// <summary>
        /// Represents the raw vector length for a single sub-warp element.
        /// </summary>
        public static readonly int RawVectorLength = Vector<uint>.Count;

        /// <summary>
        /// Represents the vector length for the whole warp element.
        /// </summary>
        public static readonly int Length = RawVectorLength;

        /// <summary>
        /// The length vector.
        /// </summary>
        public static readonly Vector<uint> LengthVector =
            new Vector<uint>((uint)RawVectorLength);

        /// <summary>
        /// Creates a new lane index vector.
        /// </summary>
        private static Vector<uint> CreateLaneIndexVector()
        {
            Span<uint> indices = stackalloc uint[RawVectorLength];
            for (int i = 0; i < indices.Length; ++i)
                indices[i] = (uint)i;
            return new Vector<uint>(indices);
        }

        /// <summary>
        /// The static lane index vector.
        /// </summary>
        public static readonly VelocityWarp32 LaneIndexVector =
            CreateLaneIndexVector();

        /// <summary>
        /// True if this vector is a 128 bit vector.
        /// </summary>
        public static readonly bool IsVector128 =
            Vector<uint>.Count == Vector128<uint>.Count;

        /// <summary>
        /// True if this vector is a 256 bit vector.
        /// </summary>
        public static readonly bool IsVector256 =
            Vector<uint>.Count == Vector256<uint>.Count;

        /// <summary>
        /// Constructs a velocity warp from a given lane mask.
        /// </summary>
        /// <param name="mask">The lane mask.</param>
        /// <returns>The constructed velocity warp.</returns>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public static VelocityWarp32 FromMask(VelocityLaneMask mask)
        {
            if (IsVector128)
            {
                // We know that there will be 4 lanes
                return Vector128.Create(
                    mask.GetActivityMaskI(0),
                    mask.GetActivityMaskI(1),
                    mask.GetActivityMaskI(2),
                    mask.GetActivityMaskI(3)).AsVector();
            }

            if (IsVector256)
            {
                // We know that there will be 8 lanes
                return Vector256.Create(
                    mask.GetActivityMaskI(0),
                    mask.GetActivityMaskI(1),
                    mask.GetActivityMaskI(2),
                    mask.GetActivityMaskI(3),
                    mask.GetActivityMaskI(4),
                    mask.GetActivityMaskI(5),
                    mask.GetActivityMaskI(6),
                    mask.GetActivityMaskI(7)).AsVector();
            }

            // Use generic stack-based method
            Span<uint> target = stackalloc uint[RawVectorLength];
            for (int index = 0; index < target.Length; ++index)
                target[index] = mask.GetActivityMaskI(index);
            return new Vector<uint>(target);
        }

        /// <summary>
        /// Converts the given velocity warp into a lane mask.
        /// </summary>
        /// <param name="warp">The warp to convert into a lane mask.</param>
        /// <returns>The converted lane mask.</returns>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public static VelocityLaneMask ToMask(VelocityWarp32 warp)
        {
            var warpRawVec = warp.warpData;
            if (IsVector128)
            {
                var warpVec = warpRawVec.AsVector128();
                var mask0 = VelocityLaneMask.Get(0, warpVec.GetElement(0));
                var mask1 = VelocityLaneMask.Get(1, warpVec.GetElement(1));
                var mask2 = VelocityLaneMask.Get(2, warpVec.GetElement(2));
                var mask3 = VelocityLaneMask.Get(3, warpVec.GetElement(3));
                return mask0 | mask1 | mask2 | mask3;
            }

            if (IsVector256)
            {
                // We know that there will be 8 lanes
                var warpVec = warpRawVec.AsVector256();
                var mask0 = VelocityLaneMask.Get(0, warpVec.GetElement(0));
                var mask1 = VelocityLaneMask.Get(1, warpVec.GetElement(1));
                var mask2 = VelocityLaneMask.Get(2, warpVec.GetElement(2));
                var mask3 = VelocityLaneMask.Get(3, warpVec.GetElement(3));
                var mask4 = VelocityLaneMask.Get(4, warpVec.GetElement(4));
                var mask5 = VelocityLaneMask.Get(5, warpVec.GetElement(5));
                var mask6 = VelocityLaneMask.Get(6, warpVec.GetElement(6));
                var mask7 = VelocityLaneMask.Get(7, warpVec.GetElement(7));
                return mask0 | mask1 | mask2 | mask3 | mask4 | mask5 | mask6 | mask7;
            }

            // Use generic method
            var mask = VelocityLaneMask.None;
            for (int index = 0; index < RawVectorLength; ++index)
                mask |= VelocityLaneMask.Get(index, warpRawVec[index]);
            return mask;
        }

        #region AdvSimd

        private static readonly Vector64<byte> FirstByteTableLowerAdvSimd =
            Vector64.Create((byte)0, 0, 0, 0, 4, 4, 4, 4);

        private static readonly Vector64<byte> FirstByteTableUpperAdvSimd =
            Vector64.Create((byte)8, 8, 8, 8, 12, 12, 12, 12);

        private static readonly Vector64<byte> ShuffleOffsetVec64AdvSimd =
            Vector64.Create((byte)0, 1, 2, 3, 0, 1, 2, 3);

        private static readonly Vector64<sbyte> First2BytesToIntAdvSimd =
            Vector64.Create(0, -1, -1, -1, 1, -1, -1, -1);

        private static readonly Vector64<sbyte> Second2BytesToIntAdvSimd =
            Vector64.Create(2, -1, -1, -1, 3, -1, -1, -1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> ComputeShuffleConfigAdvSimd(
            Vector128<uint> indices,
            Vector128<uint> width)
        {
            var div = Arm64Intrinsics.Divide(
                AdvSimd.ConvertToSingle(LaneIndexVector.As<uint>().AsVector128()),
                AdvSimd.ConvertToSingle(width));
            var offsets = AdvSimd.Multiply(
                AdvSimd.ConvertToUInt32RoundToZero(div),
                width);
            var lessThan = AdvSimd.CompareLessThan(indices, offsets);
            var greaterThanOrEqual = AdvSimd.CompareGreaterThanOrEqual(
                indices,
                AdvSimd.Add(offsets, width));
            var selectionMask = AdvSimd.Or(lessThan, greaterThanOrEqual);
            return AdvSimd.BitwiseSelect(
                selectionMask,
                LaneIndexVector.As<uint>().AsVector128(),
                indices);
        }

        #endregion

        #region Generic

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<uint> ComputeShuffleConfigGeneric(
            Vector<uint> indices,
            Vector<uint> width)
        {
            var offsets = LaneIndexVector.warpData / width * width;
            var lessThan = Vector.LessThan(indices, offsets);
            var greaterThanOrEqual = Vector.GreaterThanOrEqual(
                indices,
                offsets + width);
            var selectionMask = lessThan | greaterThanOrEqual;
            return Vector.ConditionalSelect(
                selectionMask,
                LaneIndexVector.warpData,
                indices);
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<uint> ComputeShuffleConfig(
            Vector<uint> indices,
            Vector<uint> width) =>
            IsVector128 && Arm64Intrinsics.IsSupported
                ? ComputeShuffleConfigAdvSimd(
                        indices.AsVector128(),
                        width.AsVector128())
                    .AsVector()
                : ComputeShuffleConfigGeneric(indices, width);

        #endregion

        #region Instance

        private readonly Vector<uint> warpData;

        /// <summary>
        /// Creates a new warp instance using the given data vectors.
        /// </summary>
        public unsafe VelocityWarp32(uint* data)
            : this(new ReadOnlySpan<uint>(data, Length))
        { }

        /// <summary>
        /// Creates a new warp instance using the given data vectors.
        /// </summary>
        public unsafe VelocityWarp32(ReadOnlySpan<uint> data)
            : this(new Vector<uint>(data))
        { }

        /// <summary>
        /// Creates a new warp instance using the given data vector.
        /// </summary>
        /// <param name="data">The input data vector.</param>
        public VelocityWarp32(Vector<uint> data)
        {
            warpData = data;
        }

        /// <summary>
        /// Creates a new warp instance using the given data vector.
        /// </summary>
        /// <param name="data">The input data vector.</param>
        public VelocityWarp32(Vector<int> data)
            : this(data.As<int, uint>())
        { }

        /// <summary>
        /// Creates a new warp instance using the given data vector.
        /// </summary>
        /// <param name="data">The input data vector.</param>
        public VelocityWarp32(Vector<float> data)
            : this(data.As<float, uint>())
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the value of the specified lane using specialized implementations.
        /// </summary>
        public uint this[int laneIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsVector128)
                    return warpData.AsVector128().GetElement(laneIndex);
                if (IsVector256)
                    return warpData.AsVector256().GetElement(laneIndex);
                return warpData[laneIndex];
            }
        }

        /// <summary>
        /// Returns the value of the specified lane using specialized implementations.
        /// </summary>
        public uint this[uint laneIndex] => this[(int)laneIndex];

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetElementF(int laneIndex) => Interop.IntAsFloat(this[laneIndex]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<T> As<T>() where T : struct => warpData.As<uint, T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 Mask(VelocityWarp32 mask) => warpData & mask.warpData;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp64 ToWarp64()
        {
            Vector.Widen(warpData, out var lower, out var upper);
            return new VelocityWarp64(lower, upper);
        }

        #endregion

        #region Barrier

        /// <summary>
        /// Uses a comparison and a horizontal sum to compute the number of barrier
        /// participants for which the predicate evaluated to true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 BarrierPopCount(VelocityLaneMask mask)
        {
            var greaterThan = ~Vector.Equals(
                warpData.As<uint, int>(),
                Vector<int>.Zero);
            var masked = Vector.BitwiseAnd(greaterThan, FromMask(mask).As<int>());
            return new Vector<uint>((uint)-Vector.Sum(masked));
        }

        /// <summary>
        /// Uses a comparison check with a warp-length vector to determine which lanes
        /// evaluated to true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 BarrierAndFromPopCount(VelocityLaneMask mask)
        {
            var totalCount = GetConstI(mask.Count);
            return Vector.Equals(warpData, totalCount.As<uint>());
        }

        /// <summary>
        /// Uses a comparison check with a warp-length vector to determine which lanes
        /// evaluated to true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 BarrierAnd(VelocityLaneMask mask) =>
            BarrierPopCount(mask).BarrierAndFromPopCount(mask);

        /// <summary>
        /// Uses a comparison check with a zero-data vector to determine which lanes
        /// evaluated to true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 BarrierOrFromPopCount() =>
            ~Vector.Equals(warpData, Vector<uint>.Zero);

        /// <summary>
        /// Uses a comparison check with a zero-data vector to determine which lanes
        /// evaluated to true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 BarrierOr(VelocityLaneMask mask) =>
            BarrierPopCount(mask).BarrierOrFromPopCount();

        #endregion

        #region Shuffle

        /// <summary>
        /// Extracts the value from the lane given by the first value of the source
        /// lane vector and broadcasts the value to all other lanes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 Broadcast<TVerifier>(VelocityWarp32 sourceLanes)
            where TVerifier : struct, IVelocityWarpVerifier
        {
            GetVerifier<TVerifier>().VerifyBroadcast(sourceLanes);
            return Broadcast<TVerifier>(sourceLanes[0]);
        }

        /// <summary>
        /// Extracts the value from the given lane and broadcasts the value to all
        /// other lanes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 Broadcast<TVerifier>(uint sourceLane)
            where TVerifier : struct, IVelocityWarpVerifier
        {
            GetVerifier<TVerifier>().VerifyBroadcast(sourceLane);
            // Specialized implementation on ARM
            if (IsVector128 && AdvSimd.IsSupported)
            {
                var indices = AdvSimd.Add(
                    Vector64.Create((byte)(sourceLane * 4)),
                    ShuffleOffsetVec64AdvSimd);

                // Shuffle elements
                var shuffledData = AdvSimd.VectorTableLookup(
                    warpData.AsVector128().AsByte(),
                    indices).AsUInt32();
                return Vector128.Create(shuffledData, shuffledData).AsVector();
            }
            // Use a generic broadcast operation instead
            return new Vector<uint>(this[sourceLane]);
        }

        private readonly struct ScalarShuffleOperation : IScalarIOperation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Apply(int index, int value) =>
                value >= 0 && value < RawVectorLength ? value : index;
        }

        /// <summary>
        /// Shuffles values using optimized implementations to improve performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector<uint> Shuffle(Vector<uint> sourceLanes)
        {
            // Use a special implementation on ARM
            if (IsVector128 && AdvSimd.IsSupported)
            {
                var dataVec = warpData.AsVector128();
                var sourceVec = sourceLanes.AsVector128();

                var source = AdvSimd.Multiply(sourceVec, LengthVector.AsVector128())
                    .AsByte();
                var source1 = AdvSimd.VectorTableLookup(
                    source,
                    FirstByteTableLowerAdvSimd);
                var shiftDelta1 = AdvSimd.Add(source1, ShuffleOffsetVec64AdvSimd);
                var source2 = AdvSimd.VectorTableLookup(
                    source,
                    FirstByteTableUpperAdvSimd);
                var shiftDelta2 = AdvSimd.Add(source2, ShuffleOffsetVec64AdvSimd);

                var firstPart = AdvSimd.VectorTableLookup(dataVec.AsByte(), shiftDelta1);
                var secondPart = AdvSimd.VectorTableLookup(dataVec.AsByte(), shiftDelta2);
                return Vector128
                    .Create(firstPart.AsUInt32(), secondPart.AsUInt32())
                    .AsVector();
            }

            return this.ApplyScalarIOperation(new ScalarShuffleOperation()).As<uint>();
        }

        /// <summary>
        /// Shuffles values defined by the given source lanes using temporary stack
        /// memory to shuffle generic vectors.
        /// </summary>
        public VelocityWarp32 Shuffle<TVerifier>(VelocityWarp32 sourceLanes)
            where TVerifier : struct, IVelocityWarpVerifier
        {
            GetVerifier<TVerifier>().VerifyShuffle(sourceLanes);
            return Shuffle(sourceLanes.warpData);
        }

        /// <summary>
        /// Uses the internal <see cref="ComputeShuffleConfig"/> to determine a
        /// shuffle configuration using the down delta and uses the
        /// <see cref="Shuffle{TVerifier}"/> method to reorder all values.
        /// </summary>
        /// <remarks>
        /// Note that all values of <paramref name="delta"/> and
        /// <paramref name="width"/> are assumed to be identical.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 ShuffleDown<TVerifier>(
            VelocityWarp32 delta,
            VelocityWarp32 width)
            where TVerifier : struct, IVelocityWarpVerifier
        {
            GetVerifier<TVerifier>().VerifyShuffleDown(delta, width);

            var indices = LaneIndexVector.warpData + delta.warpData;
            var offsets = ComputeShuffleConfig(indices, width.warpData);
            return Shuffle(offsets);
        }

        /// <summary>
        /// Shuffles all lanes using down deltas by expanding the values of
        /// <paramref name="delta"/> and <paramref name="width"/> values to full-size
        /// warp vectors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 ShuffleDown<TVerifier>(uint delta, uint width)
            where TVerifier : struct, IVelocityWarpVerifier =>
            ShuffleDown<TVerifier>(
                new Vector<uint>(delta),
                new Vector<uint>(width));

        /// <summary>
        /// Uses the internal <see cref="ComputeShuffleConfig"/> to determine a
        /// shuffle configuration using the up delta and uses the
        /// <see cref="Shuffle{TVerifier}"/> method to reorder all values.
        /// </summary>
        /// <remarks>
        /// Note that all values of <paramref name="delta"/> and
        /// <paramref name="width"/> are assumed to be identical.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 ShuffleUp<TVerifier>(
            VelocityWarp32 delta,
            VelocityWarp32 width)
            where TVerifier : struct, IVelocityWarpVerifier
        {
            GetVerifier<TVerifier>().VerifyShuffleUp(delta, width);

            var indices = LaneIndexVector.warpData - delta.warpData;
            var offsets = ComputeShuffleConfig(indices, width.warpData);
            return Shuffle(offsets);
        }

        /// <summary>
        /// Shuffles all lanes using up deltas by expanding the values of
        /// <paramref name="delta"/> and <paramref name="width"/> values to full-size
        /// warp vectors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 ShuffleUp<TVerifier>(uint delta, uint width)
            where TVerifier : struct, IVelocityWarpVerifier =>
            ShuffleUp<TVerifier>(
                new Vector<uint>(delta),
                new Vector<uint>(width));

        /// <summary>
        /// Uses the internal <see cref="ComputeShuffleConfig"/> to determine a
        /// shuffle configuration using the xor-mask delta and uses the
        /// <see cref="Shuffle{TVerifier}"/> method to reorder all values.
        /// </summary>
        /// <remarks>
        /// Note that all values of <paramref name="mask"/> and
        /// <paramref name="width"/> are assumed to be identical.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 ShuffleXor<TVerifier>(
            VelocityWarp32 mask,
            VelocityWarp32 width)
            where TVerifier : struct, IVelocityWarpVerifier
        {
            GetVerifier<TVerifier>().VerifyShuffleXor(mask, width);

            var indices = LaneIndexVector.warpData ^ mask.warpData;
            var offsets = ComputeShuffleConfig(indices, width.warpData);
            return Shuffle(offsets);
        }

        /// <summary>
        /// Shuffles all lanes using xor-masks by expanding the values of
        /// <paramref name="mask"/> and <paramref name="width"/> values to full-size
        /// warp vectors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 ShuffleXor<TVerifier>(uint mask, uint width)
            where TVerifier : struct, IVelocityWarpVerifier =>
            ShuffleXor<TVerifier>(
                new Vector<uint>(mask),
                new Vector<uint>(width));

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
            public unsafe uint Apply(int index, uint value)
            {
                ulong targetAddress = target[index];
                ref T managedRef = ref Unsafe.AsRef<T>((void*)targetAddress);

                TOperation op = default;
                T convertedValue = Unsafe.As<uint, T>(ref value);

                if (!mask.IsActive(index))
                    return 0U;
                T result = op.Atomic(ref managedRef, convertedValue);
                return Unsafe.As<T, uint>(ref result);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal VelocityWarp32 Atomic<T, TOperation>(
            VelocityWarp64 target,
            VelocityLaneMask mask)
            where T : unmanaged
            where TOperation : struct, IAtomicOperation<T> =>
            this.ApplyScalarUOperation(
                new AtomicScalarOperation<T, TOperation>(target, mask));

        private readonly struct AtomicCompareExchangeOperation : IScalarUOperation
        {
            private readonly VelocityWarp64 target;
            private readonly VelocityWarp32 compare;
            private readonly VelocityLaneMask mask;

            public AtomicCompareExchangeOperation(
                VelocityWarp64 targetWarp,
                VelocityWarp32 compareWarp,
                VelocityLaneMask warpMask)
            {
                target = targetWarp;
                compare = compareWarp;
                mask = warpMask;
            }

            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public unsafe uint Apply(int index, uint value)
            {
                ulong targetAddress = target[index];
                ref uint managedRef = ref Unsafe.AsRef<uint>((void*)targetAddress);
                uint compareVal = compare[index];

                if (!mask.IsActive(index))
                    return compareVal;
                return ILGPU.Atomic.CompareExchange(
                    ref managedRef,
                    compareVal,
                    value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityWarp32 AtomicCompareExchange(
            VelocityWarp64 target,
            VelocityWarp32 compare,
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
        public bool Equals(VelocityWarp32 other) =>
            other.warpData.Equals(warpData);

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one in terms of
        /// its lane values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) =>
            obj is VelocityWarp32 other && Equals(other);

        /// <summary>
        /// Returns the hash code of the underlying vector data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => warpData.GetHashCode();

        /// <summary>
        /// Returns the string representation of the underlying warp data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => warpData.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Converts a generic vector into a generic warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator VelocityWarp32(Vector<int> data) =>
            new VelocityWarp32(data.As<int, uint>());

        /// <summary>
        /// Converts a generic vector into a generic warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator VelocityWarp32(Vector<uint> data) =>
            new VelocityWarp32(data);

        /// <summary>
        /// Converts a generic vector into a generic warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator VelocityWarp32(Vector<float> data) =>
            new VelocityWarp32(data.As<float, uint>());

        #endregion
    }
}
