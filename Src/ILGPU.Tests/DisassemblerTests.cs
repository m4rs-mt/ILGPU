using ILGPU.Frontend;
using ILGPU.Frontend.DebugInformation;
using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class DisassemblerTests : TestBase
    {
        protected DisassemblerTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void StelemLdtokenKernel<T>(
            Index1 index,
            ArrayView<T> input,
            ArrayView<T> output)
            where T : unmanaged
        {
            output[index] = (dynamic)input[index] * input[index];
        }

        /// <summary>
        /// Tests that the <see cref="Disassembler"/> is able to correctly handle
        /// <see cref="ILOpCode.Ldtoken"/> and <see cref="ILOpCode.Stelem_Ref"/>.
        /// </summary>
        [Theory]
        [InlineData(typeof(byte))]
        [InlineData(typeof(sbyte))]
        [InlineData(typeof(ushort))]
        [InlineData(typeof(short))]
        [InlineData(typeof(uint))]
        [InlineData(typeof(int))]
        [InlineData(typeof(ulong))]
        [InlineData(typeof(long))]
        [InlineData(typeof(float))]
        [InlineData(typeof(double))]
        public void StelemLdtoken(Type genericType)
        {
            var methodInfo = typeof(DisassemblerTests).GetMethod(
                nameof(StelemLdtokenKernel),
                BindingFlags.NonPublic | BindingFlags.Static);
            var method = methodInfo.MakeGenericMethod(genericType);

            var disassembler = new Disassembler(method, SequencePointEnumerator.Empty);
            disassembler.Disassemble();
        }
    }
}
