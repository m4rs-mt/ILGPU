// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayView2DExecutionTests.cs
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
/// Pins the Index2D-launch + ArrayView2D round-trip semantics raised in #1464:
/// for a buffer viewed as <c>ArrayView2D&lt;T, Stride2D.DenseX/Y&gt;</c>, the
/// kernel-side <c>view[Index2D(x, y)]</c> indexer must agree with the linear
/// memory layout (<c>y * W + x</c> for DenseX, <c>x * H + y</c> for DenseY).
/// Both tests build the same kernel encoding (<c>view[index] = X * 10 + Y</c>)
/// and read back through the underlying 1D buffer, so the assertion is a
/// direct check of the Stride2D layout contract.
/// </summary>
/// <remarks>
/// Caught a real launcher-marshaling regression while bringing these tests up:
/// the test framework's <c>ProgramBuilder.ExtractFieldAccessors</c> did not
/// recursively flatten nested struct fields, so <c>ArrayView2D</c>'s IR
/// decomposition (<c>BaseView</c>, <c>Extent.X</c>, <c>Extent.Y</c>,
/// <c>Stride.YStride</c>) was emitted as <c>BaseView</c>, <c>Extent</c>,
/// <c>Stride</c> — three accessors mapped against four IR slots, producing
/// invalid C# (<c>Field3</c> dangling, <c>LongIndex2D</c>/<c>Stride2D.DenseX</c>
/// directly assigned to <c>long</c>). The fix routes the test framework
/// through <c>LauncherStubGenerator.ExtractFieldAccessors</c>, the same helper
/// the production <c>ilgpuc build</c> path uses. The leaf-flattening contract
/// is independently pinned in
/// <c>NonKernelTests/LauncherFieldAccessorFlatteningTests</c>.
/// </remarks>
public abstract class ArrayView2DExecutionTests : ExecutionTestBase
{
    protected ArrayView2DExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    private static readonly string[] ExpectedRoundTripOutput =
    [
        "x=0,y=0:0",
        "x=0,y=1:1",
        "x=1,y=0:10",
        "x=1,y=1:11",
        "x=2,y=0:20",
        "x=2,y=1:21",
    ];

    [SkippableFact]
    public async Task DenseXRoundTrip_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView2D/DenseXRoundTrip.cs",
            ["Kernels.DenseXKernel"],
            ExpectedRoundTripOutput);
    }

    [SkippableFact]
    public async Task DenseYRoundTrip_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView2D/DenseYRoundTrip.cs",
            ["Kernels.DenseYKernel"],
            ExpectedRoundTripOutput);
    }

    [SkippableFact]
    public async Task Allocate2DDenseXRoundTrip_ProducesCorrectOutput()
    {
        // The exact #1464 user pattern: native Allocate2DDenseX +
        // Index2D launch + GetAsArray2D readback. Same expected output as
        // DenseXRoundTrip — the difference is the allocator and readback
        // path, which the reinterpret-based tests don't cover.
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView2D/Allocate2DDenseXRoundTrip.cs",
            ["Kernels.Allocate2DDenseXKernel"],
            ExpectedRoundTripOutput);
    }

    [SkippableFact]
    public async Task GeneralRoundTrip_ProducesCorrectOutput()
    {
        // General stride with XStride=1, YStride=W → DenseX-equivalent layout.
        // Same expected output as DenseX — the General wrapper just exercises
        // the launcher's ability to marshal both strides plus the deeper
        // nested StrideExtent auto-property.
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView2D/GeneralRoundTrip.cs",
            ["Kernels.GeneralKernel"],
            ExpectedRoundTripOutput);
    }

    [SkippableFact]
    public async Task PaddedBitmap_ProducesCorrectOutput()
    {
        // Canonical bitmap-with-row-padding pattern from #1464: row stride
        // (5) > extent.X (3), so 2 padding slots per row stay at zero. The
        // expected output enumerates the full padded buffer (5 cols × 2 rows)
        // — the padding zeros make the row-stride structure visible.
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView2D/PaddedBitmap.cs",
            ["Kernels.PaddedKernel"],
            [
                "y=0,col=0:1",
                "y=0,col=1:11",
                "y=0,col=2:21",
                "y=0,col=3:0",
                "y=0,col=4:0",
                "y=1,col=0:2",
                "y=1,col=1:12",
                "y=1,col=2:22",
                "y=1,col=3:0",
                "y=1,col=4:0",
            ]);
    }
}

