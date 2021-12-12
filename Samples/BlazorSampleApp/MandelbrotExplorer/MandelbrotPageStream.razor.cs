// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: MandelbrotPageStream.razor.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using BlazorSampleApp.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace BlazorSampleApp.MandelbrotExplorer
{
    /// <summary>
    /// An example razor page calling a streamed accelerator via an injected IMandelbrotClient.
    /// </summary>
    public partial class MandelbrotPageStream: IDisposable
    {
#nullable disable
        public BasicCanvas Canvas2D { get; set; }

        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        IMandelbrotClient MandelbrotInstance { get; set; }
        
        [Inject]
        NavigationManager NavManager { get; set; }



        // current details
        public string ExecutionsDetails1 { get; set; }

        public string ExecutionsDetails2 { get; set; }

        public string ExecutionsDetails3 { get; set; }

        private Stopwatch _stopWatch;

        void LocationChanged(object sender, LocationChangedEventArgs e)
        {
            // assume we're leaving this page for good, preempt new computation
            _disposing = true;
        }

#nullable enable
        public bool DisabledButtons { get; set; } = true;

        string DeviceName { get; set; } = "n/a";

        // Mandelbrot parameters
        float[] areaView = new float[4];

        // Mandelbrot render depth
        int maxIterations = 1000;


        // Canvas size
        int[] displayPort = new int[2];

        
        private bool _disposing = false;

      
              
      
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


        // Initialize razor page 
        protected override void OnInitialized()
        {
            _stopWatch = new Stopwatch();

            NavManager.LocationChanged += LocationChanged;

            base.OnInitialized();
        }


        // Remove navigation detection if this page is ending.
        public void Dispose()
        {
            _disposing = true;
            NavManager.LocationChanged -= LocationChanged;
        }


        // This only gets called when the user has navigated elsewhere, this prempts the.
    


        // Once the razor page/component is render complete we can interact with the browser
        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                // We can't call any webgl functions until the page is fully rendered and the HTML canvas and JavaScript are fully loaded.
                Canvas2D.CanvasInitComplete += CanvasInitComplete;

            }
            base.OnAfterRender(firstRender);
        }


        /// <summary>
        /// Render an initial Mandelbrot graph once this razor page/component loads
        /// </summary>
        /// <param name="basicCanvas"></param>
        private async void CanvasInitComplete(BasicCanvas basicCanvas)
        {

            DeviceName = MandelbrotInstance.AcceleratorName();

            displayPort[0] = Canvas2D.Width;
            displayPort[1] = Canvas2D.Height;
            areaView[0] = -2.0f;
            areaView[1] = 1.0f;
            areaView[2] = -1.0f;
            areaView[3] = 1.0f;
            maxIterations = 1000;
            
            if (_disposing || !MandelbrotInstance.IsActive)
            {
                return;
            }
            RestartWatch();
            MandelbrotInstance.SetDisplay(Canvas2D.Width, Canvas2D.Height);
            ExecutionsDetails1 = ElapsedTime($"IL Compile - {DeviceName}");

            if (_disposing || !MandelbrotInstance.IsActive)
            {
                return;
            }
            RestartWatch();
            int[] data = await MandelbrotInstance.CalculateMandelbrot(areaView[0], areaView[1], areaView[2], areaView[3]); // ILGPU-CPU-Mode
            ExecutionsDetails2 = ElapsedTime($"IL Run - {DeviceName}");
            
            if (data == null || _disposing)
            {
                return;
            }

            await MandelbrotExtensions.Draw(Canvas2D, data, displayPort[0], displayPort[1], maxIterations, Color.Blue);

            DisabledButtons = false;
            StateHasChanged(); // note StateHasChanged will force an update of controls on our razor page.
        }



       
        /// <summary>
        /// Animate 500 frames while narrowing view to small subsection of the Mandelbrot set.
        /// </summary>
        public async void AnimateMandelbrot()
        {
            
            // Target information
            float offsetX = -0.02f;
            float offsetY = 0.00562f;

            for (int i = 0; i < 500; i++)
            {
                if (_disposing || !MandelbrotInstance.IsActive)
                {
                    break;
                }

                // Set the next areaView size
                areaView[0] = areaView[0] * 0.98f + offsetX;
                areaView[1] = areaView[1] * 0.98f + offsetX;
                areaView[2] = areaView[2] * 0.98f + offsetY;
                areaView[3] = areaView[3] * 0.98f + offsetY;

                // Generate the next frame of this animation.
                RestartWatch();
                int[] data = await MandelbrotInstance.CalculateMandelbrot(areaView[0], areaView[1], areaView[2], areaView[3]); // ILGPU-CPU-Mode
                if (data==null || _disposing)
                {
                    break;
                }
                ExecutionsDetails2 = ElapsedTime($"IL Run - {DeviceName}");

                
           

                // Render the generated frame to the canvas.
                RestartWatch();
                await MandelbrotExtensions.Draw(Canvas2D, data, displayPort[0], displayPort[1], maxIterations, Color.Blue);
                StateHasChanged();

                ExecutionsDetails3 = ElapsedTime("Web Server Render");

            }
        }
    }
   
}
