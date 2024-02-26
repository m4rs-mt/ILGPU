// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: MandelbrotPageCPU.razor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using BlazorSampleApp.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using ILGPU;
using ILGPU.Runtime;



namespace BlazorSampleApp.MandelbrotExplorer
{
#nullable disable
    public partial class MandelbrotPageCPU : IDisposable
    {
        public BasicCanvas Canvas2D { get; set; }

        public bool DisabledButtons { get; set; } = true;


        [Inject] IJSRuntime JSRuntime { get; set; }

        [Inject] IMandelbrotBasic MandelbrotInstance { get; set; }


        [Inject] NavigationManager NavManager { get; set; }

        public string ExecutionsDetails1 { get; set; }


        public string ExecutionsDetails2 { get; set; }

        public string ExecutionsDetails3 { get; set; }

        public string ExecutionsDetails4 { get; set; }

        public bool _disposing = false;

        public bool _computing = false;

        private Device _lastDevice = null;

        private Device _CPUDevice = null;

        /// <summary>
        /// Ready Blazor page once component loading is complete
        /// </summary>
        /// <param name="firstRender"></param>
        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                // we can't call any webgl functions until the page is fully rendered and the canvas is complete.
                Canvas2D.CanvasInitComplete += CanvasInitComplete;
                _stopWatch = new Stopwatch();


            }

