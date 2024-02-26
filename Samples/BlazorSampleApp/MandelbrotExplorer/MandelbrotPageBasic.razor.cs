// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: MandelbrotPageBasic.razor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using ILGPU;
using ILGPU.Runtime;
using BlazorSampleApp.Components;


namespace BlazorSampleApp.MandelbrotExplorer
{
#nullable disable
    public partial class MandelbrotPageBasic : IDisposable
    {
        public BasicCanvas Canvas2D { get; set; }

        public bool DisabledButtons { get; set; } = true;


        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        IMandelbrotBasic MandelbrotInstance { get; set; }

        [Inject]
        NavigationManager NavManager { get; set; }


        public string ExecutionsDetails1 { get; set; }


        public string ExecutionsDetails2 { get; set; }

        public string ExecutionsDetails3 { get; set; }

        private bool _disposing = false;

        protected override void OnInitialized()
        {
            NavManager.LocationChanged += LocationChanged;
            base.OnInitialized();
        }

        void LocationChanged(object sender, LocationChangedEventArgs e)
        {
            // assume we're leaving this page
            _disposing = true;
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                // we can't call any webgl functions until the page is fully rendered and the canvas is complete.
                Canvas2D.CanvasInitComplete += CanvasInitComplete;
                _stopWatch = new Stopwatch();
                GetAvailableDevices();
            }
            base.OnAfterRender(firstRender);
        }

        private async void CanvasInitComplete(BasicCanvas supercanvas)
        {


            displayPort[0] = Canvas2D.Width;
            displayPort[1] = Canvas2D.Height;
            areaView[0] = -2.0f;
            areaView[1] = 1.0f;
            areaView[2] = -1.0f;
            areaView[3] = 1.0f;
            maxIterations = 1000;

            int[] data = Crunch();
            await MandelbrotExtensions.Draw(Canvas2D, data, displayPort[0], displayPort[1], maxIterations, Color.Blue);


            // we could start the rendering process here rather than having a button click to start rendering.
            DisabledButtons = false;

            StateHasChanged();
        }


        private Device _lastDevice = null;



        List<string> SystemDevices = new List<string>();

        /// <summary>
        /// Create a non-CPU accelerator device list
        /// </summary>
        private void GetAvailableDevices()
        {
            int icnt = 0;
            foreach (Device device in MandelbrotInstance.ContextInstance.Devices)
            {
                if (device.AcceleratorType != AcceleratorType.CPU)
                {

                    if (icnt == 0)
                    {
                        DeviceName = device.Name;
                    }
                    icnt += 1;
                    SystemDevices.Add(device.Name);
                }
            }


        }

        private static Stopwatch _stopWatch;

        private void RestartWatch()
        {
            _stopWatch.Reset();
            _stopWatch.Start();
        }

        private string ElapsedTime(string title = "Elapsed Time")
        {
            _stopWatch.Stop();
            return title + " " + $"{_stopWatch.Elapsed.Minutes:00}:{_stopWatch.Elapsed.Seconds:00}.{_stopWatch.Elapsed.Milliseconds:000} ";
        }




        public string DeviceName { get; set; } = "CPUOnly";

        protected async void UpdateSelected(ChangeEventArgs e)
        {
            DeviceName = e.Value.ToString();
            await RenderDevice(DeviceName);
        }

        public async Task RenderDevice(string deviceName)
        {

            areaView[0] = -2.0f;
            areaView[1] = 1.0f;
            areaView[2] = -1.0f;
            areaView[3] = 1.0f;
            maxIterations = 1000;

            int[] data = Crunch();
            await MandelbrotExtensions.Draw(Canvas2D, data, displayPort[0], displayPort[1], maxIterations, Color.Blue);
            StateHasChanged();
        }



        float[] areaView = new float[4];
        int[] displayPort = new int[2];
        int maxIterations = 1000;

        /// <summary>
        /// Generate a Mandelbrot set.
        /// </summary>
        /// <returns></returns>
        protected int[] Crunch()
        {
            int[] data = new int[displayPort[0] * displayPort[1]];

            if (DeviceName == "CPUOnly")
            {
                RestartWatch();
                //page resource
                MandelbrotExtensions.CalcCPUParallelForFloat(data, displayPort, areaView, maxIterations); // Single thread CPU
                ExecutionsDetails1 = ElapsedTime("CPU Only Mandelbrot");


            }
            else
            {
                Device device = MandelbrotInstance.ContextInstance.Devices.First(x => x.Name == DeviceName);

                if (_lastDevice != device)
                {
                    RestartWatch();
                    MandelbrotInstance.CompileKernel(device);
                    ExecutionsDetails2 = ElapsedTime("IL Compile - " + device.Name);
                    _lastDevice = device;
                }


                RestartWatch();

                MandelbrotInstance.CalcGPU(ref data, displayPort, areaView, maxIterations); // ILGPU-CPU-Mode
                ExecutionsDetails3 = ElapsedTime("IL Run - " + DeviceName);


            }


            return data;
        }


        /// <summary>
        /// Animate 500 frames while narrowing view to small subsection of the Mandelbrot set.
        /// </summary>
        public async void AnimateMandelbrot()
        {
            int[] data = new int[displayPort[0] * displayPort[1]];

            Device device = MandelbrotInstance.ContextInstance.Devices.First(x => x.Name == DeviceName);

            if (_lastDevice != device)
            {
                RestartWatch();
                MandelbrotInstance.CompileKernel(device);
                ExecutionsDetails2 = ElapsedTime("IL Compile - " + device.Name);
                _lastDevice = device;

            }


            MandelbrotInstance.InitGPURepeat(ref data, displayPort, areaView, maxIterations);
            StateHasChanged();
            float offsetX = -0.02f;
            float offsetY = 0.00562f;

            for (int i = 0; i < 500; i++)
            {
                // here we are in a long running loop, a user can navigate away or close a window which will
                // eventually result in an exception as this loop will continue to run


                if (_disposing || MandelbrotInstance.IsDisposing)
                {

                    break;
                }
                RestartWatch();
                MandelbrotInstance.CalcGPURepeat(ref data, displayPort, areaView, maxIterations); // ILGPU-CPU-Mode
                ExecutionsDetails2 = ElapsedTime($"IL Run - {DeviceName}");

                areaView[0] = areaView[0] * 0.98f + offsetX;
                areaView[1] = areaView[1] * 0.98f + offsetX;
                areaView[2] = areaView[2] * 0.98f + offsetY;
                areaView[3] = areaView[3] * 0.98f + offsetY;


                if (_disposing || MandelbrotInstance.IsDisposing)
                {
                    break;
                }
                RestartWatch();
                await MandelbrotExtensions.Draw(Canvas2D, data, displayPort[0], displayPort[1], maxIterations, Color.Blue);
                ExecutionsDetails3 = ElapsedTime("Web Server Render");
                StateHasChanged();



            }

            MandelbrotInstance.CleanupGPURepeat();


        }



        public void Dispose()
        {
            _disposing = true;
            NavManager.LocationChanged -= LocationChanged;


        }


    }

}
