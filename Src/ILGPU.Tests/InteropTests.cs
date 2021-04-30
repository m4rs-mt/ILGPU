using ILGPU.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class InteropTests : TestBase
    {
        protected InteropTests(
            ITestOutputHelper output,
            TestContext testContext)
            : base(output, testContext)
        { }

        internal static void PrintFKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            byte b,
            short s,
            int i,
            long l,
            float f,
            double d)
        {
            bool t = b > 0;
            Interop.Write(
                "Current Thread:\t{0}, {1}+{2} (3) - {3} [{2}]",
                (int)index,
                b,
                s,
                i);
            Interop.WriteLine(
                " => %%:\t{0}, {1}\t{2}/{3}",
                l,
                f,
                d,
                t);
        }

        [Fact]
        [KernelMethod(nameof(PrintFKernel))]
        public void PrintF()
        {
            const int Length = 4;
            using var buffer = Accelerator.Allocate1D<int>(Length);

            Execute(
                Length,
                buffer.View,
                byte.MaxValue,
                short.MinValue,
                int.MaxValue,
                long.MinValue,
                float.MaxValue,
                double.MinValue);

            // We cannot verify the output of the kernel since the redirection of the
            // Stdout/Stderr stream(s) does not seem to work depending on the platform
            // on the driver. However, we can verify whether the program fails.
        }
    }
}
