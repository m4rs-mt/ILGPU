// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PreferredDeviceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Verifies the order in which <see cref="Context.GetPreferredDevices"/> ranks
/// devices: discrete devices before integrated ones regardless of their memory
/// size, then by descending memory size.
/// </summary>
public sealed class PreferredDeviceTests
{
    private sealed class TestDevice : Device
    {
        public TestDevice(string name, bool isIntegrated, long memorySize)
            : base(AcceleratorType.OpenCL)
        {
            Name = name;
            IsIntegrated = isIntegrated;
            MemorySize = memorySize;
        }

        public override Accelerator CreateAccelerator(Context context) =>
            throw new NotSupportedException();
    }

    private static string[] Order(params Device[] devices)
    {
        var method = typeof(Context).GetMethod(
            "OrderByPreference",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var ordered = (IEnumerable<Device>)method.Invoke(null, [devices])!;
        return ordered.Select(d => d.Name).ToArray();
    }

    [Fact]
    public void DiscreteDevice_OutranksIntegratedDeviceWithMoreMemory()
    {
        // The #1611 laptop: an iGPU reporting shared system RAM and an 8 GB dGPU
        var integrated = new TestDevice("iGPU", isIntegrated: true, 13L << 30);
        var discrete = new TestDevice("dGPU", isIntegrated: false, 8L << 30);
        Assert.Equal(new[] { "dGPU", "iGPU" }, Order(integrated, discrete));
    }

    [Fact]
    public void DevicesOfTheSameKind_AreRankedByMemorySize()
    {
        Assert.Equal(
            new[] { "dGPU 24 GB", "dGPU 8 GB", "iGPU 16 GB", "iGPU 4 GB" },
            Order(
                new TestDevice("iGPU 4 GB", isIntegrated: true, 4L << 30),
                new TestDevice("dGPU 8 GB", isIntegrated: false, 8L << 30),
                new TestDevice("iGPU 16 GB", isIntegrated: true, 16L << 30),
                new TestDevice("dGPU 24 GB", isIntegrated: false, 24L << 30)));
    }

    [Fact]
    public void EqualDevices_KeepTheirEnumerationOrder()
    {
        Assert.Equal(
            new[] { "first", "second" },
            Order(
                new TestDevice("first", isIntegrated: false, 8L << 30),
                new TestDevice("second", isIntegrated: false, 8L << 30)));
    }
}