            base.OnAfterRender(firstRender);
        }

        /// <summary>
        /// Initialize Mandelbrot view and render a Mandelbrot set
        /// </summary>
        /// <param name="basicCanvas"></param>
        private async void CanvasInitComplete(BasicCanvas basicCanvas)
        {
            // we could start the rendering process here rather than having a button click to start rendering.
            DisabledButtons = false;
            GetAvailableDevices();

            displayPort[0] = Canvas2D.Width;
            displayPort[1] = Canvas2D.Height;
            areaView[0] = -2.0f;
            areaView[1] = 1.0f;
            areaView[2] = -1.0f;
            areaView[3] = 1.0f;
            maxIterations = 1000;

            int[] data = Crunch(DeviceName);
            await MandelbrotExtensions.Draw(Canvas2D, data, displayPort[0],
                displayPort[1], maxIterations, Color.Blue);

            StateHasChanged();
        }


        // Initialize navigation tracking razor page
        protected override void OnInitialized()
        {
            _stopWatch = new Stopwatch();

            NavManager.LocationChanged += LocationChanged;

            base.OnInitialized();
        }


        /// <summary>
        /// We're going to allow up to 30 seconds for any compute to complete
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ComputeFinished()
        {
            int iCount = 0;
            while ((iCount < 3000) && _computing)
            {
                await Task.Delay(10);
                iCount += 1;
            }

            return (iCount < 1000);
        }

        // Remove navigation detection if this page is ending.
        public async void Dispose()
        {
            _disposing = true;
            NavManager.LocationChanged -= LocationChanged;
            bool computeFinished = await ComputeFinished();
        }


        // This only gets called when the user has navigated elsewhere.
        void LocationChanged(object sender, LocationChangedEventArgs e)
        {
            // assume we're leaving this page for good, preempt new computation
            _disposing = true;
        }

        const string SingleDouble = "Single Thread - double";
        const string SingleFloat = "Single Thread - float";
        const string SingleHalf = "Single Thread - Half";
        const string SingleBFloat16 = "Single Thread - BFloat16";
        const string SingleMini43Float8 = "Single Thread - Mini43Float8";
        const string SingleMini52Float8 = "Single Thread - Mini52Float8";

        const string ParallelDouble = "Parallel.For - double";
        const string ParallelFloat = "Parallel.For - float";
        const string ParallelHalf = "Parallel.For - Half";
        const string ParallelBFloat16 = "Parallel.For - BFloat16";
        const string ParallelMini43Float8 = "Parallel.For - Mini43Float8";
        const string ParallelMini52Float8 = "Parallel.For - Mini52Float8";

        /// <summary>
        /// Create a CPU accelerator device list
        /// </summary>
        private void GetAvailableDevices()
        {
            SystemDevices.Add(SingleDouble);
            SystemDevices.Add(SingleFloat);
            SystemDevices.Add(SingleHalf);
            SystemDevices.Add(SingleBFloat16);
            SystemDevices.Add(SingleMini43Float8);
            SystemDevices.Add(SingleMini52Float8);

            SystemDevices.Add(ParallelDouble);
            SystemDevices.Add(ParallelFloat);
            SystemDevices.Add(ParallelHalf);
            SystemDevices.Add(ParallelBFloat16);
            SystemDevices.Add(ParallelMini43Float8);
            SystemDevices.Add(ParallelMini52Float8);

            foreach (Device device in MandelbrotInstance.ContextInstance.Devices)
            {
                if (device.AcceleratorType == AcceleratorType.CPU)
                {
                    _CPUDevice = device;
                    SystemDevices.Add(_CPUDevice.Name);
                }

            }
        }


        List<string> SystemDevices = new List<string>();



        // Measure performance
        private static Stopwatch _stopWatch;

        private void RestartWatch()
        {
            _stopWatch.Reset();
            _stopWatch.Start();
        }

        private string ElapsedTime(string title = "Elapsed Time")
        {
            _stopWatch.Stop();
            return title + " " +
                   $"{_stopWatch.Elapsed.Minutes:00}:{_stopWatch.Elapsed.Seconds:00}.{_stopWatch.Elapsed.Milliseconds:000} ";
        }


        public string DeviceName { get; set; } = SingleDouble;

        protected async void UpdateSelected(ChangeEventArgs e)
        {

            DeviceName = e.Value.ToString();

            areaView[0] = -2.0f;
            areaView[1] = 1.0f;
            areaView[2] = -1.0f;
            areaView[3] = 1.0f;
            maxIterations = 1000;
            _computing = true;
            int[] data = Crunch(DeviceName);
            await MandelbrotExtensions.Draw(Canvas2D, data, displayPort[0],
                displayPort[1], maxIterations, Color.Blue);
            _computing = false;
            StateHasChanged();
        }



        float[] areaView = new float[4];
        int[] displayPort = new int[2];
        int maxIterations = 1000;




        /// <summary>
        ///
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        protected int[] Crunch(string device)
        {
            int[] data = new int[displayPort[0] * displayPort[1]];

            switch (DeviceName)
            {
                case SingleDouble:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUSingleThreadDouble(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Single Thread - Double");
                    break;
                case SingleFloat:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUSingleThreadFloat(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Single Thread - Double");
                    break;
                case SingleHalf:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUSingleThreadHalf(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Single Thread - Half");
                    break;
                case SingleBFloat16:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUSingleThreadBFloat16(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Single Thread - BFloat16");
                    break;
                case SingleMini43Float8:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUSingleThreadMini43Float8(data,
                        displayPort, areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Single Thread - Mini43Float8");
                    break;
                case SingleMini52Float8:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUSingleThreadMini52Float8(data,
                        displayPort, areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Single Thread - Mini52Float8");
                    break;
                case ParallelDouble:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUParallelForDouble(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Parallel.For - Double");
                    break;
                case ParallelFloat:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUParallelForFloat(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Parallel.For - Float");
                    break;
                case ParallelHalf:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUParallelForHalf(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Parallel.For - Half");
                    break;
                case ParallelBFloat16:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUParallelForBFloat16(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Parallel.For - BFloat16");
                    break;
                case ParallelMini43Float8:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUParallelForMini43Float8(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Parallel.For - Mini43Float8");
                    break;
                case ParallelMini52Float8:
                    RestartWatch();
                    MandelbrotExtensions.CalcCPUParallelForMini52Float8(data, displayPort,
                        areaView, maxIterations); // Single thread CPU
                    ExecutionsDetails1 = ElapsedTime("Parallel.For - Mini52Float8");
                    break;
                default:
                    _computing = true;
                    if (_lastDevice != _CPUDevice)
                    {
                        RestartWatch();
                        MandelbrotInstance.CompileKernel(_CPUDevice);
                        ExecutionsDetails3 =
                            ElapsedTime("IL Compile - " + _CPUDevice.Name);
                        _lastDevice = _CPUDevice;
                    }


                    RestartWatch();

                    MandelbrotInstance.CalcGPU(ref data, displayPort, areaView,
                        maxIterations); // ILGPU-CPU-Mode
                    _computing = false;
                    ExecutionsDetails4 = ElapsedTime("IL Run - " + _CPUDevice.Name);

                    break;
            }

            return data;
        }





        /// <summary>
        /// Animate 500 frames while narrowing view to small subsection of the Mandelbrot set.
        /// </summary>

        public async void AnimateMandelbrot()
        {
            int[] data = new int[displayPort[0] * displayPort[1]];


            float offsetX = -0.02f;
            float offsetY = 0.00562f;



            for (int i = 0; i < 500; i++)
            {
                if (_disposing)
                    break;
                RestartWatch();

                switch (DeviceName)
                {
                    case SingleDouble:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUSingleThreadDouble(data, displayPort,
                            areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Single Thread - Double");
                        break;
                    case SingleFloat:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUSingleThreadFloat(data, displayPort,
                            areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Single Thread - Double");
                        break;
                    case SingleHalf:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUSingleThreadHalf(data, displayPort,
                            areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Single Thread - Half");
                        break;
                    case SingleBFloat16:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUSingleThreadBFloat16(data,
                            displayPort, areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Single Thread - BFloat16");
                        break;
                    case SingleMini43Float8:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUSingleThreadMini43Float8(data,
                            displayPort, areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Single Thread - Mini43Float8");
                        break;
                    case SingleMini52Float8:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUSingleThreadMini52Float8(data,
                            displayPort, areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Single Thread - Mini52Float8");
                        break;
                    case ParallelDouble:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUParallelForDouble(data, displayPort,
                            areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Parallel.For - Double");
                        break;
                    case ParallelFloat:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUParallelForFloat(data, displayPort,
                            areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Parallel.For - Float");
                        break;
                    case ParallelHalf:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUParallelForHalf(data, displayPort,
                            areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Parallel.For - Half");
                        break;
                    case ParallelBFloat16:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUParallelForBFloat16(data, displayPort,
                            areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Parallel.For - BFloat16");
                        break;
                    case ParallelMini43Float8:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUParallelForMini43Float8(data,
                            displayPort, areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Parallel.For - Mini43Float8");
                        break;
                    case ParallelMini52Float8:
                        RestartWatch();
                        MandelbrotExtensions.CalcCPUParallelForMini52Float8(data,
                            displayPort, areaView, maxIterations); // Single thread CPU
                        ExecutionsDetails3 = ElapsedTime("Parallel.For - Mini52Float8");
                        break;
                    default:
                        _computing = true;
                        if (_lastDevice != _CPUDevice)
                        {
                            RestartWatch();
                            MandelbrotInstance.CompileKernel(_CPUDevice);
                            ExecutionsDetails3 =
                                ElapsedTime("IL Compile - " + _CPUDevice.Name);
                            _lastDevice = _CPUDevice;
                        }


                        RestartWatch();

                        MandelbrotInstance.CalcGPU(ref data, displayPort, areaView,
                            maxIterations); // ILGPU-CPU-Mode
                        _computing = false;
                        ExecutionsDetails4 = ElapsedTime("IL Run - " + _CPUDevice.Name);

                        break;
                }



                areaView[0] = areaView[0] * 0.98f + offsetX;
                areaView[1] = areaView[1] * 0.98f + offsetX;
                areaView[2] = areaView[2] * 0.98f + offsetY;
                areaView[3] = areaView[3] * 0.98f + offsetY;

                if (_disposing)
                    break;
                RestartWatch();

                await MandelbrotExtensions.Draw(Canvas2D, data, displayPort[0],
                    displayPort[1], maxIterations, Color.Blue);

                ExecutionsDetails4 = ElapsedTime("Render Time");

                StateHasChanged();



            }



            MandelbrotInstance.CleanupGPURepeat();
            _computing = false;

        }


    }

}

