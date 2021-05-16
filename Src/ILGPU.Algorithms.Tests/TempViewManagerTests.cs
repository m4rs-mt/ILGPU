using ILGPU.Tests;
using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Algorithms.Tests
{
    public abstract partial class TempViewManagerTests : TestBase
    {
        protected TempViewManagerTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        #region MemberData

        public static TheoryData<object> PrimitiveTypes =>
            new TheoryData<object>
        {
            { default(sbyte) },
            { default(byte) },
            { default(short) },
            { default(ushort) },
            { default(int) },
            { default(uint) },
            { default(long) },
            { default(ulong) },
            { default(float) },
            { default(double) }
        };

        #endregion

        [Theory]
        [MemberData(nameof(PrimitiveTypes))]
        [SuppressMessage(
            "Usage",
            "xUnit1026:Theory methods should use all of their parameters",
            Justification = "Required to infer generic type argument")]
        public void AlignedAllocation<T, TArraySize>(T _)
            where T : unmanaged
        {
            var length = Interop.ComputeRelativeSizeOf<int, T>(1) * 2;
            using var buffer = Accelerator.Allocate1D<int>(length);
            var viewManager = new TempViewManager(buffer.View, nameof(buffer));

            viewManager.Allocate<T>(1);
            viewManager.Allocate<T>(1);
        }

        [Theory]
        [MemberData(nameof(PrimitiveTypes))]
        [SuppressMessage(
            "Usage",
            "xUnit1026:Theory methods should use all of their parameters",
            Justification = "Required to infer generic type argument")]
        public void UnalignedAllocation<T, TArraySize>(T _)
            where T : unmanaged
        {
            // NB: The minimum allocation size of TempViewManager is a single integer,
            // so ignore types that are one integer, or smaller, in size. These types
            // will never have alignment issues.
            if (Interop.SizeOf<T>() <= Interop.SizeOf<int>())
                return;

            var length = Interop.ComputeRelativeSizeOf<int, T>(1) + 1;
            using var buffer = Accelerator.Allocate1D<int>(length);
            var viewManager = new TempViewManager(buffer.View, nameof(buffer));

            viewManager.Allocate<byte>(1);
            Assert.Throws<InvalidOperationException>(() =>
                viewManager.Allocate<T>(1));
        }
    }
}
