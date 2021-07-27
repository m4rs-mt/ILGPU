using ILGPU.Runtime;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;
using static ILGPU.Tests.EnumValues;

namespace ILGPU.Tests
{
    public abstract class SizeOfValues : TestBase
    {
        protected SizeOfValues(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        public static TheoryData<object> SizeOfTestData => new TheoryData<object>
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
            { default(double) },
            { default(BasicEnum1) },
            { default(BasicEnum2) },
            { default(BasicEnum3) },
            { default(BasicEnum4) },
            { default(EmptyStruct) },
            { default(TestStruct) },
            { default(TestStruct<TestStruct<byte>>) },
            {
                default(
                    TestStruct<BasicEnum4, TestStruct<short, EmptyStruct>>)
            },
            {
                default(
                    TestStruct<int, ulong>)
            },
            {
                default(
                    TestStruct<byte, TestStruct<int, ulong>>)
            },
            {
                default(
                    TestStruct<double, TestStruct<byte, TestStruct<int, ulong>>>)
            },
            { default(TestStruct<float, TestStruct<EmptyStruct, sbyte>>) },
            { default(DeepStructure<TestStruct<int>>) },
            {
                default(
                    TestStruct<int, TestStruct<float, TestStruct<EmptyStruct, sbyte>>>)
            },
            { default(ShortFixedBufferStruct) },
            { default(LongFixedBufferStruct) },
            { default(TestStruct<EmptyStruct, ShortFixedBufferStruct>) },
            { default(TestStruct<ShortFixedBufferStruct, LongFixedBufferStruct>) },
            { default(TestStruct<float, TestStruct<ShortFixedBufferStruct, sbyte>>) },
            { default(TestStruct<float, TestStruct<LongFixedBufferStruct, byte>>) },
        };

        internal static void SizeOfKernel<T>(
            Index1D _,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            data[0] = Interop.SizeOf<T>();
        }

        [Theory]
        [MemberData(nameof(SizeOfTestData))]
        [KernelMethod(nameof(SizeOfKernel))]
        public void SizeOf<T>(T value)
            where T : unmanaged
        {
            using var buffer = Accelerator.Allocate1D<int>(1);
            Execute<Index1D, T>(buffer.IntExtent, buffer.View);

            var size = Interop.SizeOf(value);
            var expected = new int[] { size };
            Verify(buffer.View, expected);
        }

        public static TheoryData<object> OffsetOfData => new TheoryData<object>
        {
            { default(TestStruct<TestStruct<byte>>) },
            {
                default(
                    TestStruct<BasicEnum4, TestStruct<short, EmptyStruct>>)
            },
            {
                default(
                    TestStruct<int, ulong>)
            },
            {
                default(
                    TestStruct<byte, TestStruct<int, ulong>>)
            },
            {
                default(
                    TestStruct<double, TestStruct<byte, TestStruct<int, ulong>>>)
            },
            { default(TestStruct<float, TestStruct<EmptyStruct, sbyte>>) },
            {
                default(
                    TestStruct<int, TestStruct<float, TestStruct<EmptyStruct, sbyte>>>)
            }
        };

        internal static void OffsetOfKernel<T>(
            Index1D _,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            data[0] = Interop.OffsetOf<T>("Val2");
        }

        [Theory]
        [MemberData(nameof(OffsetOfData))]
        [KernelMethod(nameof(OffsetOfKernel))]
        [SuppressMessage(
            "Usage",
            "xUnit1026:Theory methods should use all of their parameters",
            Justification = "Required to infer generic type argument")]
        public void OffsetOf<T>(T value)
            where T : unmanaged
        {
            using var buffer = Accelerator.Allocate1D<int>(1);
            Execute<Index1D, T>(buffer.IntExtent, buffer.View);

            var size = Interop.OffsetOf<T>("Val2");
            var expected = new int[] { size };
            Verify(buffer.View, expected);
        }
    }
}
