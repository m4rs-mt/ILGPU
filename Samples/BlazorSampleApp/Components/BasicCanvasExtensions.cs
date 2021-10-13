// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: BasicCanvasExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------



using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;

namespace BlazorSampleApp.Components
{
#nullable enable

    /// <summary>
    /// All webgl context methods are setup as extension methods to the BasicCanvas
    /// 
    /// This approach is taken for clarity of inter-operation with JavaScript.
    /// 
    /// Documentation about 2d webgl rendering methods can be Googled, though Mozilla 
    /// documentation is fairly easy to read.
    /// https://developer.mozilla.org/en-US/docs/Web/API/CanvasRenderingContext2D
    /// </summary>
    public static class BasicCanvasExtensions
    {
         
        public async static Task GlobalAlpha(this BasicCanvas basicCanvas, float alpha) => await basicCanvas.SetValue("globalAlpha", alpha);

        public async static Task Filter(this BasicCanvas basicCanvas, float filter) => await basicCanvas.SetValue("filter", filter); // not supported by safari

        public async static Task ImageSmoothingEnabled(this BasicCanvas basicCanvas, bool smoothingEnabled) => await basicCanvas.SetValue("imageSmoothingEnabled ", smoothingEnabled);

        public async static Task Transform(this BasicCanvas basicCanvas, float m11, float m12, float m21, float m22, float dx, float dy) => await basicCanvas.SetFunction("transform", m11, m12, m21, m22, dx, dy);

        public async static Task Translate(this BasicCanvas basicCanvas, float x, float y) => await basicCanvas.SetFunction("translate", x, y);


        // path methods
        public async static Task BeginPath(this BasicCanvas basicCanvas) => await basicCanvas.SetFunction("beginPath");
        public async static Task ClosePath(this BasicCanvas basicCanvas) => await basicCanvas.SetFunction("closePath");
        public async static Task Clip(this BasicCanvas basicCanvas) => await basicCanvas.SetFunction("clip");

        public async static Task MoveTo(this BasicCanvas basicCanvas, float x, float y) => await basicCanvas.SetFunction("moveTo", x, y);
        public async static Task LineTo(this BasicCanvas basicCanvas, float x, float y) => await basicCanvas.SetFunction("lineTo", x, y);

        public async static Task Arc(this BasicCanvas basicCanvas, int x, int y, float radius, float startAngle, float endAngle, bool counterclockwise = false) => await basicCanvas.SetFunction("arc", x, y, radius, startAngle, endAngle, counterclockwise);

        public async static Task ArcTo(this BasicCanvas basicCanvas, int x1, int y1, int x2, int y2, float radius) => await basicCanvas.SetFunction("arcTo", x1, y1, x2, y2, radius);

        public async static Task BezierCurveTo(this BasicCanvas basicCanvas, int cp1x, int cp1y, int cp2x, int cp2y, int x, int y) => await basicCanvas.SetFunction("bezierCurveTo", cp1x, cp1y, cp2x, cp2y, x, y);

        public async static Task Ellipse(this BasicCanvas basicCanvas, int x, int y, int radiusX, int radiusY, int rotation, float startAngle, float endAngle, bool counterclockwise = false) => await basicCanvas.SetFunction("ellipse", x, y, radiusX, radiusY, rotation, startAngle, endAngle, counterclockwise);

        public async static Task LineWidth(this BasicCanvas basicCanvas, float pixels) => await basicCanvas.SetValue("lineWidth", pixels);

        public async static Task MiterLimit(this BasicCanvas basicCanvas, float limit) => await basicCanvas.SetValue("miterLimit", limit);

        public async static Task LineCap(this BasicCanvas basicCanvas, string lineCap) => await basicCanvas.SetValue("lineCap", lineCap);

        public async static Task LineJoin(this BasicCanvas basicCanvas, string joinType) => await basicCanvas.SetValue("lineJoin", joinType);

        public async static Task LineDashOffset(this BasicCanvas basicCanvas, int lineDashOffset) => await basicCanvas.SetValue("lineDashOffset", lineDashOffset);

