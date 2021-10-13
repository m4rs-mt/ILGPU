// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: IMandelbrotClient.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;

namespace BlazorSampleApp.MandelbrotExplorer
{
    /// <summary>
    /// This interface is to allow Blazor's dependency injection to generate scoped access to a server hosted singleton compute session.
    /// </summary>
    public interface IMandelbrotClient
    {
        string AcceleratorName();

        public bool IsActive { get; }

        void SetDisplay(int width, int height);

        Task<int[]> CalculateMandelbrot(float left, float right, float top, float bottom);

    }
}
