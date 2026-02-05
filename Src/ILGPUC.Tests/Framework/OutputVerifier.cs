// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: OutputVerifier.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using Xunit;

using static System.Globalization.CultureInfo;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Compares actual stdout lines to expected output.
/// </summary>
static class OutputVerifier
{
    /// <summary>
    /// Verifies that actual output lines match expected lines exactly.
    /// </summary>
    public static void Verify(string[] actual, string[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(
                expected[i] == actual[i],
                $"Line {i}: expected \"{expected[i]}\" but got \"{actual[i]}\"");
        }
    }

    /// <summary>
    /// Verifies output with float tolerance for GPU backends.
    /// Lines that parse as floating-point are compared within backend-appropriate
    /// precision. Other lines are compared exactly.
    /// </summary>
    public static void VerifyWithTolerance(
        string[] actual,
        string[] expected,
        BackendType backend)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            if (double.TryParse(expected[i], InvariantCulture, out var expectedVal)
                && double.TryParse(actual[i], InvariantCulture, out var actualVal))
            {
                var digits = FloatTolerance.DoubleDigits(backend);
                Assert.Equal(expectedVal, actualVal, digits);
            }
            else
            {
                Assert.True(
                    expected[i] == actual[i],
                    $"Line {i}: expected \"{expected[i]}\" but got \"{actual[i]}\"");
            }
        }
    }
}
