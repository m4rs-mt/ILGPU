// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicCalls.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System.Linq;
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
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = GetValue();
        }

        [Fact]
        [KernelMethod(nameof(NestedCallKernel))]
        public void NestedCall()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer.View, expected);
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
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = GetStructureValue().Second.Value;
        }

        [Fact]
        [KernelMethod(nameof(NestedStructureCallKernel))]
        public void NestedStructureCall()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(23, Length).ToArray();
            Verify(buffer.View, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void GetValue(out int value) =>
            value = 42;

        internal static void NestedCallOutKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            GetValue(out int value);
            data[index] = value;
        }

        [Fact]
        [KernelMethod(nameof(NestedCallOutKernel))]
        public void NestedCallOut()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer.View, expected);
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
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            GetStructureValue(out Parent value);
            data[index] = value.Second.Value;
        }

        [Fact]
        [KernelMethod(nameof(NestedStructureCallOutKernel))]
        public void NestedStructureCallOut()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(23, Length).ToArray();
            Verify(buffer.View, expected);
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
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            Parent value)
        {
            data[index] = value.Second.Count;
        }

        [Fact]
        [KernelMethod(nameof(TestNestedCallInstanceKernel))]
        public void NestedCallInstance()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, new Parent()
            {
                Second = new Nested()
                {
                    Value = 41,
                }
            });

            var expected = Enumerable.Repeat(42, Length).ToArray();
            Verify(buffer.View, expected);
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
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Parent temp = default;
            int value = ComputeNested(ref temp);
            data[index] = (temp.First + temp.Second.Value) * value;
        }

        [Fact]
        [KernelMethod(nameof(NestedCallChainKernel))]
        public void NestedCallChain()
        {
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View);

            var expected = Enumerable.Repeat(2706, Length).ToArray();
            Verify(buffer.View, expected);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InternalReturn(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            if (0.0.Equals(index))
                return;
            data[index] = 1;
        }

        internal static void MultiReturnKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
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
            using var buffer = Accelerator.Allocate1D<int>(Length);
            Execute(buffer.Length, buffer.View, c, d);

            var expected = Enumerable.Repeat(res, Length).ToArray();
            Verify(buffer.View, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static VariableView<Int2> GetVariableViewNested(
            Index1D index,
            ArrayView1D<Int2, Stride1D.Dense> source) =>
            source.VariableView(index);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static VariableView<int> GetVariableSubViewNested(
            VariableView<Int2> source) =>
            source.SubView<int>(sizeof(int));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static VariableView<int> GetVariableSubViewRoot(
            Index1D index,
            ArrayView1D<Int2, Stride1D.Dense> source)
        {
            var variableView = GetVariableViewNested(index, source);
            return GetVariableSubViewNested(variableView);
        }

        internal static void GetSubVariableViewKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<Int2, Stride1D.Dense> source)
        {
            var view = GetVariableSubViewRoot(index, source);
            data[index] = view.Value;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(1025)]
        [InlineData(8197)]
        [KernelMethod(nameof(GetSubVariableViewKernel))]
        public void GetSubVariableView(int length)
        {
            using var buffer = Accelerator.Allocate1D<int>(length);
            var sourceData = Enumerable.Range(0, length).Select(t =>
                new Int2(t, t + 1 )).ToArray();
            var expected = Enumerable.Range(1, length).ToArray();
            using (var source = Accelerator.Allocate1D<Int2>(length))
            {
                source.CopyFromCPU(Accelerator.DefaultStream, sourceData);
                Execute(length, buffer.View, source.View);
            }

            Verify(buffer.View, expected);
        }
    }
}