/// <summary>
/// 3D analogue of <see cref="ArrayView2DExecutionTests"/>: pins the Index3D-launch
/// + ArrayView3D round-trip for both stride flavours. The kernel encodes
/// <c>X * 100 + Y * 10 + Z</c> so each axis is digit-identifiable; the host
/// reads back through the underlying 1D buffer to verify both the indexer
/// (<c>view[Index3D(x, y, z)]</c>) and the linear memory order
/// (<c>z * W * H + y * W + x</c> for DenseXY, <c>x * H * D + y * D + z</c> for
/// DenseZY). DenseXY is the more common "X-fast / row-major-like" layout;
/// DenseZY swaps to "Z-fast" and is useful for column-major numerics or when
/// porting Fortran-style kernels.
/// </summary>
/// <remarks>
/// Bringing these tests up exposed a second emitter bug — the 3D indexer
/// dispatch leaves a <c>GetField</c> with <c>FieldSpan.Span &gt; 1</c> in the
/// IR (extracting the <c>Stride3D.*</c> wrapper, two int slots, from the
/// flattened parameter struct). <c>ExpressionEmitter.EmitGetField</c> used
/// to emit that as a single <c>source.Field{N}</c> access, which yields a
/// primitive while the IR Value's declared type is still the sub-struct —
/// the resulting <c>tmp_0 = source.Field4;</c> failed Roslyn with CS0029
/// (int → struct). Fixed in <c>EmitGetField</c> by emitting a struct literal
/// that copies each constituent flat field when <c>Span &gt; 1</c>. The 2D
/// path never tripped because <c>Stride2D.DenseX/Y</c> has only one non-unit
/// stride field, so the multi-field load collapses to <c>Span == 1</c>.
/// </remarks>
public abstract class ArrayView3DExecutionTests : ExecutionTestBase
{
    protected ArrayView3DExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    private static readonly string[] ExpectedRoundTripOutput =
    [
        "x=0,y=0,z=0:0",
        "x=0,y=0,z=1:1",
        "x=0,y=1,z=0:10",
        "x=0,y=1,z=1:11",
        "x=1,y=0,z=0:100",
        "x=1,y=0,z=1:101",
        "x=1,y=1,z=0:110",
        "x=1,y=1,z=1:111",
    ];

    [SkippableFact]
    public async Task DenseXYRoundTrip_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView3D/DenseXYRoundTrip.cs",
            ["Kernels.DenseXYKernel"],
            ExpectedRoundTripOutput);
    }

    [SkippableFact]
    public async Task DenseZYRoundTrip_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView3D/DenseZYRoundTrip.cs",
            ["Kernels.DenseZYKernel"],
            ExpectedRoundTripOutput);
    }

    [SkippableFact]
    public async Task Allocate3DDenseXYRoundTrip_ProducesCorrectOutput()
    {
        // 3D analogue of Allocate2DDenseXRoundTrip: native MemoryBuffer3D
        // path with GetAsArray3D readback. Same expected output as
        // DenseXYRoundTrip — pins the allocator and readback surface.
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView3D/Allocate3DDenseXYRoundTrip.cs",
            ["Kernels.Allocate3DDenseXYKernel"],
            ExpectedRoundTripOutput);
    }

    [SkippableFact]
    public async Task GeneralRoundTrip_ProducesCorrectOutput()
    {
        // Stride3D.General stresses the deeper nested StrideExtent auto-property
        // (Index3D, three int leaves) plus the multi-field GetField path in the
        // kernel body. Strides chosen to produce a DenseXY-equivalent layout
        // so expected output matches DenseXYRoundTrip.
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView3D/GeneralRoundTrip.cs",
            ["Kernels.GeneralKernel"],
            ExpectedRoundTripOutput);
    }
}
