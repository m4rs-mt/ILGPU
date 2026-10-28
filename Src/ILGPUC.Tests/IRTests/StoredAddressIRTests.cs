// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StoredAddressIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Checks that a local whose address is stored to memory keeps its allocation.
/// </summary>
public sealed class StoredAddressIRTests(ITestOutputHelper output) :
    CompilationTestBase(output)
{
    private unsafe struct Holder
    {
        public int* Pointer;
        public int Offset;
    }

    /// <summary>
    /// Stores the address of a local into a structure and reads the local through
    /// the stored address.
    /// </summary>
    internal static unsafe void StoredAddressKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        var holder = new Holder { Pointer = &value, Offset = 1 };
        data[index] = *holder.Pointer + holder.Offset;
    }

    /// <summary>
    /// The address escapes into the structure it is stored to, so the local keeps
    /// its allocation until the address is resolved, and the store of the result
    /// remains after the global optimizations.
    /// </summary>
    [Fact]
    public void LocalWhoseAddressIsStored_KeepsItsAllocation()
    {
        var ir = DumpIRString(
            GetKernel(typeof(StoredAddressIRTests), nameof(StoredAddressKernel)),
            IRDumpPoint.AfterGlobalOpt);
        Assert.Matches(@"store i32 %\d+, ptr %\d+", ir);
        Assert.DoesNotContain("ptr null", ir);
    }
}
