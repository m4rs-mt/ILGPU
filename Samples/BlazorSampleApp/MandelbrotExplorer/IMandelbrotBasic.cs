// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: IMandelbrotBasic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using ILGPU;
using ILGPU.Runtime;

namespace BlazorSampleApp.MandelbrotExplorer
{

    /// <summary>
    /// For Blazor scoped dependency injection of the full ILGPU client into a razor page.
    /// </summary>
    public interface IMandelbrotBasic
    {
        bool IsDisposing { get; }

        Context ContextInstance { get; }

        Accelerator AcceleratorInstance { get; }

        void CompileKernel(Device device);

        void CalcGPU(ref int[] buffer, int[] dispayPort, float[] viewArea, int maxIterations);

        void InitGPURepeat(ref int[] buffer, int[] displayPort, float[] viewArea, int max_iterations);
        void CalcGPURepeat(ref int[] buffer, int[] dispayPort, float[] viewArea, int max_iterations);

        void CleanupGPURepeat();
    }
}
