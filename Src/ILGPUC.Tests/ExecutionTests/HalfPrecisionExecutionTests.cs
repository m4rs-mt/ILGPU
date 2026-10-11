// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfPrecisionExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// System.Half in kernels: conversions and memory. The expected values are the
/// results of the same computations with System.Half on the host.
/// </summary>
public abstract class HalfPrecisionExecutionTests : ExecutionTestBase
{
    protected HalfPrecisionExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    /// <summary>
    /// Conversions between Half and the other primitive types round to
    /// nearest even and match the host.
    /// </summary>
    [SkippableFact]
    public async Task HalfConversions_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float16);
        await VerifyProgramOutputAsync(
            "TestPrograms/HalfPrecision/HalfConversions.cs",
            ["Kernels.HalfConversionsKernel"],
            [
                "15360", "1065353216", "1", "1", "1", "1", "1", "1", "0", "0", "0",
                "49408", "-1071644672", "-2", "-2", "-2", "2", "2", "-2", "15360",
                "15360", "15360", "11878", "1036828672", "0", "0", "0", "0", "0", "0",
                "48128", "48128", "23544", "31743", "1199562752", "60000", "60000",
                "30000", "60000", "255", "100", "26624", "26624", "15360", "31744",
                "2139095040", "60000", "60000", "30000", "60000", "255", "100", "26626",
                "26626", "16896", "32770", "-1275068416", "0", "0", "0", "0", "0", "0",
                "31743", "31743", "23296", "26624", "1157627904", "2048", "2048", "2048",
                "2048", "255", "100", "31744", "31744", "22272", "26626", "1157644288",
                "2052", "2052", "2052", "2052", "255", "100", "56496", "56496", "23200",
                "16968", "1078525952", "3", "3", "3", "3", "3", "3", "23544", "23544",
                "23544", "32768", "-2147483648", "0", "0", "0", "0", "0", "0", "25600",
                "25600", "0", "168", "925368320", "0", "0", "0", "0", "0", "0", "59392",
                "59392", "0", "22524", "1124040704", "127", "127", "127", "127", "127",
                "100", "29191", "29191", "21280"
            ]);
    }

    /// <summary>
    /// Conversions between Half and double round to nearest even and match
    /// the host.
    /// </summary>
    [SkippableFact]
    public async Task HalfDoubleConversions_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float16);
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/HalfPrecision/HalfDoubleConversions.cs",
            ["Kernels.HalfDoubleConversionsKernel"],
            [
                "1000", "15360", "16896", "-2500", "49408", "51072", "99", "11878",
                "13517", "65504000", "31743", "31744", "9223372036854775807", "31744",
                "31744", "0", "32770", "32773", "2048000", "26624", "28161", "2052000",
                "26626", "28162", "3140", "16968", "18614", "0", "32768", "32768", "0",
                "168", "503", "127750", "22524", "24061"
            ]);
    }

    /// <summary>
    /// Half values round-trip through views, structures and uniform kernel
    /// parameters, and the bit casts reinterpret their bits.
    /// </summary>
    [SkippableFact]
    public async Task HalfMemory_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float16);
        await VerifyProgramOutputAsync(
            "TestPrograms/HalfPrecision/HalfMemory.cs",
            ["Kernels.HalfMemoryKernel"],
            [
                "19152", "-17408", "19152", "16384", "18768", "-17407", "18768", "16386",
                "18336", "-17406", "18336", "16388", "17568", "-17405", "17568", "16390",
                "16000", "-17404", "16000", "16392", "48512", "-17403", "48512", "16394",
                "50272", "-17402", "50272", "16396", "51040", "-17401", "51040", "16398",
                "51504", "-17400", "51504", "16400", "51888", "-17399", "51888", "16402",
                "52248", "-17398", "52248", "16404", "52440", "-17397", "52440", "16406",
                "19152", "18768", "18336", "17568", "16000", "48512", "50272", "51040",
                "51504", "51888", "52248", "52440"
            ]);
    }
}
