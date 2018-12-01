// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: VectorExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents extension methods for vectors.
    /// </summary>
    public static class VectorExtensions
    {
        #region Offsets

        /// <summary>
        /// Represents the offset of the x-component of a <see cref="Vector2"/>.
        /// </summary>
        public static readonly int Vector2XOffset = Interop.OffsetOf<Vector2>(nameof(Vector2.X));

        /// <summary>
        /// Represents the offset of the y-component of a <see cref="Vector2"/>.
        /// </summary>
        public static readonly int Vector2YOffset = Interop.OffsetOf<Vector2>(nameof(Vector2.Y));

        /// <summary>
        /// Represents the offset of the x-component of a <see cref="Vector3"/>.
        /// </summary>
        public static readonly int Vector3XOffset = Interop.OffsetOf<Vector3>(nameof(Vector3.X));

        /// <summary>
        /// Represents the offset of the y-component of a <see cref="Vector3"/>.
        /// </summary>
        public static readonly int Vector3YOffset = Interop.OffsetOf<Vector3>(nameof(Vector3.Y));

        /// <summary>
        /// Represents the offset of the z-component of a <see cref="Vector3"/>.
        /// </summary>
        public static readonly int Vector3ZOffset = Interop.OffsetOf<Vector3>(nameof(Vector3.Z));

        /// <summary>
        /// Represents the offset of the x-component of a <see cref="Vector4"/>.
        /// </summary>
        public static readonly int Vector4XOffset = Interop.OffsetOf<Vector4>(nameof(Vector4.X));

        /// <summary>
        /// Represents the offset of the y-component of a <see cref="Vector4"/>.
        /// </summary>
        public static readonly int Vector4YOffset = Interop.OffsetOf<Vector4>(nameof(Vector4.Y));

        /// <summary>
        /// Represents the offset of the z-component of a <see cref="Vector4"/>.
        /// </summary>
        public static readonly int Vector4ZOffset = Interop.OffsetOf<Vector4>(nameof(Vector4.Z));

        /// <summary>
        /// Represents the offset of the w-component of a <see cref="Vector4"/>.
        /// </summary>
        public static readonly int Vector4WOffset = Interop.OffsetOf<Vector4>(nameof(Vector4.W));

        #endregion

        #region Atomics

        /// <summary>
        /// Atommically adds the given operand and the vector at the target location.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="operand">The operand to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AtomicAdd(this VariableView<Vector2> target, Vector2 operand)
        {
            Atomic.Add(ref target.GetSubView<float>(Vector2XOffset).Value, operand.X);
            Atomic.Add(ref target.GetSubView<float>(Vector2YOffset).Value, operand.Y);
        }

        /// <summary>
        /// Atommically adds the given operand and the vector at the target location.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="operand">The operand to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AtomicAdd(this VariableView<Vector3> target, Vector3 operand)
        {
            Atomic.Add(ref target.GetSubView<float>(Vector3XOffset).Value, operand.X);
            Atomic.Add(ref target.GetSubView<float>(Vector3YOffset).Value, operand.Y);
            Atomic.Add(ref target.GetSubView<float>(Vector3ZOffset).Value, operand.Z);
        }

        /// <summary>
        /// Atommically adds the given operand and the vector at the target location.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="operand">The operand to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AtomicAdd(this VariableView<Vector4> target, Vector4 operand)
        {
            Atomic.Add(ref target.GetSubView<float>(Vector4XOffset).Value, operand.X);
            Atomic.Add(ref target.GetSubView<float>(Vector4YOffset).Value, operand.Y);
            Atomic.Add(ref target.GetSubView<float>(Vector4ZOffset).Value, operand.Z);
            Atomic.Add(ref target.GetSubView<float>(Vector4WOffset).Value, operand.W);
        }

        #endregion

        #region Conversions

        /// <summary>
        /// Converts the index to a corresponding <see cref="Vector2"/>.
        /// </summary>
        /// <param name="index">The source index.</param>
        /// <returns>The converted <see cref="Vector2"/>.</returns>
        public static Vector2 ToVector(this Index2 index)
        {
            return new Vector2(index.X, index.Y);
        }

        /// <summary>
        /// Converts the index to a corresponding <see cref="Vector3"/>.
        /// </summary>
        /// <param name="index">The source index.</param>
        /// <returns>The converted <see cref="Vector3"/>.</returns>
        public static Vector3 ToVector(this Index3 index)
        {
            return new Vector3(index.X, index.Y, index.Z);
        }

        /// <summary>
        /// Converts the vector to a corresponding <see cref="Index2"/>.
        /// </summary>
        /// <param name="vector">The source vector.</param>
        /// <returns>The converted <see cref="Index2"/>.</returns>
        public static Index2 ToIndex(this Vector2 vector)
        {
            return new Index2((int)vector.X, (int)vector.Y);
        }

        /// <summary>
        /// Converts the vector to a corresponding <see cref="Index3"/>.
        /// </summary>
        /// <param name="vector">The source vector.</param>
        /// <returns>The converted <see cref="Index3"/>.</returns>
        public static Index3 ToIndex(this Vector3 vector)
        {
            return new Index3((int)vector.X, (int)vector.Y, (int)vector.Z);
        }

        #endregion
    }
}
