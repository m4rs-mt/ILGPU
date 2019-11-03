using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class SizeOfValues : TestBase
    {
        protected SizeOfValues(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }

        internal struct SizeOfStruct
        {
            public int X;
            public long Y;
            public short Z;
            public int W;
        }

        internal static void SizeOfKernel<T>(
            Index index,
            ArrayView<int> data)
            where T : struct
        {
            data[0] = Interop.SizeOf<T>();
        }

        [Theory]
        [InlineData(typeof(sbyte))]
        [InlineData(typeof(byte))]
        [InlineData(typeof(short))]
        [InlineData(typeof(ushort))]
        [InlineData(typeof(int))]
        [InlineData(typeof(uint))]
        [InlineData(typeof(long))]
        [InlineData(typeof(ulong))]
        [InlineData(typeof(float))]
        [InlineData(typeof(double))]
        [InlineData(typeof(SizeOfStruct))]
        public void SizeOf(Type type)
        {
            var method = typeof(SizeOfValues).GetMethod(
                nameof(SizeOfKernel),
                BindingFlags.NonPublic | BindingFlags.Static);
            var specializedMethod = method.MakeGenericMethod(type);
            using var buffer = Accelerator.Allocate<int>(1);
            Execute<Index>(specializedMethod, buffer.Length, buffer.View);

            var size = Marshal.SizeOf(type);
            var expected = new int[] { size };
            Verify(buffer, expected);
        }
    }
}
