// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: VectorArithmeticExecutionTests.cs
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

public abstract class VectorArithmeticExecutionTests : ExecutionTestBase
{
    protected VectorArithmeticExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task DivisorReuse_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/DivisorReuse.cs",
            ["Kernels.DivisorReuseKernel"],
            [
                "5", "6", "7", "8", "11009", "10010", "9011", "8012", "7013", "5", "6",
                "7"
            ]);
    }

    [SkippableFact]
    public async Task UniformDivisorInactiveBranch_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/UniformDivisorInactiveBranch.cs",
            ["Kernels.UniformDivisorInactiveBranchKernel"],
            ["1", "2", "3", "4", "5", "6", "7", "8", "9", "1", "2", "3"]);
    }

    [SkippableFact]
    public async Task UnsignedIntOps_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/UnsignedIntOps.cs",
            ["Kernels.UnsignedIntOpsKernel"],
            [
                "2147483647", "3622198816", "2517136235", "5005755640", "2886788825",
                "2288678374", "4150670494", "2658330963", "5534227325", "2147483647",
                "3622198816", "2517136235"
            ]);
    }

    [SkippableFact]
    public async Task UnsignedLongOps_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/UnsignedLongOps.cs",
            ["Kernels.UnsignedLongOpsKernel"],
            [
                "0", "3889306357633695323", "1485582835955017562", "5374889193588712874",
                "2971165671910035113", "567442150231357353", "4456748507865052677",
                "2053024986186374906", "5942331343820070229", "0", "3889306357633695323",
                "1485582835955017562"
            ]);
    }

    [SkippableFact]
    public async Task UnsignedUniformOps_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/UnsignedUniformOps.cs",
            ["Kernels.UnsignedUniformOpsKernel"],
            [
                "1565873493", "1565873494", "1565873495", "1565873496", "1565873497",
                "1565873498", "1565873499", "1565873500", "1565873501", "1565873493",
                "1565873494", "1565873495"
            ]);
    }

    [SkippableFact]
    public async Task UnsignedCompare_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/UnsignedCompare.cs",
            ["Kernels.UnsignedCompareKernel"],
            ["5", "5", "5", "5", "2", "2", "2", "1", "1", "5", "5", "5"]);
    }

    [SkippableFact]
    public async Task SmallIntegerOps_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/SmallIntegerOps.cs",
            ["Kernels.SmallIntegerOpsKernel"],
            [
                "0", "1356532", "2714064", "4071607", "5427390", "6784934", "8142467",
                "8563813", "558343", "0", "1356532", "2714064"
            ]);
    }

    [SkippableFact]
    public async Task SmallIntegerWrap_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/SmallIntegerWrap.cs",
            ["Kernels.SmallIntegerWrapKernel"],
            [
                "100200", "15000120237", "29999884018", "-20536095945", "-5536075908",
                "9463944129", "24463964166", "-26072015797", "-11071995760", "100200",
                "15000120237", "29999884018"
            ]);
    }

    [SkippableFact]
    public async Task SignExtension_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/SignExtension.cs",
            ["Kernels.SignExtensionKernel"],
            [
                "0", "37009000", "74018000", "111027000", "-108029536", "-71020536",
                "-34011536", "2997464", "40006464", "0", "37009000", "74018000"
            ]);
    }

    [SkippableFact]
    public async Task WideShifts_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/WideShifts.cs",
            ["Kernels.WideShiftsKernel"],
            [
                "0", "2305843007066210303", "-9223372036854775808",
                "-9223372036854775804", "24", "52", "120", "280", "639", "0",
                "2305843007066210303", "-9223372036854775808"
            ]);
    }

    [SkippableFact]
    public async Task UnsignedMinMax_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/UnsignedMinMax.cs",
            ["Kernels.UnsignedMinMaxKernel"],
            [
                "4294967294", "4294967294", "4294967294", "4294967294", "3", "12", "14",
                "16", "19", "4294967294", "4294967294", "4294967294"
            ]);
    }

    [SkippableFact]
    public async Task BoolOps_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/BoolOps.cs",
            ["Kernels.BoolOpsKernel"],
            ["12", "1", "12", "14", "5", "14", "5", "14", "5", "12", "1", "12"]);
    }

    [SkippableFact]
    public async Task NaNConstant_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/NaNConstant.cs",
            ["Kernels.NaNConstantKernel"],
            ["0", "5", "10", "-1", "20", "25", "30", "35", "40", "0", "5", "10"]);
    }

    [SkippableFact]
    public async Task HalfArithmetic_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float16);
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorArithmetic/HalfArithmetic.cs",
            ["Kernels.HalfArithmeticKernel"],
            [
                "125", "150", "225", "350", "525", "750", "1025", "1350", "1725", "125",
                "150", "225"
            ]);
    }
}
