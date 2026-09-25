// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaGraphCapture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class CudaGraphCapture : TestBase
    {
        protected CudaGraphCapture(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal static void IncKernel(Index1D index, ArrayView<float> data) =>
            data[index] += 1.0f;

        /// <summary>
        /// A stream capture records submitted work without executing it; instantiating
        /// the resulting graph and launching it replays the recorded work exactly once
        /// per launch.
        /// </summary>
        [SkippableFact]
        public void CaptureRecordsAndReplaysSingleLaunch()
        {
            Skip.If(Accelerator.AcceleratorType != AcceleratorType.Cuda);

            const int length = 1024;
            using var buffer = Accelerator.Allocate1D<float>(length);
            var kernel = Accelerator
                .LoadAutoGroupedKernel<Index1D, ArrayView<float>>(IncKernel);
            using var stream = (CudaStream)Accelerator.CreateStream();

            buffer.MemSetToZero(stream);
            stream.Synchronize();

            // Warm up (force JIT/finalization) before capturing.
            kernel(stream, length, buffer.View);
            stream.Synchronize();
            buffer.MemSetToZero(stream);
            stream.Synchronize();

            stream.BeginCapture();
            kernel(stream, length, buffer.View);
            using var graph = stream.EndCapture();

            // Capture records but must not execute: the buffer is still zero.
            Assert.All(buffer.GetAsArray1D(), v => Assert.Equal(0.0f, v));

            using var exec = graph.Instantiate();

            exec.Launch(stream);
            stream.Synchronize();
            Assert.All(buffer.GetAsArray1D(), v => Assert.Equal(1.0f, v));

            exec.Launch(stream);
            stream.Synchronize();
            Assert.All(buffer.GetAsArray1D(), v => Assert.Equal(2.0f, v));

            // Replaying N more times advances by exactly N (the graph is reusable).
            const int extra = 8;
            for (int i = 0; i < extra; i++)
                exec.Launch(stream);
            stream.Synchronize();
            Assert.All(buffer.GetAsArray1D(), v => Assert.Equal(2.0f + extra, v));
        }

        /// <summary>
        /// A capture spanning many kernel launches replays the whole sequence with a
        /// single graph launch.
        /// </summary>
        [SkippableFact]
        public void CaptureRecordsMultiLaunchGraph()
        {
            Skip.If(Accelerator.AcceleratorType != AcceleratorType.Cuda);

            const int length = 256;
            const int kernelsPerGraph = 16;
            using var buffer = Accelerator.Allocate1D<float>(length);
            var kernel = Accelerator
                .LoadAutoGroupedKernel<Index1D, ArrayView<float>>(IncKernel);
            using var stream = (CudaStream)Accelerator.CreateStream();

            buffer.MemSetToZero(stream);
            kernel(stream, length, buffer.View);   // warmup
            stream.Synchronize();
            buffer.MemSetToZero(stream);
            stream.Synchronize();

            stream.BeginCapture();
            for (int i = 0; i < kernelsPerGraph; i++)
                kernel(stream, length, buffer.View);
            using var graph = stream.EndCapture();
            using var exec = graph.Instantiate();

            const int replays = 5;
            for (int i = 0; i < replays; i++)
                exec.Launch(stream);
            stream.Synchronize();

            // Each replay applies all kernelsPerGraph increments.
            float expected = kernelsPerGraph * replays;
            Assert.All(buffer.GetAsArray1D(), v => Assert.Equal(expected, v));
        }
    }
}
