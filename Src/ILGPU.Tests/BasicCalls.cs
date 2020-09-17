﻿using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract partial class BasicCalls : TestBase
    {
        private const int Length = 32;

        protected BasicCalls(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int GetValue() => 42;

        internal static void NestedCallKernel(
            Index1 index,
            ArrayView<int> data)
        {
            data[index] = GetValue();
        }

        [Fact]
        [KernelMethod(nameof(NestedCallKernel))]
        public void NestedCall()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Parent GetStructureValue() =>
            new Parent()
            {
                Second = new Nested()
                {
                    Value = 23
                }
            };

        internal static void NestedStructureCallKernel(
            Index1 index,
            ArrayView<int> data)
        {
            data[index] = GetStructureValue().Second.Value;
        }

        [Fact]
        [KernelMethod(nameof(NestedStructureCallKernel))]
        public void NestedStructureCall()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(23, Length).ToArray();
            Verify(buffer, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void GetValue(out int value) =>
            value = 42;

        internal static void NestedCallOutKernel(
            Index1 index,
            ArrayView<int> data)
        {
            GetValue(out int value);
            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(NestedCallOutKernel))]
        public void NestedCallOut()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void GetStructureValue(out Parent value) =>
            value = new Parent()
            {
                Second = new Nested()
                {
                    Value = 23
                }
            };

        internal static void NestedStructureCallOutKernel(
            Index1 index,
            ArrayView<int> data)
        {
            GetStructureValue(out Parent value);
            data[index] = value.Second.Value;
        }

        [Fact]
        [KernelMethod(nameof(NestedStructureCallOutKernel))]
        public void NestedStructureCallOut()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(23, Length).ToArray();
            Verify(buffer, expected);
        }

        internal struct Parent
        {
            public int First;
            public Nested Second;
        }

        internal struct Nested
        {
            public int Value;

            public int Count
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                get => Value + 1;
            }
        }

        internal static void TestNestedCallInstanceKernel(
            Index1 index,
            ArrayView<int> data,
            Parent value)
        {
            data[index] = value.Second.Count;
        }

        [Fact]
        [KernelMethod(nameof(TestNestedCallInstanceKernel))]
        public void NestedCallInstance()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, new Parent()
            {
                Second = new Nested()
                {
                    Value = 41,
                }
            });

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Parent ComputeNested2()
        {
            var t = GetStructureValue();
            t.First = 42;
            ++t.Second.Value;
            return t;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ComputeNested2(out Parent value)
        {
            value = ComputeNested2();
            return --value.First;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ComputeNested(ref Parent value)
        {
            int result = ComputeNested2(out value);
            value.First = result + 1;
            return result;
        }

        internal static void NestedCallChainKernel(
            Index1 index,
            ArrayView<int> data)
        {
            Parent temp = default;
            int value = ComputeNested(ref temp);
            data[index] = (temp.First + temp.Second.Value) * value;
        }

        [Fact]
        [KernelMethod(nameof(NestedCallChainKernel))]
        public void NestedCallChain()
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(2706, Length).ToArray();
            Verify(buffer, expected);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InternalReturn(Index1 index, ArrayView<int> data)
        {
            if (0.0.Equals(index))
                return;
            data[index] = 1;
        }

        internal static void MultiReturnKernel(
            Index1 index,
            ArrayView<int> data,
            int c,
            int d)
        {
            data[index] = 0;

            if (c < d)
                return;

            InternalReturn(index, data);

            if (c + 1 < d)
                return;

            for (int i = 0; i < c; ++i)
            {
                if (i + 10 < d)
                    return;
            }

            data[index] = int.MaxValue;
        }

        [Theory]
        [InlineData(0, 1, 0)]
        [InlineData(1, 1, int.MaxValue)]
        [InlineData(10, 9, int.MaxValue)]
        [KernelMethod(nameof(MultiReturnKernel))]
        public void MultiReturn(int c, int d, int res)
        {
            using var buffer = Accelerator.Allocate<int>(Length);
            Execute(buffer.Length, buffer.View, c, d);

            var expected = Enumerable.Repeat(res, Length).ToArray();
            Verify(buffer, expected);
        }
    }
}

