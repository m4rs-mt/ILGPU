// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerLoopIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class PointerLoopIRTests(ITestOutputHelper output)
    : CompilationTestBase(output)
{
    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LocalLess_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(PointerLoopKernels), nameof(PointerLoopKernels.LocalLessKernel)), opt, mode);

    // --- Address spaces ---

    // A pointer moved up to an end pointer it is compared with points to local
    // memory: the comparison no longer keeps the loop pointer generic, which
    // Metal and OpenCL emitted as a pointer to global memory
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void LocalLess_LoopPointerPointsToLocalMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpLoopKernel(nameof(PointerLoopKernels.LocalLessKernel), backend, opt);
        Assert.Matches(@"phi ptr<i32> addrspace\(local\) \[", ir);
        Assert.Matches(@"icmp ult ptr<i32> addrspace\(local\) %\d+, %\d+", ir);
        Assert.DoesNotMatch(@"phi ptr<i32> \[", ir);
    }

    // Two pointers to shared memory compared with each other both point to
    // shared memory
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void SharedLess_LoopPointersPointToSharedMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpLoopKernel(nameof(PointerLoopKernels.SharedLessKernel), backend, opt);
        Assert.Matches(@"icmp ult ptr<i32> addrspace\(shared\) %\d+, %\d+", ir);
        Assert.DoesNotMatch(@"phi ptr<i32> \[", ir);
    }

    // The pointers of a fixed statement on a local array moved in loops point to
    // local memory, also after the array has been lowered to local memory, and
    // the pointer selected from the first element and the null pointer of an
    // empty array no longer fails to lower at O1 and O2
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void FixedArray_LoopPointersPointToLocalMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpLoopKernel(nameof(PointerLoopKernels.FixedArrayKernel), backend, opt);
        Assert.Matches(@"icmp uge ptr<i32> addrspace\(local\) %\d+, ", ir);
        Assert.Matches(@"icmp ule ptr<i32> addrspace\(local\) %\d+, ", ir);
        Assert.DoesNotMatch(@"phi ptr<i32> \[", ir);
    }

    // Apple's GPU compiler drops the stores of a loop that moves a pointer over a
    // local array read at a dynamic index afterwards at O1 and O2 unless they
    // are volatile, also with a non-zero initializer and three elements; stores
    // to view elements stay plain stores
    [Theory]
    [InlineData(nameof(PointerLoopKernels.FixedStoreKernel), OptimizationLevel.O0)]
    [InlineData(nameof(PointerLoopKernels.FixedStoreKernel), OptimizationLevel.O1)]
    [InlineData(nameof(PointerLoopKernels.FixedStoreKernel), OptimizationLevel.O2)]
    [InlineData(nameof(PointerLoopKernels.FixedStoreInitKernel), OptimizationLevel.O1)]
    [InlineData(nameof(PointerLoopKernels.FixedStoreLength3Kernel), OptimizationLevel.O2)]
    public void FixedStore_MetalLoopStoresAreVolatile(
        string kernelName,
        OptimizationLevel opt)
    {
        using var helper = new CompilationHelper(BackendType.Metal);
        var source = helper.GenerateBackendCode(
            GetKernel(typeof(PointerLoopKernels), kernelName),
            BackendType.Metal,
            new CompilationProperties(OptimizationLevel: opt)).SourceCode;
        Assert.Matches(@"\*\(volatile thread int\*\)\w+ = ", source);
        Assert.DoesNotMatch(@"\*\(volatile device ", source);
    }

    // Pointer fields moved up to an end pointer and a pointer local moved
    // through a pointer to it point to local memory: storing pointers derived
    // from their own values (c.P++, (*pp)++) no longer keeps the structure or
    // the pointer local generic, which made Metal and OpenCL reject comparing
    // them with the end pointer as comparing pointers to global and local
    // memory
    [Theory]
    [InlineData(nameof(PointerLoopKernels.FieldLessKernel), BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(nameof(PointerLoopKernels.FieldLessKernel), BackendType.OpenCL, OptimizationLevel.O1)]
    [InlineData(nameof(PointerLoopKernels.TrailingFieldLessKernel), BackendType.Metal, OptimizationLevel.O2)]
    [InlineData(nameof(PointerLoopKernels.TrailingFieldLessKernel), BackendType.OpenCL, OptimizationLevel.O0)]
    [InlineData(nameof(PointerLoopKernels.PointerToPointerLessKernel), BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(nameof(PointerLoopKernels.PointerToPointerLessKernel), BackendType.OpenCL, OptimizationLevel.O0)]
    public void StoredLoopPointer_PointsToLocalMemory(
        string kernelName,
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpLoopKernel(kernelName, backend, opt);
        Assert.Matches(@"icmp ult ptr<(i32|void)> addrspace\(local\) %\d+, %\d+", ir);
        Assert.DoesNotMatch(@"alloca ptr<i32>, ", ir);
        Assert.DoesNotMatch(@"type \{ (i32, )?ptr<i32>(, i32)? \}", ir);
    }

    // Pointers to two pointer locals compared with each other point to local
    // memory and so do the pointer locals
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O1)]
    public void PointerToPointerCompare_ComparesPointersToLocalMemory(
        BackendType backend,
        OptimizationLevel opt)
    {
        var ir = DumpLoopKernel(
            nameof(PointerLoopKernels.PointerToPointerCompareKernel),
            backend,
            opt);
        Assert.Matches(
            @"icmp eq ptr<ptr<i32> addrspace\(local\)> addrspace\(local\) %\d+, %\d+",
            ir);
        Assert.DoesNotMatch(@"alloca ptr<i32>, ", ir);
    }

    // With generic pointers, the address of a local is compared with the address
    // of a view element as a generic pointer
    [Theory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    public void MixedSpace_ComparesGenericPointers(OptimizationLevel opt)
    {
        var ir = DumpLoopKernel(
            nameof(PointerLoopKernels.MixedSpaceKernel),
            BackendType.Cuda,
            opt);
        Assert.Matches(@"icmp (eq|ne) ptr<void> %\d+, %\d+", ir);
    }

    private string DumpLoopKernel(
        string kernelName,
        BackendType backend,
        OptimizationLevel opt) =>
        DumpIRString(
            GetKernel(typeof(PointerLoopKernels), kernelName),
            IRDumpPoint.AfterBackendTransforms,
            new CompilationProperties(OptimizationLevel: opt),
            backend);
}
