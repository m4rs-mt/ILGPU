// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UncompiledLaunchTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests the runtime errors of kernel launches that ILGPUC did not compile,
/// such as launches called through reflection. The test assembly itself is
/// not rewritten by ILGPUC, so its launches remain uncompiled.
/// </summary>
public sealed class UncompiledLaunchTests
{
    /// <summary>
    /// Asserts that the given launch throws a readable
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    private static void AssertUncompiled(Action<AcceleratorStream> launch)
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);

        var exception = Assert.Throws<NotSupportedException>(
            () => launch(accelerator.DefaultStream));
        Assert.Contains("not compiled by ILGPUC", exception.Message);
        Assert.Contains("stream?.Launch", exception.Message);
        Assert.DoesNotContain("{0}", exception.Message);
    }

    /// <summary>
    /// An uncompiled implicitly grouped launch names the cause.
    /// </summary>
    [Fact]
    public void UncompiledAutoGroupedLaunch_ThrowsReadableMessage() =>
        AssertUncompiled(stream => stream.Launch((Index1D)4, _ => { }));

    /// <summary>
    /// An uncompiled explicitly grouped launch names the cause.
    /// </summary>
    [Fact]
    public void UncompiledGroupedLaunch_ThrowsReadableMessage() =>
        AssertUncompiled(stream => stream.Launch(new KernelConfig(1, 4), _ => { }));

    /// <summary>
    /// An uncompiled 3D launch names the cause.
    /// </summary>
    [Fact]
    public void Uncompiled3DLaunch_ThrowsReadableMessage() =>
        AssertUncompiled(stream => stream.Launch(new Index3D(1, 1, 1), _ => { }));

    /// <summary>
    /// Binding a kernel delegate outside of a compiled kernel names the cause.
    /// </summary>
    [Fact]
    public void KernelDelegateBindAtRuntime_ThrowsReadableMessage()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => KernelDelegates.Bind<Func<int, int>>("Test:Host.Run.op#0"));
        Assert.Contains("kernels compiled by ILGPUC", exception.Message);
    }
}
