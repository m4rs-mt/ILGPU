// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2021-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicCanvasExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using System.Text.Json.Nodes;


namespace BlazorSampleApp.Components;

/// <summary>
/// All webgl context methods are setup as extension methods to the BasicCanvas
///
/// This approach is taken for clarity of inter-operation with JavaScript.
///
/// Documentation about 2d webgl rendering methods can be Googled, though Mozilla
/// documentation is fairly easy to read.
/// https://developer.mozilla.org/en-US/docs/Web/API/CanvasRenderingContext2D
/// </summary>

#nullable enable

public static class BasicCanvasExtensions
{



    public async static ValueTask CallElementMethod(this BasicCanvas basicCanvas,
        string methodName, ElementReference elementRef, params object?[]? args)
        => await basicCanvas.CallElementMethod(elementRef, methodName, args);

    public async static ValueTask<JsonObject> GetContextAttributes(this BasicCanvas basicCanvas) => await basicCanvas.GetFunctionBasicContext<JsonObject>("getContextAttributes");


    // basic values
    public async static ValueTask GlobalAlpha(this BasicCanvas basicCanvas, float alpha) => await basicCanvas.SetValueBasicContext("globalAlpha", alpha);

    public async static ValueTask<float> GlobalAlpha(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<float>("globalAlpha");

    public async static ValueTask Filter(this BasicCanvas basicCanvas, string filter) => await basicCanvas.SetValueBasicContext("filter", filter); // not supported by safari
    public async static ValueTask<string> Filter(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("filter");




    // transformations

    public async static ValueTask SetTransform(this BasicCanvas basicCanvas, float a, float b, float c, float d, float e, float f) => await basicCanvas.SetFunctionBasicContext("setTransform", a, b, c, d, e, f);

    public async static ValueTask SetTransform(this BasicCanvas basicCanvas, double a, double b, double c, double d, double e, double f) => await basicCanvas.SetFunctionBasicContext("setTransform", a, b, c, d, e, f);


    public async static ValueTask<JsonObject> GetTransform(this BasicCanvas basicCanvas) => await basicCanvas.GetFunctionBasicContext<JsonObject>("getTransform");
    public async static ValueTask Transform(this BasicCanvas basicCanvas, float a, float b, float c, float d, float e, float f) => await basicCanvas.SetFunctionBasicContext("transform", a, b, c, d, e, f);
    public async static ValueTask Transform(this BasicCanvas basicCanvas, double a, double b, double c, double d, double e, double f) => await basicCanvas.SetFunctionBasicContext("transform", a, b, c, d, e, f);

    public async static ValueTask ResetTransform(this BasicCanvas basicCanvas) => await basicCanvas.SetFunctionBasicContext("resetTransform");
    public async static ValueTask Translate(this BasicCanvas basicCanvas, float x, float y) => await basicCanvas.SetFunctionBasicContext("translate", x, y);

    public async static ValueTask Rotate(this BasicCanvas basicCanvas, float angle) => await basicCanvas.SetFunctionBasicContext("rotate", angle);

    public async static ValueTask Scale(this BasicCanvas basicCanvas, float x, float y) => await basicCanvas.SetFunctionBasicContext("scale", x, y);


    // path methods
    public async static ValueTask BeginPath(this BasicCanvas basicCanvas) => await basicCanvas.SetFunctionBasicContext("beginPath");
    public async static ValueTask ClosePath(this BasicCanvas basicCanvas) => await basicCanvas.SetFunctionBasicContext("closePath");
    public async static ValueTask Clip(this BasicCanvas basicCanvas) => await basicCanvas.SetFunctionBasicContext("clip");

    public async static ValueTask MoveTo(this BasicCanvas basicCanvas, float x, float y) => await basicCanvas.SetFunctionBasicContext("moveTo", x, y);
    public async static ValueTask LineTo(this BasicCanvas basicCanvas, float x, float y) => await basicCanvas.SetFunctionBasicContext("lineTo", x, y);

    public async static ValueTask Arc(this BasicCanvas basicCanvas, int x, int y, float radius, float startAngle, float endAngle, bool counterclockwise = false) => await basicCanvas.SetFunctionBasicContext("arc", x, y, radius, startAngle, endAngle, counterclockwise);

    public async static ValueTask ArcTo(this BasicCanvas basicCanvas, int x1, int y1, int x2, int y2, float radius) => await basicCanvas.SetFunctionBasicContext("arcTo", x1, y1, x2, y2, radius);

    public async static ValueTask BezierCurveTo(this BasicCanvas basicCanvas, int cp1x, int cp1y, int cp2x, int cp2y, int x, int y) => await basicCanvas.SetFunctionBasicContext("bezierCurveTo", cp1x, cp1y, cp2x, cp2y, x, y);

    public async static ValueTask Ellipse(this BasicCanvas basicCanvas, int x, int y, int radiusX, int radiusY, int rotation, float startAngle, float endAngle, bool counterclockwise = false) => await basicCanvas.SetFunctionBasicContext("ellipse", x, y, radiusX, radiusY, rotation, startAngle, endAngle, counterclockwise);

    public async static ValueTask LineWidth(this BasicCanvas basicCanvas, float pixels) => await basicCanvas.SetValueBasicContext("lineWidth", pixels);

    public async static ValueTask<float> LineWidth(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<float>("lineWidth");

    public async static ValueTask MiterLimit(this BasicCanvas basicCanvas, float limit) => await basicCanvas.SetValueBasicContext("miterLimit", limit);

    public async static ValueTask<float> MiterLimit(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<float>("miterLimit");

    public async static ValueTask LineCap(this BasicCanvas basicCanvas, CanvasLineCap value) => await basicCanvas.SetValueBasicContext("lineCap", value.ToString());

    public async static ValueTask<string> LineCap(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("lineCap");


    public async static ValueTask LineJoin(this BasicCanvas basicCanvas, CanvasLineJoin joinType) => await basicCanvas.SetValueBasicContext("lineJoin", joinType.ToString());

    public async static ValueTask LineDashOffset(this BasicCanvas basicCanvas, int lineDashOffset) => await basicCanvas.SetValueBasicContext("lineDashOffset", lineDashOffset);

    public async static ValueTask Stroke(this BasicCanvas basicCanvas) => await basicCanvas.SetFunctionBasicContext("stroke");

    public async static ValueTask StrokeStyle(this BasicCanvas basicCanvas, string strokeStyle) => await basicCanvas.SetValueBasicContext("strokeStyle", strokeStyle);

    // draw methods

    public async static ValueTask GlobalCompositeOperation(this BasicCanvas basicCanvas, string operation) => await basicCanvas.SetValueBasicContext("globalCompositeOperation", operation);

    public async static ValueTask<string> GlobalCompositeOperation(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("globalCompositeOperation");


    public async static ValueTask Fill(this BasicCanvas basicCanvas) => await basicCanvas.SetFunctionBasicContext("fill");
    public async static ValueTask FillStyle(this BasicCanvas basicCanvas, string style) => await basicCanvas.SetValueBasicContext("fillStyle", style);
    public async static ValueTask<string> FillStyle(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("fillStyle");


    public async static ValueTask ShadowColor(this BasicCanvas basicCanvas, string color) => await basicCanvas.SetValueBasicContext("shadowColor", color);
    public async static ValueTask<string> ShadowColor(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("shadowColor");

    public async static ValueTask ShadowBlur(this BasicCanvas basicCanvas, float blur) => await basicCanvas.SetValueBasicContext("shadowBlur", blur);
    public async static ValueTask<float> ShadowBlur(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<float>("shadowBlur");


    public async static ValueTask ShadowOffsetX(this BasicCanvas basicCanvas, int offset) => await basicCanvas.SetValueBasicContext("shadowOffsetX", offset);
    public async static ValueTask<float> ShadowOffsetX(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<float>("shadowOffsetX");

    public async static ValueTask ShadowOffsetY(this BasicCanvas basicCanvas, int offset) => await basicCanvas.SetValueBasicContext("shadowOffsetY", offset);
    public async static ValueTask<float> ShadowOffsetY(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<float>("shadowOffsetY");


    public async static ValueTask Save(this BasicCanvas basicCanvas) => await basicCanvas.SetFunctionBasicContext("save");
    public async static ValueTask Restore(this BasicCanvas basicCanvas) => await basicCanvas.SetFunctionBasicContext("restore");



    // rectangle methods
    public async static ValueTask ClearRect(this BasicCanvas basicCanvas, int x, int y, int width, int height) => await basicCanvas.SetFunctionBasicContext("clearRect", x, y, width, height);
    public async static ValueTask FillRect(this BasicCanvas basicCanvas, int x, int y, int width, int height) => await basicCanvas.SetFunctionBasicContext("fillRect", x, y, width, height);
    public async static ValueTask StrokeRect(this BasicCanvas basicCanvas, int x, int y, int width, int height) => await basicCanvas.SetFunctionBasicContext("strokeRect", x, y, width, height);

    // text methods
    public async static ValueTask Font(this BasicCanvas basicCanvas, string fontName) => await basicCanvas.SetValueBasicContext("font", fontName);
    public async static ValueTask<string> Font(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("font");
    public async static ValueTask TextAlign(this BasicCanvas basicCanvas, CanvasTextAlign align) => await basicCanvas.SetValueBasicContext("textAlign", align.ToString());
    public async static ValueTask<string> TextAlign(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("textAlign");
    public async static ValueTask TextBaseline(this BasicCanvas basicCanvas, CanvasTextBaseline baseline) => await basicCanvas.SetValueBasicContext("textBaseline", baseline.ToString());
    public async static ValueTask<string> TextBaseline(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("textBaseline");
    public async static ValueTask Direction(this BasicCanvas basicCanvas, CanvasDirection direction) => await basicCanvas.SetValueBasicContext("direction", direction.ToString());
    public async static ValueTask<string> Direction(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("direction");


    public async static ValueTask FillText(this BasicCanvas basicCanvas, string text, int x, int y) => await basicCanvas.SetFunctionBasicContext("fillText", text, x, y);
    public async static ValueTask FillText(this BasicCanvas basicCanvas, string text, int x, int y, int maxWidth) => await basicCanvas.SetFunctionBasicContext("fillText", text, x, y, maxWidth);
    public async static ValueTask StrokeText(this BasicCanvas basicCanvas, string text, int x, int y) => await basicCanvas.SetFunctionBasicContext("StrokeText", text, x, y);
    public async static ValueTask StrokeText(this BasicCanvas basicCanvas, string text, int x, int y, int maxWidth) => await basicCanvas.SetFunctionBasicContext("StrokeText", text, x, y, maxWidth);

    public async static ValueTask<JsonObject> MeasureText(this BasicCanvas basicCanvas, string text) => await basicCanvas.GetFunctionDrawingBasis<JsonObject>("measureText", text);// is this a dictionary?


    // image methods
    public async static ValueTask ImageSmoothingEnabled(this BasicCanvas basicCanvas, bool smoothingEnabled) => await basicCanvas.SetValueBasicContext("imageSmoothingEnabled", smoothingEnabled);
    public async static ValueTask<bool> ImageSmoothingEnabled(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<bool>("imageSmoothingEnabled");
    public async static ValueTask ImageSmoothingQuality(this BasicCanvas basicCanvas, string quality) => await basicCanvas.SetValueBasicContext("imageSmoothingQuality", quality); /// not supported by firefox
    public async static ValueTask<string> ImageSmoothingQuality(this BasicCanvas basicCanvas) => await basicCanvas.GetValueBasicContext<string>("imageSmoothingQuality");

    // image pixel manipulation
    public async static ValueTask CreateImageData(this BasicCanvas basicCanvas, string nameImageStorage, int width, int height, params object?[]? args) => await basicCanvas.SetFunctionDrawingBasis("createImageData", nameImageStorage, width, height, args);
    public async static ValueTask CreateImageDataCopyByteArray(this BasicCanvas basicCanvas, string nameImageStorage, int width, int height, params object?[]? args) => await basicCanvas.SetFunctionDrawingBasis("createImageDataCopyByteArray", nameImageStorage, width, height, args);
    public async static ValueTask GetImageData(this BasicCanvas basicCanvas, string nameImageStorage, int x, int y, int width, int height) => await basicCanvas.SetFunctionDrawingBasis("getImageData", nameImageStorage, x, y, width, height);
    public async static ValueTask PutImageData(this BasicCanvas basicCanvas, string nameImageStorage, int x, int y) => await basicCanvas.SetFunctionDrawingBasis("putImageData", nameImageStorage, x, y);
    public async static ValueTask PutImageData(this BasicCanvas basicCanvas, string nameImageStorage, int x, int y, int dx, int dy, int dWidth, int dHeight) => await basicCanvas.SetFunctionDrawingBasis("putImageDataPartial", nameImageStorage, x, y, dx, dy, dWidth, dHeight);

    // image support
    public async static ValueTask CreateImage(this BasicCanvas basicCanvas, string nameImageStorage, string source, string alt) => await basicCanvas.SetFunctionDrawingBasis("createImage", nameImageStorage, source, alt);
    public async static ValueTask DrawImage(this BasicCanvas basicCanvas, string nameImageStorage, int x, int y) => await basicCanvas.SetFunctionDrawingBasis("drawImage", nameImageStorage, x, y);


}
