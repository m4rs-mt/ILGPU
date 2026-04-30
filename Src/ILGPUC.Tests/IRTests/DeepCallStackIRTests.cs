// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeepCallStackIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Frontend;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Round-3 derisking: validates that a remappable BCL intrinsic
/// (<c>System.Math.Abs(int)</c> → <c>XMath.Abs(int)</c> →
/// <c>@llvm.abs.i32</c>) is correctly discovered and emitted even when
/// the call site is 8 frames below the kernel entry.
///
/// Why this test matters in round 3:
///
/// The lazy-walk optimization landed in commit <c>7fe20cf8e</c> drops the
/// pre-emptive operand walk inside
/// <see cref="ILGPUC.Frontend.ILFrontend"/>'s on-the-fly disassembly path
/// and instead lets <see cref="ILGPUC.Frontend.CodeGenerator"/> drive
/// operand discovery via <c>OnNewMethodCalled</c>. A deep chain stresses
/// that the discovery actually propagates through every level — if any
/// level's compilation aborted silently, transitive intrinsics below it
/// would never be reached and the IR would emit a (broken) BCL call to
/// <c>Math.Abs</c> instead of the expected
/// <c>@llvm.abs.i32</c> intrinsic.
/// </summary>
public sealed class DeepCallStackIRTests : CompilationTestBase
{
    public DeepCallStackIRTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Compiles the 8-deep <see cref="DeepCallStackKernels.DeepChainAbsKernel"/>
    /// at <see cref="IRDumpPoint.AfterFrontend"/> (no optimizer / no
    /// inlining) and asserts the resulting IR contains the GPU
    /// <c>llvm.abs.i32</c> intrinsic emission. AfterFrontend is the strict
    /// test: at this stage every helper layer is still a distinct IR
    /// method, so the remap must have fired specifically inside
    /// <c>Layer8</c>'s body — not via opt-time inlining.
    /// </summary>
    [Fact]
    public void DeepChainAbs_AfterFrontend_EmitsAbsIntrinsic()
    {
        var kernel = typeof(DeepCallStackKernels)
            .GetMethod(nameof(DeepCallStackKernels.DeepChainAbsKernel))!;

        var ir = DumpIRString(kernel, IRDumpPoint.AfterFrontend);

        Assert.Contains(
            "llvm.abs.i32",
            ir);
    }

    /// <summary>
    /// Same kernel, AfterGlobalOpt at O1 / Release. The optimizer may
    /// inline some layers but the intrinsic emission must still be
    /// present (the remap is a frontend-time decision, opt only
    /// rearranges blocks).
    /// </summary>
    [Fact]
    public void DeepChainAbs_AfterGlobalOpt_O1Release_EmitsAbsIntrinsic()
    {
        var kernel = typeof(DeepCallStackKernels)
            .GetMethod(nameof(DeepCallStackKernels.DeepChainAbsKernel))!;

        var ir = DumpIRString(kernel, IRDumpPoint.AfterGlobalOpt);

        Assert.Contains(
            "llvm.abs.i32",
            ir);
    }

    /// <summary>
    /// **Path-B-at-depth derisk.** Forces <c>ILGPUC.Tests</c> (the entry
    /// assembly) into <see cref="ILFrontendCache.ForcedNonWalkableAssemblyNames"/>,
    /// which makes round-3's lazy-walk gate skip every helper layer
    /// inside <see cref="DeepCallStackKernels"/>. With the chain
    /// non-walkable:
    ///
    /// <list type="number">
    /// <item><description>The eager BFS in
    /// <see cref="ILFrontend.LoadMethods"/> records every layer with a
    /// <c>null</c> body and skips disassembly.</description></item>
    /// <item><description>
    /// <see cref="ILFrontend.GenerateCode"/> falls back to the
    /// on-the-fly disassembly path for every layer, with operand
    /// discovery driven exclusively by
    /// <c>CodeGenerator.OnNewMethodCalled</c> (no preemptive operand
    /// walk — round-3 explicitly removed it).
    /// </description></item>
    /// <item><description>
    /// At the bottom of the chain, <see cref="DeepCallStackKernels.Layer8"/>
    /// codegen reaches <c>Math.Abs(int)</c>. Since
    /// <c>System.Private.CoreLib</c> is non-walkable, the BFS never
    /// visited it, so the <c>_methods[Math.Abs]</c> alias to
    /// <c>XMath.Abs</c>'s body never got set. <see cref="ILGPUC.Frontend.Intrinsic.Intrinsics.TryGenerateCode"/>
    /// (Path B) catches it via <c>TryGetIntrinsicRemapping</c>,
    /// redirects to <c>XMath.Abs(int)</c>, and the generator emits
    /// <c>@llvm.abs.i32</c>.
    /// </description></item>
    /// </list>
    ///
    /// This is the worst-case scenario the user explicitly called out
    /// when round 3 was being designed: a deep chain through
    /// non-walkable code with the intrinsic at the very bottom.
    /// </summary>
    [Fact]
    public void DeepChainAbs_PathB_AtDepth8_EmitsAbsIntrinsic()
    {
        var kernel = typeof(DeepCallStackKernels)
            .GetMethod(nameof(DeepCallStackKernels.DeepChainAbsKernel))!;

        // Force the entry assembly non-walkable. Every layer
        // (DeepCallStackKernels.Layer1..Layer8) lives in ILGPUC.Tests
        // and so will be skipped by the eager BFS, exercising the
        // codegen-time discovery + Path-B intrinsic remap.
        var cache = new ILFrontendCache();
        cache.ForcedNonWalkableAssemblyNames.Add("ILGPUC.Tests");

        var ir = DumpAfterFrontendWithCache(kernel, cache);

        // (1) Depth assertion: every layer must appear as a distinct IR
        // method. If `OnNewMethodCalled`-driven operand discovery
        // skipped any layer, or if the inliner collapsed the chain at
        // frontend time, this would catch it. AfterFrontend dumps come
        // before optimization so all 8 layers must be present as
        // separate `define` blocks.
        for (int i = 1; i <= 8; i++)
        {
            Assert.Contains($"define i32 @Layer{i}(", ir);
        }

        // (2) The intrinsic emission itself is the load-bearing
        // assertion: codegen for Layer8 emitted `@llvm.abs.i32`, which
        // can only happen via Path B redirecting `Math.Abs(int)` to
        // `XMath.Abs(int)`.
        Assert.Contains("llvm.abs.i32", ir);

        // (3) Negative assertion: the redirect must have happened.
        // If `Intrinsics.TryGenerateCode`'s remap check at line 344 of
        // `Intrinsics.cs` failed to fire, codegen would fall through to
        // `GetMethod(Math.Abs)` and emit a real call to BCL `Math.Abs`,
        // and the IR would contain a call site referencing it. Asserting
        // its absence proves Path B redirected the call before any
        // codegen for the BCL method ran.
        Assert.DoesNotContain("@System.Math.Abs", ir);
        Assert.DoesNotContain("call i32 @Abs(", ir);
    }
}
