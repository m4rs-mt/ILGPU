// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: MandelbrotClient.cs
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
using BlazorSampleApp.ILGPUWebHost;

namespace BlazorSampleApp.MandelbrotExplorer
{
    /// <summary>
    /// This object's job is to provide access/state/parameters to a compute session, this object is loaded via 
    /// Blazor "scoped" dependency injected in a razor page. 
    /// 
    /// IDisposable is implemented to notify the a "hosted" compute session is no longer needed a user's razor page.
    /// </summary>
    ///
#nullable disable
    public class MandelbrotClient : IMandelbrotClient, IDisposable
    {

        private ComputeSession _session;

        public bool IsActive { get { return _session?.IsActive ?? false; }  }

        // In Blazor server app or any web  a 
        private string SessionId = Guid.NewGuid().ToString();   

        private bool _disposing = false;
      
        public int ViewWidth { get; private set; }

        public int ViewHeight {  get; private set; }

        public float Left { get; private set;  } = -2.0f;
        public float Right {  get; private set; } = 1.0f;
        public float Top {  get; private set; } = 1.0f;

        public float Bottom {  get; private set; } = -1.0f;

        public int MaxIterations { get; set; } = 1000;

        private int[] _buffer = null;

        /// <summary>
        /// set display size and compile the GPU kernel
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public void SetDisplay(int width, int height)
        {
            ViewWidth = width;
            ViewHeight = height;

            if (_disposing)
            {
                return;
            }

            if ((_buffer?.Length ?? 0) != ViewWidth * ViewHeight)
            {
                _buffer = new int[ViewWidth * ViewHeight];
            }

            if (!_session.KernelIsCompiled && _session.IsActive)
            {
                _session.InitGPU(ref _buffer, new int[2] { ViewWidth, ViewHeight }, new float[4] { Left, Right, Bottom, Top }, MaxIterations);
                _session.CompileKernel();
            }
        }



        /// <summary>
        /// Generate a Mandelbrot set
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="bottom"></param>
        /// <param name="top"></param>
        /// <returns></returns>
        public async Task<int[]> CalculateMandelbrot(float left, float right, float bottom, float top)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;

         

            if (_session.IsActive && !_session.IsComputing && !_disposing)
            {
                if (await _session.CalcGPU(_buffer, new float[] { Left, Right, Bottom, Top }))
                {
                    return _buffer;
                }

            }

            return null;
        }

        public MandelbrotClient(IComputeHost computeHost)
        {
            // method for dependency injection otherwise this blows up on a web server.
            
            if (!computeHost.HostConfigured)
            {
                computeHost.ConfigureAcceleration(new AcceleratorType[2] { AcceleratorType.OpenCL, AcceleratorType.Cuda }, int.MaxValue, 20);
            }

            _session = computeHost.NewComputeStream(SessionId);

        }

        public string AcceleratorName() 
        {
            return _session?.Stream?.Accelerator?.Name ?? "n/a";
        }


        public void Dispose()
        {
            _disposing = true;
          

            if (!_session?.IsDisposing ?? false)
            {
                _session.Dispose();
            }

            _session = null;

        }


    }
}
