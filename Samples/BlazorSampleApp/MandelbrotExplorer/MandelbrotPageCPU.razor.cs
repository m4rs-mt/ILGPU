// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2022-2024 ILGPU Project
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

        public string ExecutionsDetails { get; set; }

        public bool _disposing = false;

        public bool _computing = false;

        private Device _lastDevice = null;

        private List<Device> cpuBasedDevices = new List<Device>();

        private ElementReference DeviceSelect { get; set; }

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

        /// <summary>
        /// Create a CPU accelerator device list
        /// </summary>
        private void GetAvailableDevices()
        {
            SystemDeviceNames.Add("Single Thread");
            SystemDeviceNames.Add("Parallel CPU");


            foreach (Device device in MandelbrotInstance.ContextInstance.Devices)
            {
                switch (device.AcceleratorType)
                {
                    case AcceleratorType.Velocity:
                    case AcceleratorType.CPU:
                        cpuBasedDevices.Add(device);
                        SystemDeviceNames.Add(device.Name);
                        break;

                }

            }


        }


        readonly List<string> SystemDeviceNames = new List<string>();



        // Measure performance
        private static Stopwatch _stopWatch;

        private void RestartWatch()
        {
            _stopWatch.Reset();
            _stopWatch.Start();
        }

        private string ElapsedTime()
        {
            _stopWatch.Stop();
            return
                   $"{_stopWatch.Elapsed.Minutes:00}:{_stopWatch.Elapsed.Seconds:00}.{_stopWatch.Elapsed.Milliseconds:000} ";
        }


        private string DeviceName { get; set; } = "Single Thread";

        private async void UpdateSelected(ChangeEventArgs e)
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


        private readonly List<string> executionDetails = new List<string>();


        private void PopulateExecutionDetails()
        {
            string result = String.Empty;

            foreach (string s in executionDetails)
            {
                result = result + s + "<br/>";
            }

            ExecutionsDetails = result;
        }

        private void SetExecutionDetailCompile(int position, string acceleratorName, string compileTime)
        {
            for (int i = executionDetails.Count; i < SystemDeviceNames.Count*2; i++)
            {
                executionDetails.Add(string.Empty);
            }

            executionDetails[position*2] = acceleratorName + " Compile: " + compileTime;

            PopulateExecutionDetails();
        }

        private void SetExecutionDetailRun(int position, string acceleratorName, string runTime)
        {
            for (int i = executionDetails.Count; i < SystemDeviceNames.Count*2; i++)
            {
                executionDetails.Add(string.Empty);
            }
            executionDetails[position*2+1] = acceleratorName + " Run: " + runTime;

            PopulateExecutionDetails();

        }



        /// <summary>
        ///
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
        private int[] Crunch(string deviceName)
        {
            int[] data = new int[displayPort[0] * displayPort[1]];

            if (DeviceName == "Single Thread")
            {
                RestartWatch();
                MandelbrotExtensions.CalcCPUSingle(data, displayPort, areaView,
                    maxIterations); // Single thread CPU
                SetExecutionDetailRun(0,DeviceName, ElapsedTime());


            }
            else if (DeviceName == "Parallel CPU")
            {
                RestartWatch();
                MandelbrotExtensions.CalcCPUParallel(data, displayPort, areaView,
                    maxIterations); // parallel thread CPU
                SetExecutionDetailRun(1,DeviceName, ElapsedTime());


            }
            else
            {
                _computing = true;
                var device = cpuBasedDevices.Find(x => x.Name == deviceName);

                if (_lastDevice != device)
                {
                    RestartWatch();
                    MandelbrotInstance.CompileKernel(device);
                    SetExecutionDetailCompile(SystemDeviceNames.IndexOf(DeviceName),DeviceName, ElapsedTime());
                    _lastDevice = device;
                }


                RestartWatch();

                MandelbrotInstance.CalcGPU(ref data, displayPort, areaView,
                    maxIterations); // ILGPU-CPU-Mode
                _computing = false;
                SetExecutionDetailRun(SystemDeviceNames.IndexOf(DeviceName),DeviceName, ElapsedTime());




            }

            return data;
        }


        private async void ProfileMandelbrot()
        {
            foreach (string deviceName in SystemDeviceNames)
            {
                DeviceName = deviceName;
                await Canvas2D.SetElementValue(DeviceSelect, "value", deviceName);
                StateHasChanged();
            }


        }


        /// <summary>
        /// Animate 500 frames while narrowing view to small subsection of the Mandelbrot set.
        /// </summary>

        private async void AnimateMandelbrot()
        {
            int[] data = new int[displayPort[0] * displayPort[1]];


            float offsetX = -0.02f;
            float offsetY = 0.00562f;

            switch (DeviceName)
            {
                case "Single Thread":

                    for (int i = 0; i < 500; i++)
                    {
                        if (_disposing)
                            break;
                        RestartWatch();

                        MandelbrotExtensions.CalcCPUSingle(data, displayPort,
                            areaView, maxIterations); // ILGPU-CPU-Mode


                        areaView[0] = areaView[0] * 0.98f + offsetX;
                        areaView[1] = areaView[1] * 0.98f + offsetX;
                        areaView[2] = areaView[2] * 0.98f + offsetY;
                        areaView[3] = areaView[3] * 0.98f + offsetY;

                        if (_disposing)
                            break;
                        RestartWatch();

                        await MandelbrotExtensions.Draw(Canvas2D, data,
                            displayPort[0], displayPort[1], maxIterations,
                            Color.Blue);



                        StateHasChanged();


                    }



                    break;


                case "Parallel CPU":


                    for (int i = 0; i < 500; i++)
                    {
                        if (_disposing)
                            break;

                        RestartWatch();
                        MandelbrotExtensions.CalcCPUParallel(data, displayPort,
                            areaView, maxIterations); // ILGPU-CPU-Mode

                        areaView[0] = areaView[0] * 0.98f + offsetX;
                        areaView[1] = areaView[1] * 0.98f + offsetX;
                        areaView[2] = areaView[2] * 0.98f + offsetY;
                        areaView[3] = areaView[3] * 0.98f + offsetY;

                        if (_disposing)
                            break;

                        RestartWatch();
                        await MandelbrotExtensions.Draw(Canvas2D, data,
                            displayPort[0], displayPort[1], maxIterations,
                            Color.Blue);



                        StateHasChanged();


                    }


                    break;


                default:
                    _computing = true;

                    var device = cpuBasedDevices.Find(x => x.Name == DeviceName);


                    if (_lastDevice != device)
                    {
                        RestartWatch();
                        MandelbrotInstance.CompileKernel(device);
                        _lastDevice = device;

                    }

                    MandelbrotInstance.InitGPURepeat(ref data, displayPort, areaView,
                        maxIterations);

                    for (int i = 0; i < 500; i++)
                    {
                        if (_disposing)
                            break;

                        RestartWatch();

                        MandelbrotInstance.CalcGPURepeat(ref data, displayPort,
                            areaView, maxIterations); // ILGPU-CPU-Mode

                        areaView[0] = areaView[0] * 0.98f + offsetX;
                        areaView[1] = areaView[1] * 0.98f + offsetX;
                        areaView[2] = areaView[2] * 0.98f + offsetY;
                        areaView[3] = areaView[3] * 0.98f + offsetY;

                        if (_disposing)
                            break;
                        RestartWatch();

                        await MandelbrotExtensions.Draw(Canvas2D, data,
                            displayPort[0], displayPort[1], maxIterations,
                            Color.Blue);



                        StateHasChanged();


                    }

                    MandelbrotInstance.CleanupGPURepeat();
                    _computing = false;
                    break;
            }
        }


    }

}


