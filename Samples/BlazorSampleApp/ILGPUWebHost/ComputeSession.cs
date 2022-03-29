// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: ComputeSession.cs
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
using BlazorSampleApp.MandelbrotExplorer;



namespace BlazorSampleApp.ILGPUWebHost
{
    /// <summary>
    /// An individual compute session represents all compute resources for a single accelerator stream.    
    /// 
    /// This object must implement IDisposable as it allocates dedicated non-movable GPU accessible buffers.
    /// </summary>
#nullable disable
    public class ComputeSession : IDisposable
    {

        // one or more kernels can be hosted in each compute session depending on the complexity of our calculation
        private System.Action<AcceleratorStream, Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>, int, ArrayView<int>> mandelbrot_kernel;


        // SessionID allows individual Blazor sessions to "find" their compute session 
        
        public string SessionID { get; set; }

        private bool _active = true;
        public bool IsActive { get { return _active; } set { _active = value; } }


        // do we have an active process? If so we wait until the process is finished to avoid blowing up GPU
        private bool _computing = false;
        public bool IsComputing { get { return _computing; } }

        private bool _disposing = false;
        public bool IsDisposing { get { return _disposing; } }

        private bool _kernelIsCompiled = false;
        public bool KernelIsCompiled {  get { return _kernelIsCompiled; } }



        private AcceleratorStream _stream = null;


        private ComputeHost _host;

        public AcceleratorStream Stream 
        {
            get
            {
                if (IsActive)
                {
                    return _stream;
                }
                else
                {
                    return null;
                }
            }
        }

        public ComputeSession(string sessionID, Accelerator accelerator, ComputeHost host)
        {
            SessionID = sessionID;

            _stream = accelerator.CreateStream();
            _host = host;
    
        }

        public void CompileKernel()
        {

            if (IsActive && !_kernelIsCompiled)
            {                
                _computing = true;
                mandelbrot_kernel = _stream.Accelerator.LoadAutoGroupedKernel<Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>, int, ArrayView<int>>(MandelbrotExtensions.MandelbrotKernel);
                _kernelIsCompiled = true;
                _computing = false;
            }


        }


        /// <summary>
        /// We're going to allow up to 10 seconds for any compute to complete
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ComputeFinished()
        {
            int iCount = 0;
            while ((iCount < 1000) && _computing)
            {
                await Task.Delay(10);
                iCount += 1;
            }
            return (iCount < 1000);
        }

        /// <summary>
        /// Clean up all compute resources
        /// </summary>
        public async void Dispose()
        {
            _disposing = true;
            _active = false; // do not allow any new computation.

            // the host will dispose of this stream
            _host?.ReturnSession(this);
            _host = null;

            bool computeFinished = await ComputeFinished();

            _stream?.Dispose();
            _stream = null;
            _display?.Dispose();
            _display = null;

            _area?.Dispose();
            _area = null;
            _output?.Dispose();
            _output = null;
            mandelbrot_kernel = null;
        }


        int _buffersize;
        int _iterations;
        MemoryBuffer1D<int, Stride1D.Dense> _display = null;
        MemoryBuffer1D<float, Stride1D.Dense> _area = null;
        MemoryBuffer1D<int, Stride1D.Dense> _output = null;

        public void InitGPU(ref int[] buffer, int[] displayPort, float[] viewArea, int max_iterations)
        {
            if (IsActive && !_disposing)
            {
                // release current buffers if they exist
                _display?.Dispose();
                _area?.Dispose();
                _output?.Dispose();

                _buffersize = buffer.Length;

                _iterations = max_iterations;

                _area = _stream.Accelerator.Allocate1D<float>(viewArea);
                
                _display = _stream.Accelerator.Allocate1D<int>(displayPort);

                _output = _stream.Accelerator.Allocate1D<int>(_buffersize);
              
            }
        }


        /// <summary>
        /// calculate a new result set based on the viewArea of interest
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="viewArea"></param>
        /// <returns></returns>
        public async Task<bool> CalcGPU(int[] buffer, float[] viewArea)
        {
            bool result = false;
            if (buffer.Length != _buffersize)
            {
                return false;
            }
            
            // copy parameters to GPU inbound buffer
            ArrayViewExtensions.CopyFromCPU(_area, viewArea);

            if (IsActive && !_computing && !_disposing)
            {
                // Launch kernel
                _computing = true;
              
                mandelbrot_kernel(_stream, _buffersize, _display, _area, _iterations, _output.View);
                await _stream.SynchronizeAsync(); // wait for the stream to synchronize
                result = IsActive;
                _computing = false;
            }


            // Reads data from the GPU buffer into a CPU array.
            // that the kernel and memory copy are completed first.
            if (IsActive && !_disposing)
            {
                _computing = true;
                _output.CopyToCPU(buffer);
                result = IsActive;
                _computing = false;
            }

            return result && !_disposing;
        }

    }
}
