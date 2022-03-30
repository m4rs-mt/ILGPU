// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: ComputeHostExtension.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------



using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;

namespace BlazorSampleApp.ILGPUWebHost
{

    public static class ComputeHostExtension
    {
        /// <summary>
        /// We rank accelerators by their maximium throughput.
        /// 
        /// Rank is approximated by clock rate * threads per processor * multiprocessors, for an integrated gpu 
        /// we assume the clock rate is 2GHz to avoid operating specific calls
        /// </summary>
        /// <param name="accelerator"></param>
        /// <returns></returns>
        public static long AcceleratorRank(this Accelerator accelerator)
        {
            long clockRate = Convert.ToInt64(int.MaxValue); // assume 2 GHz for a CPU.

            clockRate = (accelerator as ILGPU.Runtime.OpenCL.CLAccelerator)?.ClockRate ?? clockRate;

            clockRate = (accelerator as ILGPU.Runtime.Cuda.CudaAccelerator)?.ClockRate ?? clockRate;

            return accelerator.NumMultiprocessors * accelerator.MaxNumThreadsPerMultiprocessor * clockRate;
        }
    }
}
