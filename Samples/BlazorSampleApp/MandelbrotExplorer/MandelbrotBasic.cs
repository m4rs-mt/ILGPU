// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: MandelbrotBasic.cs
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
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Velocity;


namespace BlazorSampleApp.MandelbrotExplorer
{
    /// <summary>
    /// This loads the scoped dependency injected instance of ILGPU per razor page which is similar coding to a command line application.
    ///
    /// Blazor has difference levels of dependency injection, this should be scoped
    /// "services.AddScoped<ILGPU.BlazorSamples.MandelbrotExplorer.IMandelbrotBasic, ILGPU.BlazorSamples.MandelbrotExplorer.MandelbrotBasic>();"
    /// in the Blazor server startup file.
    /// </summary>

#nullable disable
    public class MandelbrotBasic : IMandelbrotBasic, IDisposable
    {
        private Context _context = null;
        private Accelerator _accelerator = null;
        private System.Action<Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>, int, ArrayView<int>> mandelbrot_kernel;


        // If we are disposing do not start new processes
        private bool _disposing = false;

        // Are we actively computing? If so we wait until the active compute process is finished to avoid blowing
        // up the GPU and all shared ILGPU sessions on the GPU by disposing a buffer while a kernel is active.
        private bool _computing = true;


        public bool IsDisposing { get {  return _disposing; }  }

        public Context ContextInstance {
            get
            {
                return _context;
            }
        }


        public Accelerator AcceleratorInstance
        {
            get
            {
                return _accelerator;
            }
        }

        public MandelbrotBasic()
        {
            _context = Context.Create(builder => builder.Default().Velocity());
            _accelerator = null;
        }


        /// <summary>
        /// Compile the Mandelbrot kernel based on the device selected.
        /// </summary>
        /// <param name="accelerator"></param>
        public void CompileKernel(Device device)
        {
            _accelerator?.Dispose();
            _accelerator = device.CreateAccelerator(_context);

            mandelbrot_kernel = _accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<float, Stride1D.Dense>, int, ArrayView<int>>(MandelbrotExtensions.MandelbrotKernel);
        }

        /// <summary>
        /// We're going to allow up to 10 seconds for any compute to complete. This should be an appsettings parameter, or passed as a compute timeout.
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
        /// Dispose accelerator and main ILGPU context, not to be called except when this component is disposed by dependency injection.
        /// </summary>
        public async void Dispose()
        {

            _disposing = true;

            bool computeFinished = await ComputeFinished();

            CleanupGPURepeat();
            mandelbrot_kernel = null;
            _accelerator?.Dispose();
            _context?.Dispose();

        }

        /// <summary>
        /// Calculate the Mandelbrot set a single time on the GPU.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="maxIterations"></param>
        public void CalcGPU(ref int[] buffer, int[] dispayPort, float[] viewArea, int max_iterations)
        {
            _computing = true;
            int num_values = buffer.Length;
            using var dev_out = _accelerator.Allocate1D<int>(num_values);
            using var displayParams = _accelerator.Allocate1D<int>(dispayPort);
            using var viewAreaParams = _accelerator.Allocate1D<float>(viewArea);


            if (!_disposing)
            {
                // Launch kernel
                mandelbrot_kernel(num_values, displayParams, viewAreaParams, max_iterations, dev_out.View);
            }

            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // the kernel computation is complete.
            if (!_disposing)
            {
                dev_out.CopyToCPU(buffer);
            }
            _computing = false;

            return;
        }

        int _buffersize;
        MemoryBuffer1D<int, Stride1D.Dense> _display = null;
        MemoryBuffer1D<float, Stride1D.Dense> _area = null;
        MemoryBuffer1D<int, Stride1D.Dense> _output = null;

        /// <summary>
        /// Initialize resources for repetitive computing
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="displayPort"></param>
        /// <param name="viewArea"></param>
        /// <param name="max_iterations"></param>
        public void InitGPURepeat(ref int[] buffer, int[] displayPort, float[] viewArea, int max_iterations)
        {
            if (!_disposing)
            {
                _computing = true;
                _buffersize = buffer.Length;
                _area = _accelerator.Allocate1D<float>(viewArea);
                _output = _accelerator.Allocate1D<int>(_buffersize);
                _display = _accelerator.Allocate1D<int>(displayPort);
                _computing = false;
            }

        }


        /// <summary>
        /// Calculate a new Mandelbrot set.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="dispayPort"></param>
        /// <param name="viewArea"></param>
        /// <param name="max_iterations"></param>
        public void CalcGPURepeat(ref int[] buffer, int[] dispayPort, float[] viewArea, int max_iterations)
        {
            _computing = true;

            if (!_disposing)
            {
                ArrayViewExtensions.CopyFromCPU(_area, viewArea);
            }


            if (!_disposing)
            {
                // Launch kernel
                mandelbrot_kernel(_buffersize, _display, _area, max_iterations, _output.View);
            }


            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            if (!_disposing)
            {
                _output.CopyToCPU(buffer);
            }

            _computing = false;

            return;
        }

        /// <summary>
        /// Clean up compute resources.
        /// </summary>
        public void CleanupGPURepeat()
        {
            _display?.Dispose();
            _display = null;
            _area?.Dispose();
            _area = null;
            _output?.Dispose();
            _output = null;

        }



    }


}
