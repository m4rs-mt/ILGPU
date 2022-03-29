using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using BlazorSampleApp.Components;
using BlazorSampleApp.MandelbrotExplorer;
using System.Drawing;
using System.Text.Json.Nodes;

namespace BlazorSampleApp.Pages
{
    public partial class BasicCanvasTest : ComponentBase
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


        protected async void Render1()
        {
            await Canvas2D.GlobalAlpha(1.0f);
            await Canvas2D.FillStyle("#009");
            await Canvas2D.FillRect(0, 0, Canvas2D.Width, Canvas2D.Height);
            await Canvas2D.FillStyle("#0Df");
            await Canvas2D.FillRect(0, 0, 300, 300);
            await Canvas2D.FillStyle("#0C6");
            await Canvas2D.FillRect(300, 0, 300, 300);
            await Canvas2D.FillStyle("#F90");
            await Canvas2D.FillRect(0, 300, 300, 300);
            await Canvas2D.FillStyle("#03F");
            await Canvas2D.FillRect(300, 300, 300, 300);
            await Canvas2D.FillStyle("#888");

            await Canvas2D.GlobalAlpha(0.2f);
            for (int i = 0; i < 7; i++)
            {
                await Canvas2D.BeginPath();
                await Canvas2D.Arc(300, 300, 30 + 30 * i, 0, MathF.PI * 2, true);
                await Canvas2D.Fill();
            }
            await Canvas2D.ClosePath();
        }


        protected async void TestLocalScript()
        {
            // evil way to add methods to our drawing basis

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
            ctx.fillRect(0, 0, 300, 300);
            ctx.fillStyle = '#6C0';
            ctx.fillRect(300, 0, 300, 300);
            ctx.fillStyle = '#09F';
            ctx.fillRect(0, 300, 300, 300);
            ctx.fillStyle = '#F30';
            ctx.fillRect(300, 300, 300, 300);
            ctx.fillStyle = '#FFF';

            // Set transparency value
            ctx.globalAlpha = 0.2;

            // Draw transparent circles
            for (let i = 0; i < 7; i++) {
                ctx.beginPath();
                ctx.arc(300, 300, 30 + 30 * i, 0, Math.PI * 2, true);
                ctx.fill();
            }
            ctx.closePath();
        };

})()
");

            await Canvas2D.SetFunctionDrawingBasis("MyUpdateCanvas");
        }



        protected async void TestImageMethods()
        {

            int[] buffer = new int[Canvas2D.Width * Canvas2D.Height];

            int[] display = new int[2] { Canvas2D.Width, Canvas2D.Height };

            float[] view = new float[4] { -2, 1, -1, 1 };

            MandelbrotExtensions.CalcCPUParallel(buffer, display, view, 1000);
            await MandelbrotExtensions.Draw(Canvas2D, buffer, Canvas2D.Width, Canvas2D.Height, 1000, Color.Blue);



            StateHasChanged();
            Thread.Sleep(2000);

            await Canvas2D.GetImageData("Subsection", 0, 0, Canvas2D.Width / 2, Canvas2D.Height / 2);
            await Canvas2D.PutImageData("Subsection", Canvas2D.Width / 2, Canvas2D.Height / 2);
            await Canvas2D.PutImageData("Subsection", 100, 100, Canvas2D.Width / 4, Canvas2D.Height / 4, Canvas2D.Width / 4, Canvas2D.Height / 4);

        }


        protected async void TestGetMethods()
        {

            JsonObject attributes = await Canvas2D.GetContextAttributes();

            float globalAlpha = await Canvas2D.GlobalAlpha();

            string filter = await Canvas2D.Filter();

            JsonObject transform = await Canvas2D.GetTransform();

            float lineWidth = await Canvas2D.LineWidth();

            float MiterLimit = await Canvas2D.MiterLimit();

            string LineCap = await Canvas2D.LineCap();

            string GlobalCompositeOperation = await Canvas2D.GlobalCompositeOperation();

            string FillStyle = await Canvas2D.FillStyle();

            string ShadowColor = await Canvas2D.ShadowColor();

            float ShadowBlur = await Canvas2D.ShadowBlur();

            float ShadowOffsetX = await Canvas2D.ShadowOffsetX();

            float ShadowOffsetY = await Canvas2D.ShadowOffsetY();


            string Font = await Canvas2D.Font();
            string TextAlign = await Canvas2D.TextAlign();
            string TextBaseline = await Canvas2D.TextBaseline();
            string Direction = await Canvas2D.Direction();


            JsonObject MeasureText = await Canvas2D.MeasureText("Really?");



    


        }
    }
}
