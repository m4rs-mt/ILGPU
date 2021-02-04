using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable xUnit1026 // Theory methods should use all of their parameters

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
            var dataValue = new Parent
            {
                First = value,
                Second = value
            };
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

        public static TheoryData<object> StructureEmptyTypeData => new TheoryData<object>
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
        };

        internal static void StructureEmptyKernel<T1, T2>(
            Index1 index,
            ArrayView<TestStruct<T1, T2>> output,
            TestStruct<T1, T2> input)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            output[index].Val0 = input.Val0;
            output[index].Val1 = input.Val1;
            output[index].Val2 = input.Val2;
        }

        [Theory]
        [MemberData(nameof(StructureEmptyTypeData))]
        [KernelMethod(nameof(StructureEmptyKernel))]
        public void StructureGetEmptyLeft<T>(T _)
            where T : unmanaged
        {
            var expected = new TestStruct<EmptyStruct, T>()
            {
                Val0 = default,
                Val1 = ushort.MaxValue,
                Val2 = default,
            };

            using var buffer = Accelerator.Allocate<TestStruct<EmptyStruct, T>>(1);
            Execute<Index1, EmptyStruct, T>(buffer.Length, buffer.View, expected);
            Verify(buffer, new[] { expected });
        }

        [Theory]
        [MemberData(nameof(StructureEmptyTypeData))]
        [KernelMethod(nameof(StructureEmptyKernel))]
        public void StructureGetEmptyRight<T>(T _)
            where T : unmanaged
        {
            var expected = new TestStruct<T, EmptyStruct>()
            {
                Val0 = default,
                Val1 = ushort.MaxValue,
                Val2 = default,
            };

            using var buffer = Accelerator.Allocate<TestStruct<T, EmptyStruct>>(1);
            Execute<Index1, T, EmptyStruct>(
                buffer.Length,
                buffer.View,
                expected);
            Verify(buffer, new[] { expected });
        }

        internal struct UnsignedFieldStruct
        {
            public byte x;
            public ushort y;
            public uint z;
        }

        internal static void StructureUnsignedFieldKernel(
            Index1 index,
            ArrayView<long> output,
            UnsignedFieldStruct input)
        {
            output[index] = input.x;
            output[index + 1] = input.y;
            output[index + 2] = input.z;
        }

        [Fact]
        [KernelMethod(nameof(StructureUnsignedFieldKernel))]
        public void StructureUnsignedField()
        {
            var maxUInt8 = byte.MaxValue;
            var maxUInt16 = ushort.MaxValue;
            var maxUInt32 = uint.MaxValue;
            var expected = new long[] { maxUInt8, maxUInt16, maxUInt32 };

            var input = new UnsignedFieldStruct
            {
                x = maxUInt8,
                y = maxUInt16,
                z = maxUInt32
            };

            using var output = Accelerator.Allocate<long>(3);
            Execute(1, output.View, input);
            Verify(output, expected);
        }

        internal static void StructureLoweringKernel<T>(
            Index1 index,
            ArrayView<T> output,
            T value0,
            T value1,
            int c)
            where T : unmanaged
        {
            output[index] = Utilities.Select(c > 0, value0, value1);
        }

        [Theory]
        [KernelMethod(nameof(StructureLoweringKernel))]
        [MemberData(nameof(StructureInteropData))]
        public void StructureLowering<T>(T value)
            where T : unmanaged
        {
            using var output = Accelerator.Allocate<T>(1);
            Execute<Index1, T>(1, output.View, value, value, 42);

            var expected = new T[] { value };
            Verify(output, expected);
        }

        internal struct Vector2
        {
            public int X { get; set; }
            public int Y { get; set; }

            public static readonly Vector2 Zero = new Vector2(0, 0);

            public Vector2(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StructureAggressiveInliningFunc(out Vector2 v)
        {
            v = Vector2.Zero;
            v.X += 42;
        }

        internal static void StructureAggressiveInliningKernel(
            Index1 index, ArrayView<int> output)
        {
            StructureAggressiveInliningFunc(out var v);
            output[index] = v.X;
        }

        [Fact]
        [KernelMethod(nameof(StructureAggressiveInliningKernel))]
        public void StructureAggressiveInlining()
        {
            using var output = Accelerator.Allocate<int>(1);
            Execute(1, output.View);

            var expected = new int[] { 42 };
            Verify(output, expected);
        }
    }
}

#pragma warning restore xUnit1026 // Theory methods should use all of their parameters