        public async static Task Stroke(this BasicCanvas basicCanvas) => await basicCanvas.SetFunction("stroke");

        public async static Task StrokeStyle(this BasicCanvas basicCanvas, string strokeStyle) => await basicCanvas.SetValue("strokeStyle", strokeStyle);

        // draw methods

        public async static Task GlobalCompositeOperation(this BasicCanvas basicCanvas, string operation) => await basicCanvas.SetValue("globalCompositeOperation", operation);
        public async static Task Fill(this BasicCanvas basicCanvas) => await basicCanvas.SetFunction("fill");
        public async static Task FillStyle(this BasicCanvas basicCanvas, string style) => await basicCanvas.SetValue("fillStyle", style);
        public async static Task Filter(this BasicCanvas basicCanvas, string filter) => await basicCanvas.SetValue("filter", filter);

        public async static Task ShadowColor(this BasicCanvas basicCanvas, string color) => await basicCanvas.SetValue("shadowColor", color);

        public async static Task ShadowBlur(this BasicCanvas basicCanvas, int blur) => await basicCanvas.SetValue("shadowBlur", blur);
        public async static Task ShadowOffsetX(this BasicCanvas basicCanvas, int offset) => await basicCanvas.SetValue("shadowOffsetX", offset);
        public async static Task ShadowOffsetY(this BasicCanvas basicCanvas, int offset) => await basicCanvas.SetValue("shadowOffsetY", offset);



        // rectangle methods
        public async static Task ClearRect(this BasicCanvas basicCanvas, int x, int y, int width, int height) => await basicCanvas.SetFunction("clearRect", x, y, width, height);
        public async static Task FillRect(this BasicCanvas basicCanvas, int x, int y, int width, int height) => await basicCanvas.SetFunction("fillRect", x, y, width, height);


        // text methods
       
        public async static Task Font(this BasicCanvas basicCanvas, string fontName) => await basicCanvas.SetValue("font", fontName);

        public async static Task TextAlign(this BasicCanvas basicCanvas, string align) => await basicCanvas.SetValue("textAlign", align);
        public async static Task TextBaseline(this BasicCanvas basicCanvas, string baseline) => await basicCanvas.SetValue("textBaseline", baseline);

        public async static Task Direction(this BasicCanvas basicCanvas, string direction) => await basicCanvas.SetValue("direction", direction); // not supported by firefox
        public async static Task FillText(this BasicCanvas basicCanvas, int x, int y, int width, int height) => await basicCanvas.SetFunction("fillText", x, y, width, height);

        public async static Task FillText(this BasicCanvas basicCanvas, int x, int y, int width, int height, int maxWidth) => await basicCanvas.SetFunction("fillText", x, y, width, height, maxWidth);

        public async static Task StrokeText(this BasicCanvas basicCanvas, int x, int y, int width, int height) => await basicCanvas.SetFunction("strokeText", x, y, width, height);
        public async static Task StrokeText(this BasicCanvas basicCanvas, int x, int y, int width, int height, int maxWidth) => await basicCanvas.SetFunction("strokeText", x, y, width, height, maxWidth);


        // image methods
        public async static Task ImageSmoothingQuality(this BasicCanvas basicCanvas, string quality) => await basicCanvas.SetValue("imageSmoothingQuality ", quality); /// not supported by firefox

        public async static Task CreateImageData(this BasicCanvas basicCanvas, string nameImageStorage, int width, int height, params object?[]? args) => await basicCanvas.SetFunctionDrawingBasis("createImageData", nameImageStorage, width, height, args);

        public async static Task PutImageData(this BasicCanvas basicCanvas, string nameImageStorage, int x, int y) => await basicCanvas.SetFunctionDrawingBasis("putImageData", nameImageStorage, x, y);

        public async static Task PutImageData(this BasicCanvas basicCanvas, string nameImageStorage, int x, int y, int dirtyX, int dirtyY, int dirtyWidth, int dirtyHeight) => await basicCanvas.SetFunctionDrawingBasis("putImageDataRepaint", nameImageStorage, x, y, dirtyX, dirtyY, dirtyWidth, dirtyHeight);
    }
}
