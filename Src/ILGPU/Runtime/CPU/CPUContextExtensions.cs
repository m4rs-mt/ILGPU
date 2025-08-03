// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUContextExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// CPU-specific context and builder extensions.
/// </summary>
public static class CPUContextExtensions
{
    #region Builder

    /// <summary>
    /// Enables the default CPU device for vectorized CPU execution.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <returns>The updated builder instance.</returns>
    public static Context.Builder CPU(this Context.Builder builder)
    {
        builder.DeviceRegistry.Register(CPUDevice.Default);
        return builder;
    }

    /// <summary>
    /// Enables a CPU device with a custom vector width for emulated execution.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <param name="vectorWidth">
    /// The SIMD vector width (number of lanes). Must be a power of two
    /// and greater than zero.
    /// </param>
    /// <returns>The updated builder instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="vectorWidth"/> is not a power of two or is zero.
    /// </exception>
    public static Context.Builder CPU(
        this Context.Builder builder,
        int vectorWidth)
    {
        if (vectorWidth <= 0 || (vectorWidth & (vectorWidth - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(vectorWidth),
                vectorWidth,
                "Vector width must be a power of two and greater than zero.");
        }

        builder.DeviceRegistry.Register(CPUDevice.Emulated(vectorWidth));
        return builder;
    }

    #endregion
}
