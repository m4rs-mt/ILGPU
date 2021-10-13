'use strict'

// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: BasicCanvas.js
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------




/// <summary>
/// This attaches the html canvas of our Blazor Basic Canvas component to the 
/// webgl 2d drawing context. Allows Blazor to call webgl rendering context 
/// directly or using the DrawingBasis class when advanced webgl methods
/// need browser javascript accessible data. 
/// </summary>
function initializeBasicCanvas(canvasid, isWebassemblyClient, isTransparent) {

    const canvas = document.getElementById(canvasid); //'webgl-canvas'

    let context = canvas.getContext('2d', { alpha: false, colorSpace: 'srgb' });

    // If the browser supports a 2d webgl context then create a drawing basis 
    // and attach our drawing basis to the canvas for all webgl support
    if (context) {
        canvas.drawingBasis = new DrawingBasis(context, isWebassemblyClient, canvas);
    }


    return;
}




/// <summary>
/// Return a value from our rendering context
/// </summary>
function getValueBasicContext(drawcanvas, valueName) {

    // Blazor "params object?[]? args" as values can be passed as an array of arrays. 
    // When the first element is an array substitute the first element as the array.
    if (Array.isArray(valueName)) {
        valueName = valueName[0];       
    }


    if (drawcanvas && drawcanvas.drawingBasis) {

        const context = drawcanvas.drawingBasis.context;

        return context[valueName];
    }
}


/// <summary>
/// Set values on the webgl drawing context
/// </summary>
function setValueBasicContext(drawcanvas, valueName, values) {

    if (drawcanvas && drawcanvas.drawingBasis) {

        const context = drawcanvas.drawingBasis.context;

        // Blazor "params object?[]? args" as values can be passed as an array of arrays. 
        // When the first element is an array substitute the first element as the array.
        if (Array.isArray(values)) {

            if (Array.isArray(values[0])) {

                values = values[0];
            }           
        }
        context[valueName] = values;
    }
}


/// <summary>
/// This function allows us to call any webgl context function by name
/// </summary>
function setFunctionBasicContext(drawcanvas, functionName, values) {

    if (drawcanvas && drawcanvas.drawingBasis) {

        const context = drawcanvas.drawingBasis.context;

        if (Array.isArray(values))
        {
            // Blazor "params object?[]? args" as values can be passed as an array of arrays. 
            // When the first element is an array substitute the first element as the array.
            if (Array.isArray(values[0]))
            {
                values = values[0];
            }

            // call the named webgl function
            switch (values.length)
            {
                case 0: //no parameters empty array
                    context[functionName]();
                    break;
                case 1:
                    context[functionName](values[0]);
                    break;
                case 2:
                    context[functionName](values[0], values[1]);
                    break;
                case 3:
                    context[functionName](values[0], values[1], values[2]);
                    break;
                case 4:
                    context[functionName](values[0], values[1], values[2], values[3]);
                    break;
                case 5:
                    context[functionName](values[0], values[1], values[2], values[3], values[4]);
                    break;
                case 6:
                    context[functionName](values[0], values[1], values[2], values[3], values[4], values[5]);
                    break;
                case 7:
                    context[functionName](values[0], values[1], values[2], values[3], values[4], values[5], values[6]);
                    break;
                default:
                    context[functionName](values); // arrays longer than 7 terms have to be managed by the receiving function
                    break;
            }
        }
        else
        {
            context[functionName](values);
        }
    }
}


/// <summary>
/// more than a few webgl functions require additional support which we store in drawing basis class
/// </summary>
function setFunctionDrawingBasis(drawcanvas, functionName, values) {

    if (drawcanvas && drawcanvas.drawingBasis) {

        const drawingBasis = drawcanvas.drawingBasis;

        // Blazor "params object?[]? args" as values can be passed as an array of arrays. 
        // When the first element is an array substitute the first element as the array.
        if (Array.isArray(values)) {

            if (Array.isArray(values[0])) {
                values = values[0];
            }

            switch (values.length) {
                case 0: //no parameters empty array
                    drawingBasis[functionName]();
                    break;
                case 1:
                    drawingBasis[functionName](values[0]);
                    break;
                case 2:
                    drawingBasis[functionName](values[0], values[1]);
                    break;
                case 3:
                    drawingBasis[functionName](values[0], values[1], values[2]);
                    break;
                case 4:
                    drawingBasis[functionName](values[0], values[1], values[2], values[3]);
                    break;
                case 5:
                    drawingBasis[functionName](values[0], values[1], values[2], values[3], values[4]);
                    break;
                case 6:
                    drawingBasis[functionName](values[0], values[1], values[2], values[3], values[4], values[5]);
                    break;
                case 7:
                    drawingBasis[functionName](values[0], values[1], values[2], values[3], values[4], values[5], values[6]);
                    break;
                default:
                    drawingBasis[functionName](values); // arrays longer than 7 terms have to be managed by the receiving function
                    break;
            }
        } else {
            drawingBasis[functionName](values);
        }
    }
}



