// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfMemberTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that every public member of <see cref="Half"/>, including the explicit
/// implementations of the generic math interfaces, either has a kernel
/// implementation (a remapping or a code generator) or is rejected with a compile
/// error, so that no member compiles from its BCL implementation (#1606).
/// </summary>
public sealed class HalfMemberTests
{
    /// <summary>
    /// The members that kernels reject: parsing and formatting, the comparisons
    /// with boxed values, the conversions with decimal, Int128 and UInt128, and the
    /// span writers of IFloatingPoint.
    /// </summary>
    private static readonly string[] RejectedMembers =
    [
        "Int32 CompareTo(System.Object)",
        "Boolean Equals(System.Object)",
        "System.Int128 op_CheckedExplicit(System.Half)",
        "System.UInt128 op_CheckedExplicit(System.Half)",
        "System.Decimal op_Explicit(System.Half)",
        "System.Half op_Explicit(System.Decimal)",
        "System.Int128 op_Explicit(System.Half)",
        "System.UInt128 op_Explicit(System.Half)",
        "System.Half Parse(System.ReadOnlySpan`1[System.Byte], " +
            "System.Globalization.NumberStyles, System.IFormatProvider)",
        "System.Half Parse(System.ReadOnlySpan`1[System.Byte], System.IFormatProvider)",
        "System.Half Parse(System.ReadOnlySpan`1[System.Char], " +
            "System.Globalization.NumberStyles, System.IFormatProvider)",
        "System.Half Parse(System.ReadOnlySpan`1[System.Char], System.IFormatProvider)",
        "System.Half Parse(System.String, System.Globalization.NumberStyles, " +
            "System.IFormatProvider)",
        "System.Half Parse(System.String, System.Globalization.NumberStyles)",
        "System.Half Parse(System.String, System.IFormatProvider)",
        "System.Half Parse(System.String)",
        "Boolean System.Numerics.IFloatingPoint<System.Half>." +
            "TryWriteExponentBigEndian(System.Span`1[System.Byte], Int32 ByRef)",
        "Boolean System.Numerics.IFloatingPoint<System.Half>." +
            "TryWriteExponentLittleEndian(System.Span`1[System.Byte], Int32 ByRef)",
        "Boolean System.Numerics.IFloatingPoint<System.Half>." +
            "TryWriteSignificandBigEndian(System.Span`1[System.Byte], Int32 ByRef)",
        "Boolean System.Numerics.IFloatingPoint<System.Half>." +
            "TryWriteSignificandLittleEndian(System.Span`1[System.Byte], Int32 ByRef)",
        "System.String ToString()",
        "System.String ToString(System.IFormatProvider)",
        "System.String ToString(System.String, System.IFormatProvider)",
        "System.String ToString(System.String)",
        "Boolean TryFormat(System.Span`1[System.Byte], Int32 ByRef, " +
            "System.ReadOnlySpan`1[System.Char], System.IFormatProvider)",
        "Boolean TryFormat(System.Span`1[System.Char], Int32 ByRef, " +
            "System.ReadOnlySpan`1[System.Char], System.IFormatProvider)",
        "Boolean TryParse(System.ReadOnlySpan`1[System.Byte], " +
            "System.Globalization.NumberStyles, System.IFormatProvider, " +
            "System.Half ByRef)",
        "Boolean TryParse(System.ReadOnlySpan`1[System.Byte], System.Half ByRef)",
        "Boolean TryParse(System.ReadOnlySpan`1[System.Byte], System.IFormatProvider, " +
            "System.Half ByRef)",
        "Boolean TryParse(System.ReadOnlySpan`1[System.Char], " +
            "System.Globalization.NumberStyles, System.IFormatProvider, " +
            "System.Half ByRef)",
        "Boolean TryParse(System.ReadOnlySpan`1[System.Char], System.Half ByRef)",
        "Boolean TryParse(System.ReadOnlySpan`1[System.Char], System.IFormatProvider, " +
            "System.Half ByRef)",
        "Boolean TryParse(System.String, System.Globalization.NumberStyles, " +
            "System.IFormatProvider, System.Half ByRef)",
        "Boolean TryParse(System.String, System.Half ByRef)",
        "Boolean TryParse(System.String, System.IFormatProvider, System.Half ByRef)",
    ];

    /// <summary>
    /// Returns the public methods of <see cref="Half"/> and its explicit
    /// implementations of public interfaces.
    /// </summary>
    private static IEnumerable<MethodInfo> GetPublicMembers()
    {
        var explicitImplementations = typeof(Half).GetInterfaces()
            .Where(type => type.IsPublic)
            .SelectMany(type => typeof(Half).GetInterfaceMap(type).TargetMethods)
            .Where(method => method.DeclaringType == typeof(Half) && method.IsPrivate)
            .ToHashSet();
        return typeof(Half)
            .GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.IsPublic || explicitImplementations.Contains(method));
    }

    /// <summary>
    /// Every public member of Half is remapped, generated or rejected, and the
    /// rejected members are exactly the expected ones. A member added by a new
    /// .NET version is rejected until it is implemented, which this test reports.
    /// </summary>
    [Fact]
    public void EveryMember_IsImplementedOrRejected()
    {
        var rejected = new List<string>();
        int implemented = 0;
        foreach (var method in GetPublicMembers())
        {
            if (Intrinsics.IsUnsupportedMethod(method))
            {
                rejected.Add(method.ToString()!);
                continue;
            }
            Assert.True(
                Intrinsics.HasIntrinsicRemapping(method) ||
                Intrinsics.HasIntrinsicGenerator(method),
                $"'{method}' is neither implemented nor rejected");
            ++implemented;
        }

        Assert.Equal(
            RejectedMembers.Order(StringComparer.Ordinal),
            rejected.Order(StringComparer.Ordinal));
        Assert.True(implemented > 150, $"Only {implemented} members are implemented");
    }

    /// <summary>
    /// Kernels that call rejected members. Defined here, outside <c>Kernels/</c>,
    /// so that the kernel registry does not pick them up.
    /// </summary>
    private static class ErrorKernels
    {
        public static void ToDecimal(
            Index1D index,
            ArrayView1D<Half, Stride1D.Dense> data,
            ArrayView1D<long, Stride1D.Dense> output)
        {
            output[index] = (long)(decimal)data[index];
        }

        public static void ToInt128(
            Index1D index,
            ArrayView1D<Half, Stride1D.Dense> data,
            ArrayView1D<long, Stride1D.Dense> output)
        {
            output[index] = (long)(Int128)data[index];
        }
    }

    /// <summary>
    /// A call to a rejected member is a compile error that names the member.
    /// </summary>
    [Theory]
    [InlineData(nameof(ErrorKernels.ToDecimal), "op_Explicit")]
    [InlineData(nameof(ErrorKernels.ToInt128), "op_Explicit")]
    public void RejectedMember_IsCompileError(string kernelName, string memberName)
    {
        var kernel = typeof(ErrorKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        using var helper = new CompilationHelper(BackendType.CPU);
        var exception = Assert.ThrowsAny<Exception>(() =>
            helper.GenerateBackendCode(kernel, BackendType.CPU));

        var messages = new List<string>();
        for (
            var current = exception;
            current is not null;
            current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        var text = string.Join("\n", messages);
        Assert.Contains("Not supported runtime method", text, StringComparison.Ordinal);
        Assert.Contains(memberName, text, StringComparison.Ordinal);
    }
}
