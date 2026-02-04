// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloatTolerance.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using Xunit;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Per-backend float/double precision tolerances from the old test suite.
/// </summary>
static class FloatTolerance
{
    /// <summary>
    /// Returns the number of significant digits for float on the given backend.
    /// </summary>
    public static int FloatDigits(BackendType backend) => backend switch
    {
        BackendType.CPU => 6,
        BackendType.Cuda => 4,
        BackendType.Metal => 4,
        BackendType.ROCm => 4,
        BackendType.OpenCL => 3,
        _ => 6,
    };

    /// <summary>
    /// Returns the number of significant digits for double on the given backend.
    /// </summary>
    public static int DoubleDigits(BackendType backend) => backend switch
    {
        BackendType.CPU => 15,
        BackendType.Cuda => 12,
        BackendType.Metal => 12,
        BackendType.ROCm => 12,
        BackendType.OpenCL => 10,
        _ => 15,
    };

    /// <summary>
    /// Asserts float equality with backend-appropriate precision.
    /// </summary>
    public static void AssertEqual(float expected, float actual, BackendType backend)
    {
        if (float.IsNaN(expected))
        {
            Assert.True(float.IsNaN(actual), $"Expected NaN but got {actual}");
            return;
        }
        if (float.IsPositiveInfinity(expected))
        {
            Assert.True(float.IsPositiveInfinity(actual),
                $"Expected +Inf but got {actual}");
            return;
        }
        if (float.IsNegativeInfinity(expected))
        {
            Assert.True(float.IsNegativeInfinity(actual),
                $"Expected -Inf but got {actual}");
            return;
        }

        Assert.Equal(expected, actual, FloatDigits(backend));
    }

    /// <summary>
    /// Asserts double equality with backend-appropriate precision.
    /// </summary>
    public static void AssertEqual(double expected, double actual, BackendType backend)
    {
        if (double.IsNaN(expected))
        {
            Assert.True(double.IsNaN(actual), $"Expected NaN but got {actual}");
            return;
        }
        if (double.IsPositiveInfinity(expected))
        {
            Assert.True(double.IsPositiveInfinity(actual),
                $"Expected +Inf but got {actual}");
            return;
        }
        if (double.IsNegativeInfinity(expected))
        {
            Assert.True(double.IsNegativeInfinity(actual),
                $"Expected -Inf but got {actual}");
            return;
        }

        Assert.Equal(expected, actual, DoubleDigits(backend));
    }
}
