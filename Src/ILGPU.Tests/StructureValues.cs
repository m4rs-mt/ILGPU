using System;
using System.Diagnostics.CodeAnalysis;
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
            { new DeepStructure<EmptyStruct>() },
            { new TestStruct<SmallCustomSizeStruct, long>()
            {
                Val0 = new SmallCustomSizeStruct()
                {
                    Data = byte.MaxValue,
                },
                Val1 = ushort.MaxValue,
                Val2 = long.MinValue,
            } },
            { new ShortFixedBufferStruct(short.MaxValue) },
            { new LongFixedBufferStruct(long.MaxValue) },
            { new TestStruct<EmptyStruct, ShortFixedBufferStruct>()
            {
                Val0 = default,
                Val1 = ushort.MaxValue,
                Val2 = new ShortFixedBufferStruct(short.MaxValue),
            } },
            { new TestStruct<ShortFixedBufferStruct, LongFixedBufferStruct>()
            {
                Val0 = new ShortFixedBufferStruct(short.MinValue),
                Val1 = ushort.MaxValue,
                Val2 = new LongFixedBufferStruct(long.MinValue),
            } },
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

        [SuppressMessage(
            "Design",
            "CA1067:Override Object.Equals(object) when implementing IEquatable<T>",
            Justification = "Test object")]
        internal struct Parent : IEquatable<Parent>
        {
            public Nested First;
            public Nested Second;

            public bool Equals(Parent other) =>
                First.Equals(other.First) &&
                Second.Equals(other.Second);
        }

        [SuppressMessage(
            "Design",
            "CA1067:Override Object.Equals(object) when implementing IEquatable<T>",
            Justification = "Test object")]
        internal struct Nested : IEquatable<Nested>
        {
            public int Value1;
            public int Value2;

            public bool Equals(Nested other) =>
                Value1.Equals(other.Value1) &&
                Value2.Equals(other.Value2);
        }

        internal static void StructureGetNestedKernel(
            Index1 index,
            ArrayView<Nested> data,
            Parent value)
        {
            data[index] = value.Second;
        }

        [Fact]
        [KernelMethod(nameof(StructureGetNestedKernel))]
        public void StructureGetNested()
        {
            var nested = new Nested()
            {
                Value1 = int.MinValue,
                Value2 = int.MaxValue,
            };
            var value = new Parent()
            {
                First = nested,
                Second = nested,
            };

            using var buffer = Accelerator.Allocate<Nested>(1);
            Execute(buffer.Length, buffer.View, value);
            Verify(buffer, new Nested[] { nested });
        }

        internal static void StructureSetNestedKernel(
            Index1 index,
            ArrayView<Parent> data,
            Nested value)
        {
            var dataValue = new Parent();
            dataValue.First = value;
            dataValue.Second = value;
            data[index] = dataValue;
        }

        [Fact]
        [KernelMethod(nameof(StructureSetNestedKernel))]
        public void StructureSetNested()
        {
            var nested = new Nested()
            {
                Value1 = int.MinValue,
                Value2 = int.MaxValue,
            };
            var value = new Parent()
            {
                First = nested,
                Second = nested,
            };

            using var buffer = Accelerator.Allocate<Parent>(1);
            Execute(buffer.Length, buffer.View, nested);
            Verify(buffer, new Parent[] { value });
        }
    }
}
