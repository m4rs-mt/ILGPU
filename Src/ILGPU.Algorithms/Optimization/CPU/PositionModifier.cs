// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: PositionModifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.Optimization.CPU
{
    /// <summary>
    /// Represents an abstract modifier for player/particle positions during optimization.
    /// This allows users to implement specific clamping, rounding, or adjustments
    /// during an optimization run.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface ICPUPositionModifier<T>
        where T : unmanaged
    {
        /// <summary>
        /// Adjusts the given player/particle position according to user- and domain-
        /// specific constraints.
        /// </summary>
        /// <param name="index">The current player/particle index.</param>
        /// <param name="position">The position to adjust (if desired).</param>
        /// <param name="numDimensions">The raw dimensions of the input problem.</param>
        /// <param name="numPaddedDimensions">The padded number of dimensions.</param>
        /// <remarks>
        /// The length of the position memory will be equal to the input problem
        /// dimension in case of a scalar optimizer. If the optimizer has been created
        /// for vector-based execution, the position memory length will be padded
        /// according to the vector length. If you want to use vector instructions inside
        /// this function, make sure to create a vectorized optimizer or account for
        /// non-optimized memory lengths.
        /// </remarks>
        void AdjustPosition(
            int index,
            Memory<T> position,
            int numDimensions,
            int numPaddedDimensions);
    }

    /// <summary>
    /// Static utility class for <see cref="ICPUPositionModifier{T}"/> interfaces.
    /// </summary>
    public static class CPUPositionModifier
    {
        /// <summary>
        /// Represents a nop position modifier.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        public readonly struct Nop<T> : ICPUPositionModifier<T>
            where T : unmanaged
        {
            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AdjustPosition(
                int index,
                Memory<T> position,
                int numDimensions,
                int numPaddedDimensions)
            { }
        }

        /// <summary>
        /// Rounds floating point values according to the given number of digits.
        /// </summary>
        /// <param name="NumDigits">The number of digits to round to.</param>
        /// <param name="MidpointRounding">The midpoint rounding mode.</param>
        public readonly record struct FloatRoundingModifier(
            int NumDigits,
            MidpointRounding MidpointRounding) :
            ICPUPositionModifier<float>
        {
            /// <summary>
            /// Rounds the given position according to the specified number of digits.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AdjustPosition(
                int index,
                Memory<float> position,
                int numDimensions,
                int numPaddedDimensions)
            {
                var span = position.Span;
                for (int i = 0; i < numDimensions; ++i)
                    span[i] = XMath.Round(span[i], NumDigits, MidpointRounding);
            }
        }

        /// <summary>
        /// Rounds floating point values according to the given number of digits.
        /// </summary>
        /// <param name="NumDigits">The number of digits to round to.</param>
        /// <param name="MidpointRounding">The midpoint rounding mode.</param>
        public readonly record struct DoubleRoundingModifier(
            int NumDigits,
            MidpointRounding MidpointRounding) :
            ICPUPositionModifier<double>
        {
            /// <summary>
            /// Rounds the given position according to the specified number of digits.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AdjustPosition(
                int index,
                Memory<double> position,
                int numDimensions,
                int numPaddedDimensions)
            {
                var span = position.Span;
                for (int i = 0; i < numDimensions; ++i)
                    span[i] = XMath.Round(span[i], NumDigits, MidpointRounding);
            }
        }

        /// <summary>
        /// Returns a new no-operation CPU position modifier.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>A new Nop position modifier.</returns>
        public static Nop<T> GetNop<T>() where T : unmanaged => new();

        /// <summary>
        /// Returns a new float rounding modifier.
        /// </summary>
        /// <param name="numDigits">The number of digits to round to.</param>
        /// <param name="midpointRounding">The midpoint rounding mode.</param>
        /// <returns>A new rounding modifier.</returns>
        public static FloatRoundingModifier GetFloatRounding(
            int numDigits,
            MidpointRounding midpointRounding = MidpointRounding.ToEven)
        {
            if (numDigits < 0)
                throw new ArgumentOutOfRangeException(nameof(numDigits));
            return new(numDigits, midpointRounding);
        }

        /// <summary>
        /// Returns a new double rounding modifier.
        /// </summary>
        /// <param name="numDigits">The number of digits to round to.</param>
        /// <param name="midpointRounding">The midpoint rounding mode.</param>
        /// <returns>A new rounding modifier.</returns>
        public static DoubleRoundingModifier GetDoubleRounding(
            int numDigits,
            MidpointRounding midpointRounding = MidpointRounding.ToEven)
        {
            if (numDigits < 0)
                throw new ArgumentOutOfRangeException(nameof(numDigits));
            return new(numDigits, midpointRounding);
        }
    }
}
