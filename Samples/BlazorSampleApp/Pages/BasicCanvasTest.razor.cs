// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: MandelbrotBasic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

// the BasicCanvas component loads its library as a module during runtime to avoid polluting the global javascript name space.


using BlazorSampleApp.Components;

namespace BlazorSampleApp.Pages
{
    public partial class BasicCanvasTest
    {

#nullable disable
        public BasicCanvas Canvas2D { get; set; }

#nullable enable

        public bool DisabledButtons { get; set; } = true;



        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Canvas2D.CanvasInitComplete += Canvas2d_CanvasInitComplete;
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async void Canvas2d_CanvasInitComplete(BasicCanvas obj)
        {
            DisabledButtons = false;
            StateHasChanged();

            await Canvas2D.FillStyle("#009");
            await Canvas2D.FillRect(0, 0, Canvas2D.Width, Canvas2D.Height);

        }

        protected async void RenderMethods()
        {
            // call canvas methods from C#, note server side Blazor there is a 20 to 100+ millisecond round
            // trip which can lead to a poor user experience with too many back to back draw calls


            await Canvas2D.GlobalAlpha(1.0f);
            await Canvas2D.FillStyle("#009");
            await Canvas2D.FillRect(0, 0, Canvas2D.Width, Canvas2D.Height);
            await Canvas2D.FillStyle("#0Df");
            await Canvas2D.FillRect(0, 0, Canvas2D.Width / 2, Canvas2D.Height/2);
            await Canvas2D.FillStyle("#0C6");
            await Canvas2D.FillRect(Canvas2D.Width/2, 0, Canvas2D.Width / 2, Canvas2D.Height / 2);
            await Canvas2D.FillStyle("#F90");
            await Canvas2D.FillRect(0, 300, 400, 300);
            await Canvas2D.FillStyle("#03F");
            await Canvas2D.FillRect(Canvas2D.Width / 2, Canvas2D.Height / 2, Canvas2D.Width / 2, Canvas2D.Height / 2);
            await Canvas2D.FillStyle("#888");

            await Canvas2D.GlobalAlpha(0.2f);
            for (int i = 0; i < 7; i++)
            {
                await Canvas2D.BeginPath();
                await Canvas2D.Arc(Canvas2D.Width / 2, Canvas2D.Height/2, 30 + 30 * i, 0, MathF.PI * 2, true);
                await Canvas2D.Fill();
            }
            await Canvas2D.ClosePath();
        }


        protected async void InjectJavaScriptDemo()
        {
            // evil way to inject text as a method into our DrawingBasis class loaded in BasicCanvas.js module

            // the canvas InjectScript extension methods calls JavaScripts's eval function while inside the module namespace
            // injected the "MyUpdateCanvas" method into the DrawingBasis wrapper method, 

            await Canvas2D.InjectScript(@"
(function(){

 DrawingBasis.prototype.MyUpdateCanvas =
        function () {

            const ctx = this.context;

            // Draw background
            ctx.globalAlpha = 1.0;
            ctx.fillStype = '#009';
            ctx.fillRect(0,0," + Canvas2D.Width + "," + Canvas2D.Height + @")
            ctx.fillStyle = '#FD0';
            ctx.fillRect(0, 0, " + Canvas2D.Width / 2 + ", " + Canvas2D.Height/2 + @");
            ctx.fillStyle = '#6C0';
            ctx.fillRect(" + Canvas2D.Width/2 + ", 0, " + Canvas2D.Width / 2 + ", " + Canvas2D.Height/2 + @");
            ctx.fillStyle = '#09F';
            ctx.fillRect(0, " + Canvas2D.Height/2 + ", " + Canvas2D.Width / 2 + ", " + Canvas2D.Height/2 + @");
            ctx.fillStyle = '#F30';
            ctx.fillRect(" + Canvas2D.Width/2 + ", " + Canvas2D.Height/2 + ", " + Canvas2D.Width/2 + ", " + Canvas2D.Height/2 + @");
            ctx.fillStyle = '#FFF';

            // Set transparency value
            ctx.globalAlpha = 0.2;

            // Draw transparent circles
            for (let i = 0; i < 7; i++) {
                ctx.beginPath();
                ctx.arc(" + Canvas2D.Width / 2 + ",  " + Canvas2D.Height / 2 + @", 30 + 30 * i, 0, Math.PI * 2, true);
                ctx.fill();
            }
            ctx.closePath();
        };

})()
");
            // then call our new method in our BasicCanvas.js module to render the script without client server delay
            await Canvas2D.SetFunctionDrawingBasis("MyUpdateCanvas");
        }


    }
}