/// <summary>
/// DrawingBasis is a wrapper class to simplify use of the CanvasRenderingContext2D 
///
/// Remember everything in Javascript is executing in the client browser therefore 
/// all resources have to be "pushed" to the browser to be accessible to the
/// webgl context and rendering javascript.
/// </summary>



class DrawingBasis {

    constructor(canvasContext, isWebAssemblyClient, canvas) {
        // reference for GLContext
        this.context = canvasContext;
        // reference for .NetCalls  

        this.canvas = canvas;

        // is the rest of the Blazor page "in process" with the WebGL canvas?
        this.isWebAssemblyClient = isWebAssemblyClient;

        // we need to store information where the webgl methods can access the information
        this.imageStorage = [];
        this.gradientStorage = [];
        this.patternStorage = [];
        this.transformStorage = [];
    }

    // oddly enough most methods will call directly on the context, however we will want to manage resources by name

    // image processing
    createImageData(name, width, height) {
        this.imageStorage[name] = this.context.createImageData(width, height);
    }

    createImageData(name, sourceName, width, height = 0) {
        if (height == 0) {
            this.imageStorage[name] = this.context.createImageData(this.imageStorage[sourceName], width * 4); // only create the same size canvas
        }
        else {
            this.imageStorage[name] = this.context.createImageData(this.imageStorage[sourceName], width * 4, height * 4); // only create the same size canvas
        }
    }

    // We create a new image in webgl and then copy pixel values. 
    // An 800 x 600 image will be approximately 2MB in size
    // therefor for performance reasons this is a poor approach,  
    // data compression would be appropriate for transmission.
    // For example generating a compressed PNG file on the server
    // and having the canvas download the image file would reduce the
    // load on server bandwidth by 80% or more.
    createImageData(name, width, height, values) // r,g,b,a array
    {

        values = values[0];
       
        delete this.imageStorage[name];

        // create a blank image, image data is always initially blank
        const imageData = this.context.createImageData(width, height);
        this.imageStorage[name] = imageData;
        const length = imageData.data.length;

        // Different runtimes will return either a based64 encoded string or the actual Uint8Array
        if (values instanceof Uint8Array) {
            // Copy contents from the source.
            for (let i = 0; i < length; i += 1) {
                imageData.data[i] = values[i];
            }
        }
        else // Otherwise we have a base 64 encoded string
        {
            // Likely there are better ways to decode and copy in JavaScript
            var binary_string = window.atob(values);
            for (let i = 0; i < length; i += 1) {
                imageData.data[i] = binary_string.charCodeAt(i);[i];
            }
        }
    }

    // paint an image to the canvas
    putImageData(name, x, y) {
        this.context.putImageData(this.imageStorage[name], x, y);
    }

    // paint an image to the canvas limited to the dirty area to be repainted
    putImageDataRepaint(name, x, y, dirtyX, dirtyY, dirtyWidth, dirtyHeight) {
        this.context.putImageData(this.imageStorage[name], x, y, dirtyX, dirtyY, dirtyWidth, dirtyHeight);
    }

    // webgl supports an assortment of functions for loading image files 

    //transforms
    createTransform(name, m11, m12, m21, m22, dx, dy) {
        this.transformStorage[name] = new DOMMatrix([m11, m12, m21, m22, dx, dy]);
    }

    setTransform(name) {
        this.context.setTransform(this.transformStorage[name]);
    }

    // patterns
    createPattern(name, imageName, repetition) {
        this.patternStorage[name] = createPattern(this.imageStorage[name], repetition);
    }

    setPatternFillStyle(name) {
        this.context.fillStyle = this.patternStorage[name];
    }

    //Gradients
    createLinearGradient(name, x0, y0, x1, y1) {
        this.gradientStorage[name] = this.context.createLinearGradient(x0, y0, x1, y1);
    }

    createRadialGradient(name, x0, y0, r0, x1, y1, r1) {
        this.gradientStorage[name] = this.context.createRadialGradient(x0, y0, r0, x1, y1, r1);
    }

    addColorStop(name, stop, color) {
        this.gradientStorage[name].addColorStop(stop, color);
    }

    setGradientFillStyle(name) {
        this.context.fillStyle = this.gradientStorage[name];
    }
}