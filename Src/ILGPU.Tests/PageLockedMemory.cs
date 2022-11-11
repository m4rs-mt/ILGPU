// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: PageLockedMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class PageLockedMemory : TestBase
    {
        protected PageLockedMemory(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        private const int Length = 1024;

        public static TheoryData<object> Numbers => new TheoryData<object>
        {
            { 10 },
            { -10 },
            { int.MaxValue },
            { int.MinValue },
        };

        internal static void PinnedMemoryKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            if (data[index] == 0)
            {
                data[index] = 42;
            }
            else
            {
                data[index] = 24;
            }
        }

        [Fact]
        [KernelMethod(nameof(PinnedMemoryKernel))]
        public unsafe void PinnedUsingGCHandle()
        {
            var expected = Enumerable.Repeat(42, Length).ToArray();
            var array = new int[Length];
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            try
            {
                using var buffer = Accelerator.Allocate1D<int>(array.Length);
                using var scope = Accelerator.CreatePageLockFromPinned(array);

                buffer.View.CopyFromPageLockedAsync(scope);
                Execute(buffer.Length, buffer.View);

                buffer.View.CopyToPageLockedAsync(scope);
                Accelerator.Synchronize();
                Verify1D(array, expected);
            }
            finally
            {
                handle.Free();
            }
        }

#if NET5_0_OR_GREATER
        [Fact]
        [KernelMethod(nameof(PinnedMemoryKernel))]
        public void PinnedUsingGCAllocateArray()
        {
            var expected = Enumerable.Repeat(42, Length).ToArray();
            var array = System.GC.AllocateArray<int>(Length, pinned: true);
            using var buffer = Accelerator.Allocate1D<int>(array.Length);
            using var scope = Accelerator.CreatePageLockFromPinned(array);

            buffer.View.CopyFromPageLockedAsync(scope);
            Execute(buffer.Length, buffer.View);

            buffer.View.CopyToPageLockedAsync(scope);
            Accelerator.Synchronize();
            Verify1D(array, expected);
        }
#endif

        internal static void CopyKernel(
            Index1D index,
            ArrayView1D<long, Stride1D.Dense> data)
        {
            data[index] -= 5;
        }

        [Theory]
        [MemberData(nameof(Numbers))]
        [KernelMethod(nameof(CopyKernel))]
        public void Copy(long constant)
        {
            using var array = Accelerator.AllocatePageLocked1D<long>(Length);
            for (int i = 0; i < Length; i++)
                array[i] = constant;
            using var buff = Accelerator.Allocate1D<long>(Length);

            // Start copying, create the expected array in the meantime
            buff.View.CopyFromPageLockedAsync(array);
            var expected = Enumerable.Repeat(constant - 5, Length).ToArray();
            Accelerator.Synchronize();

            Execute(array.Extent.ToIntIndex(), buff.View);
            Accelerator.Synchronize();

            buff.View.CopyToPageLockedAsync(array);
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, array.Length);
            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], array[i]);
        }

        // No need for kernel, assuming copy tests pass.
        // Just going to confirm integrity in this test.
        [Fact]
        public void GetAsArrayPageLocked()
        {
            using var array = Accelerator.AllocatePageLocked1D<long>(Length);
            for (int i = 0; i < Length; i++)
                array[i] = 10;

            using var buff = Accelerator.Allocate1D<long>(Length);
            buff.View.CopyFromPageLockedAsync(array);

            var expected = new int[Length];
            for (int i = 0; i < Length; i++)
                expected[i] = 10;
            Accelerator.Synchronize();

            var data = buff.View.GetAsPageLocked1D();
            Accelerator.Synchronize();

            Assert.Equal(expected.Length, data.Length);

            for (int i = 0; i < Length; i++)
                Assert.Equal(expected[i], data[i]);
        }
    }
}
