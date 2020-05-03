using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class StructureValues : TestBase
    {
        protected StructureValues(
            ITestOutputHelper output,
            TestContext testContext)
            : base(output, testContext)
        { }

        public static TheoryData<object> StructureInteropData => new TheoryData<object>
        {
            { default(EmptyStruct) },
            { default(TestStruct) },
            { new TestStruct<byte>()
            {
                Val0 = 1,
                Val1 = byte.MaxValue,
                Val2 = short.MinValue
            } },
            { new TestStruct<EmptyStruct, sbyte>()
            {
                Val0 = default,
                Val1 = ushort.MaxValue,
                Val2 = sbyte.MinValue,
            } },
            { new TestStruct<float, TestStruct<EmptyStruct, sbyte>>()
            {
                Val0 = float.Epsilon,
                Val1 = ushort.MaxValue,
                Val2 = new TestStruct<EmptyStruct, sbyte>()
                {
                    Val0 = default,
                    Val1 = ushort.MaxValue,
                    Val2 = sbyte.MinValue,
                }
            } },
            { new TestStruct<int, TestStruct<float, TestStruct<EmptyStruct, sbyte>>>()
            {
                Val0 = int.MinValue,
                Val1 = ushort.MaxValue,
                Val2 = new TestStruct<float, TestStruct<EmptyStruct, sbyte>>()
                {
                    Val0 = float.Epsilon,
                    Val1 = ushort.MaxValue,
                    Val2 = new TestStruct<EmptyStruct, sbyte>()
                    {
                        Val0 = default,
                        Val1 = ushort.MaxValue,
                        Val2 = sbyte.MinValue,
                    }
                }
            } },
            { new DeepStructure<EmptyStruct>() }
        };

        internal static void StructureInteropKernel<T>(
            Index1 index,
            ArrayView<T> data,
            T value)
            where T : unmanaged
        {
            data[index] = value;
        }

        [Theory]
        [MemberData(nameof(StructureInteropData))]
        [KernelMethod(nameof(StructureInteropKernel))]
        public void StructureInterop<T>(T value)
            where T : unmanaged
        {
            using var buffer = Accelerator.Allocate<T>(1);
            Execute<Index1, T>(buffer.Length, buffer.View, value);

            var expected = new T[] { value };
            Verify(buffer, expected);
        }

        public static TheoryData<object> StructureViewInteropData =>
            new TheoryData<object>
        {
            { default(EmptyStruct) },
            { default(TestStruct) },
            { new TestStruct<byte>()
            {
                Val0 = 1,
                Val1 = byte.MaxValue,
                Val2 = short.MinValue
            } },
            { new TestStruct<int, TestStruct<float, TestStruct<EmptyStruct, sbyte>>>()
            {
                Val0 = int.MinValue,
                Val1 = ushort.MaxValue,
                Val2 = new TestStruct<float, TestStruct<EmptyStruct, sbyte>>()
                {
                    Val0 = float.Epsilon,
                    Val1 = 0,
                    Val2 = new TestStruct<EmptyStruct, sbyte>()
                    {
                        Val0 = default,
                        Val1 = ushort.MinValue,
                        Val2 = sbyte.MinValue,
                    }
                }
            } }
        };

        internal static void StructureViewInteropKernel<T>(
            Index1 index,
            DeepStructure<ArrayView<T>> value,
            T val0,
            T val1,
            T val2)
            where T : unmanaged
        {
            value.Val0[index] = val0;
            value.Val1[index] = val1;
            value.Val2[index] = val2;
        }

        [Theory]
        [MemberData(nameof(StructureInteropData))]
        [KernelMethod(nameof(StructureViewInteropKernel))]
        public void StructureViewInterop<T>(T value)
            where T : unmanaged
        {
            using var nestedBuffer = Accelerator.Allocate<T>(1);
            using var nestedBuffer2 = Accelerator.Allocate<T>(1);
            using var nestedBuffer3 = Accelerator.Allocate<T>(1);

            var nestedStructure = new DeepStructure<ArrayView<T>>(
                nestedBuffer,
                nestedBuffer2,
                nestedBuffer3);
            Execute<Index1, T>(1, nestedStructure, value, value, value);

            var expected = new T[] { value };
            Verify(nestedBuffer, expected);
            Verify(nestedBuffer2, expected);
            Verify(nestedBuffer3, expected);
        }
    }
}
