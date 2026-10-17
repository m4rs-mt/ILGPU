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

using ILGPU.Runtime;
using System;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests the runtime errors of kernel delegate bindings used outside of
/// kernels compiled by ILGPUC. Uncompiled launches are covered by
/// <see cref="KernelNotCompiledTests"/>.
/// </summary>
public sealed class UncompiledLaunchTests
{
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
