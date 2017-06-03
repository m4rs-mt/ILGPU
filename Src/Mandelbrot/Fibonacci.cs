using ILGPU;
using ILGPU.Lightning;
using System.Reflection;
using System;
using ILGPU.Backends;

namespace Mandelbrot
{
    class Fibonacci
    {
        public struct Data
        {
            public int a1;
            public int a2;
            public int a3;
            public int a4;
            public int a5;
            public int a6;
            public int a7;
            public int a8;
            public int a9;
            public int a10;
            public int a11;
            public int a12;
            public int a13;
            public int a14;
            public int a15;
            public int a16;
        }

        struct Sequencer : ISequencer<Data>
        {
            public Data ComputeSequenceElement(int sequenceIndex)
            {
                int i = 1;
                return new Data()
                {
                    a6 = i,
                };
            }
        }

        const int GroupSize = 32;

        static void FibonacciSharedKernel(
            GroupedIndex<Index> index,
            int x,
            ArrayView<Data> input,
            ArrayView<int> output,
            [SharedMemory(GroupSize)]
            ArrayView<int> tempData)
        {
            var gridIdx = index.GridIdx;
            var groupIdx = index.GroupIdx;

            tempData[groupIdx] = input[groupIdx].a16;
            index.Barrier();

            int result = 0;
            for (int i = 0; i < x; ++i)
                result += tempData[i];

            //index.Barrier();

            output[gridIdx] = result;
        }

        static void FibonacciKernel(
            Index index,
            int x,
            ArrayView<Data> input,
            ArrayView<int> output)
        {
            int result = 0;
            for (int i = 0; i < x; ++i)
                result += input[i].a16;
            output[index] = result;
        }

        private static Context context;
        private static LightningContext lc_cpu;
        private static LightningKernel fibonacci_cpu_kernel;
        private static LightningContext lc_cuda;
        private static LightningKernel fibonacci_cuda_kernel;
        private static LightningKernel fibonacci_shared_cuda_kernel;
        public static void CompileKernels()
        {
            context = new Context();
            
            lc_cuda = LightningContext.CreateCudaContext(context);
            lc_cpu = LightningContext.CreateCPUContext(context, GroupSize);

            fibonacci_cuda_kernel = lc_cuda.LoadCachedKernel(typeof(Fibonacci).GetMethod(
                nameof(FibonacciKernel),
                BindingFlags.NonPublic | BindingFlags.Static)
                );
            fibonacci_shared_cuda_kernel = lc_cuda.LoadCachedKernel(typeof(Fibonacci).GetMethod(
                nameof(FibonacciSharedKernel),
                BindingFlags.NonPublic | BindingFlags.Static)
                );
            System.IO.File.WriteAllBytes("Kernel.ptx", fibonacci_shared_cuda_kernel.CompiledKernel.GetBuffer());
            fibonacci_cpu_kernel = lc_cpu.LoadCachedKernel(typeof(Fibonacci).GetMethod(
                nameof(FibonacciKernel),
                BindingFlags.NonPublic | BindingFlags.Static)
                );
        }

        private static void Fibonacci_PrepareModuleLowering(object sender, LLVMBackendEventArgs e)
        {
            IntPtr bla;
            LLVMSharp.LLVM.PrintModuleToFile(e.ModuleRef, "CodeGen.ll", out bla);
        }

        private static void Fibonacci_KernelModuleLowered(object sender, LLVMBackendEventArgs e)
        {
            IntPtr bla;
            LLVMSharp.LLVM.PrintModuleToFile(e.ModuleRef, "LoweringOutput.ll", out bla);
        }

        public static void Dispose()
        {
            lc_cuda.Dispose();
            lc_cpu.Dispose();
            context.Dispose();
        }

        public static void CalcCPU(int[] buffer, int width, int height, int max_iterations)
        {
            int num_values = buffer.Length;
            var dev_in = lc_cpu.Allocate<Data>(GroupSize);
            lc_cpu.Sequence(dev_in.View, new Sequencer());

            var dev_out = lc_cpu.Allocate<int>(num_values);

            // Launch kernel
            //fibonacci_cpu_kernel.Launch(
            //    new GroupedIndex<Index>(num_values, GroupSize),
            //    GroupSize, dev_in.View, dev_out.View);
            lc_cpu.Synchronize();
            dev_out.CopyTo(buffer, 0, 0, num_values);

            dev_in.Dispose();
            dev_out.Dispose();
            return;
        }


        public static void CalcSharedCUDA(int[] buffer, int width, int height, int max_iterations)
        {
            int num_values = buffer.Length;
            var dev_in = lc_cuda.Allocate<Data>(GroupSize);
            lc_cuda.Sequence(dev_in.View, new Sequencer());
            var dev_out = lc_cuda.Allocate<int>(num_values);

            // Launch kernel
            fibonacci_shared_cuda_kernel.Launch(
                new GroupedIndex<Index>(num_values, GroupSize),
                GroupSize, dev_in.View, dev_out.View);
            lc_cuda.Synchronize();
            dev_out.CopyTo(buffer, 0, 0, num_values);

            dev_in.Dispose();
            dev_out.Dispose();
            return;
        }

        public static void CalcCUDA(int[] buffer, int width, int height, int max_iterations)
        {
            int num_values = buffer.Length;
            var dev_in = lc_cuda.Allocate<Data>(GroupSize);
            lc_cuda.Sequence(dev_in.View, new Sequencer());
            var dev_out = lc_cuda.Allocate<int>(num_values);

            // Launch kernel
            fibonacci_cuda_kernel.Launch(
                num_values,
                GroupSize, dev_in.View, dev_out.View);
            lc_cuda.Synchronize();
            dev_out.CopyTo(buffer, 0, 0, num_values);

            dev_in.Dispose();
            dev_out.Dispose();
            return;
        }

        public static void CalcSingleThreaded(int[] buffer, int width, int height, int max_iterations)
        {
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    // int index = i + j * width;  // ILGPU-like index

                    if (max_iterations < 2)
                        return;
                    buffer[0] = 1;
                    buffer[1] = 1;
                    for (int k = 2; k < max_iterations; k++)
                    {
                        buffer[k] = buffer[k - 1] + buffer[k - 2];
                    }
                }
            }
        }


    }
}
