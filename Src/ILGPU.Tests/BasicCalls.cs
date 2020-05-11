using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class BasicCalls : TestBase
    {
        private const int Length = 32;

        protected BasicCalls(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        [KernelMethod(nameof(NestedCallKernel))]
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

        [KernelMethod(nameof(NestedCallKernel))]
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
    }
}

