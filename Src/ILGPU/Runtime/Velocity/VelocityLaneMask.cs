// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityLaneMask.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// A mask for all active lanes stored in the general purpose register bank.
    /// </summary>
    readonly struct VelocityLaneMask : IEquatable<VelocityLaneMask>
    {
        #region Static

        public static readonly MethodInfo DumpMethod = typeof(VelocityLaneMask).GetMethod(
            nameof(Dump),
            BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Specifies the maximum number of lanes per mask.
        /// </summary>
        public const int MaxNumberOfLanes = sizeof(uint) * 8;

        /// <summary>
        /// Represents the maximum lanes mask.
        /// </summary>
        private static readonly uint MaxNumberOfLanesMask =
            uint.MaxValue >> (MaxNumberOfLanes - VelocityWarp32.Length);

        /// <summary>
        /// Represents a mask in which all lanes are active.
        /// </summary>
        public static readonly VelocityLaneMask All = new VelocityLaneMask(uint.MaxValue);

        /// <summary>
        /// Represents a mask in which all lanes are inactive.
        /// </summary>
        public static readonly VelocityLaneMask None = new VelocityLaneMask(0);

        /// <summary>
        /// Dumps the given lane mask to the console output.
        /// </summary>
        /// <param name="mask">The lane mask to output.</param>
        public static void Dump(VelocityLaneMask mask) =>
            Console.WriteLine(mask.ToString());

        /// <summary>
        /// Verifies the given lane index.
        /// </summary>
        /// <param name="laneIndex">The lane index to verify.</param>
        private static void VerifyLaneIndex(int laneIndex) =>
            Debug.Assert(
                laneIndex >= 0 && laneIndex < MaxNumberOfLanes,
                "Lane index out of range");

        /// <summary>
        /// Gets a raw lane mask for the given lane index.
        /// </summary>
        /// <param name="laneIndex">The lane index.</param>
        /// <returns>A raw activity lane mask for the given lane index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetLaneIndexMask(int laneIndex) => 1U << laneIndex;

        /// <summary>
        /// Gets a new lane mask for the given lane index.
        /// </summary>
        /// <param name="laneIndex">The lane index.</param>
        /// <param name="value">Non-zero to activate the lane, zero otherwise.</param>
        /// <returns>The created activity lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityLaneMask Get(int laneIndex, uint value) =>
            new VelocityLaneMask(value != 0U ? GetLaneIndexMask(laneIndex) : 0);

        /// <summary>
        /// Gets a new lane mask for the given lane index.
        /// </summary>
        /// <param name="laneIndex">The lane index.</param>
        /// <param name="value">Non-zero to activate the lane, zero otherwise.</param>
        /// <returns>The created activity lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityLaneMask Get(int laneIndex, ulong value) =>
            new VelocityLaneMask(value != 0UL ? GetLaneIndexMask(laneIndex) : 0);

        /// <summary>
        /// Gets a new lane mask for the given lane index.
        /// </summary>
        /// <param name="laneIndex">The lane index.</param>
        /// <returns>The created activity lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityLaneMask Get(int laneIndex) =>
            new VelocityLaneMask(GetLaneIndexMask(laneIndex));

        /// <summary>
        /// Unifies two lane masks.
        /// </summary>
        /// <param name="left">The left lane mask.</param>
        /// <param name="right">The right lane mask.</param>
        /// <returns>
        /// A unified lane mask containing active lanes from both masks.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityLaneMask Unify(
            VelocityLaneMask left,
            VelocityLaneMask right) =>
            new VelocityLaneMask(left.mask | right.mask);

        /// <summary>
        /// Intersects two lane masks.
        /// </summary>
        /// <param name="left">The left lane mask.</param>
        /// <param name="right">The right lane mask.</param>
        /// <returns>
        /// An intersected lane mask containing only active lanes that are active in the
        /// left and the right masks.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityLaneMask Intersect(
            VelocityLaneMask left,
            VelocityLaneMask right) =>
            new VelocityLaneMask(left.mask & right.mask);

        /// <summary>
        /// Negates the given lane mask.
        /// </summary>
        /// <param name="mask">The lane mask to negate.</param>
        /// <returns>The negated version of the input lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityLaneMask Negate(VelocityLaneMask mask)
        {
            uint negatedMask = ~mask.mask & MaxNumberOfLanesMask;
            return new VelocityLaneMask(negatedMask);
        }

        /// <summary>
        /// Returns true if the given lane mask has at least one active lane.
        /// </summary>
        /// <param name="mask">The lane mask to text.</param>
        /// <returns>True if the given lane mask has at least one active lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasActiveLanes(VelocityLaneMask mask) => mask.HasAny;

        /// <summary>
        /// Returns true if all lanes in the given mask are considered active.
        /// </summary>
        /// <param name="mask">The lane mask to text.</param>
        /// <returns>
        /// True true if all lanes in the given mask are considered active.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreAllLanesActive(VelocityLaneMask mask) =>
            mask.Count == VelocityWarp32.Length;

        #endregion

        #region Instance

        private readonly uint mask;

        /// <summary>
        /// Constructs a new lane mask based on the given raw mask.
        /// </summary>
        /// <param name="rawMask">The raw lane mask.</param>
        internal VelocityLaneMask(uint rawMask)
        {
            mask = rawMask;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of active lanes in this mask.
        /// </summary>
        public int Count => IntrinsicMath.PopCount(mask);

        /// <summary>
        /// Returns true if this mask contains at least one active lane.
        /// </summary>
        public bool HasAny => (mask & MaxNumberOfLanesMask) != 0;

        #endregion

        #region Methods

        /// <summary>
        /// Returns a raw activity mask for the specified lane.
        /// </summary>
        /// <param name="laneIndex">The lane index to get the mask for.</param>
        /// <returns>The raw activity mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetActivityMaskI(int laneIndex)
        {
            VerifyLaneIndex(laneIndex);
            return IsActive(laneIndex) ? uint.MaxValue : 0;
        }

        /// <summary>
        /// Returns a raw activity mask for the specified lane.
        /// </summary>
        /// <param name="laneIndex">The lane index to get the mask for.</param>
        /// <returns>The raw activity mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetActivityMaskL(int laneIndex)
        {
            VerifyLaneIndex(laneIndex);
            return IsActive(laneIndex) ? ulong.MaxValue : 0L;
        }

        /// <summary>
        /// Returns true if the specified lane is active.
        /// </summary>
        /// <param name="laneIndex">The lane index to test.</param>
        /// <returns>True if the specified lane is active..</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsActive(int laneIndex)
        {
            VerifyLaneIndex(laneIndex);
            return (mask & GetLaneIndexMask(laneIndex)) != 0;
        }

        /// <summary>
        /// Disables the specified lane.
        /// </summary>
        /// <param name="laneIndex">The lane index to disable.</param>
        /// <returns>The updated lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityLaneMask Disable(int laneIndex)
        {
            VerifyLaneIndex(laneIndex);
            return new VelocityLaneMask(mask & ~GetLaneIndexMask(laneIndex));
        }

        /// <summary>
        /// Enables the specified lane.
        /// </summary>
        /// <param name="laneIndex">The lane index to enable.</param>
        /// <returns>The updated lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VelocityLaneMask Enable(int laneIndex)
        {
            VerifyLaneIndex(laneIndex);
            return new VelocityLaneMask(mask | GetLaneIndexMask(laneIndex));
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if both masks are equal.
        /// </summary>
        /// <param name="other">The other mask to compare to.</param>
        /// <returns>True if both masks are equal.</returns>
        public bool Equals(VelocityLaneMask other) => mask == other.mask;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the current mask is equal to the given object.
        /// </summary>
        /// <param name="other">The other object to compare to.</param>
        /// <returns>True if both objects represent the same mask.</returns>
        public override bool Equals(object other) =>
            other is VelocityLaneMask otherMask && Equals(otherMask);

        /// <summary>
        /// Returns true the hash code of this mask.
        /// </summary>
        /// <returns>The hash code of this mask.</returns>
        public override int GetHashCode() => mask.GetHashCode();

        /// <summary>
        /// Returns the bit string representation of the current mask.
        /// </summary>
        public override string ToString()
        {
            var baseArray = Convert.ToString(mask, 2).ToCharArray();
            Array.Reverse(baseArray);
            return new string(baseArray);
        }

        #endregion

        #region Operators

        /// <summary>
        /// Unifies two lane masks.
        /// </summary>
        /// <param name="left">The left lane mask.</param>
        /// <param name="right">The right lane mask.</param>
        /// <returns>
        /// A unified lane mask containing active lanes from both masks.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityLaneMask operator |(
            VelocityLaneMask left,
            VelocityLaneMask right) =>
            Unify(left, right);

        #endregion
    }

    partial class VelocityOperations
    {
        #region Lane Masks

        /// <summary>
        /// Initializes all lane mask emitters.
        /// </summary>
        private void InitVelocityLaneMaskEmitter()
        {
            var type = typeof(VelocityLaneMask);
            NoLanesMask = GetField(type, nameof(VelocityLaneMask.None));
            AllLanesMask = GetField(type, nameof(VelocityLaneMask.All));
            UnifyLanesMask = GetMethod(type, nameof(VelocityLaneMask.Unify));
            IntersectLanesMask = GetMethod(type, nameof(VelocityLaneMask.Intersect));
            NegateLaneMask = GetMethod(type, nameof(VelocityLaneMask.Negate));
            MaskHasActiveLanes = GetMethod(type, nameof(VelocityLaneMask.HasActiveLanes));
            AreAllLanesActive = GetMethod(
                type,
                nameof(VelocityLaneMask.AreAllLanesActive));
        }

        /// <summary>
        /// Returns the no-lane mask getter method.
        /// </summary>
        public FieldInfo NoLanesMask { get; private set; }

        /// <summary>
        /// Returns the all-lane mask getter method.
        /// </summary>
        public FieldInfo AllLanesMask { get; private set; }

        /// <summary>
        /// Returns the unify masks method.
        /// </summary>
        public MethodInfo UnifyLanesMask { get; private set; }

        /// <summary>
        /// Returns the intersect masks method.
        /// </summary>
        public MethodInfo IntersectLanesMask { get; private set; }

        /// <summary>
        /// Returns the negate masks method.
        /// </summary>
        public MethodInfo NegateLaneMask { get; private set; }

        /// <summary>
        /// Returns the method to test whether a given lane mask has active lanes.
        /// </summary>
        public MethodInfo MaskHasActiveLanes { get; private set; }

        /// <summary>
        /// Returns the method to test whether all lanes are active.
        /// </summary>
        public MethodInfo AreAllLanesActive { get; private set; }

        #endregion
    }
}
