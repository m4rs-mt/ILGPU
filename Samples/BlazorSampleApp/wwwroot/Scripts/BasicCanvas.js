// ---------------------------------------------------------------------------------------
//                                        KilnGod.BlazorWebGL
//
// File: BasicCanvas.razor.cs
//
// This file is part of KilnGod.BlazorWebGL and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

export function initializeBasicCanvas(canvasid, isWebassemblyClient, isTransparent, isDesynchronized) {
    "use strict";


    const canvas = window.document.getElementById(canvasid); //'webgl-canvas'

    let context = canvas.getContext("2d", { alpha: isTransparent, colorSpace: "srgb", desynchronized: isDesynchronized });

    // If the browser supports a 2d webgl context then create a drawing basis
    // and attach our drawing basis to the canvas for all webgl support
    if (context) {
        canvas.drawingBasis = new DrawingBasis(context, isWebassemblyClient, canvas);
    }


    return;
}


export function InjectScript(scriptText) {

    eval(scriptText);

}


export function setValueBasicContext(drawcanvas, valueName, values) {
    "use strict";
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


export function setFunctionBasicContext(drawcanvas, functionName, values) {
    "use strict";
    if (drawcanvas && drawcanvas.drawingBasis) {

        const context = drawcanvas.drawingBasis.context;

        if (Array.isArray(values)) {
            // Blazor "params object?[]? args" as values can be passed as an array of arrays. 
            // When the first element is an array substitute the first element as the array.
            if (Array.isArray(values[0])) {
                values = values[0];
            }

            // call the named webgl function
            switch (values.length) {
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
        else {
            context[functionName](values);
        }
    }
}





export function setFunctionDrawingBasis(drawcanvas, functionName, values) {
    "use strict";
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



export function getValueBasicContext(drawcanvas, valueName) {
    "use strict";
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


export function getFunctionBasicContext(drawcanvas, functionName, values) {
    "use strict";
    if (drawcanvas && drawcanvas.drawingBasis) {

        const context = drawcanvas.drawingBasis.context;

        if (Array.isArray(values)) {
            // Blazor "params object?[]? args" as values can be passed as an array of arrays. 
            // When the first element is an array substitute the first element as the array.
            if (Array.isArray(values[0])) {
                values = values[0];
            }

            // call the named webgl function
            switch (values.length) {
                case 0: //no parameters empty array
                    return context[functionName]();
                case 1:
                    return context[functionName](values[0]);
                case 2:
                    return context[functionName](values[0], values[1]);
                case 3:
                    return context[functionName](values[0], values[1], values[2]);
                case 4:
                    return context[functionName](values[0], values[1], values[2], values[3]);
                case 5:
                    return context[functionName](values[0], values[1], values[2], values[3], values[4]);
                case 6:
                    return context[functionName](values[0], values[1], values[2], values[3], values[4], values[5]);
                case 7:
                    return context[functionName](values[0], values[1], values[2], values[3], values[4], values[5], values[6]);
                default:
                    return context[functionName](values); // arrays longer than 7 terms have to be managed by the receiving function
            }
        }
        else {
            return context[functionName](values);
        }
    }
}



export function getFunctionDrawingBasis(drawcanvas, functionName, values) {
    "use strict";
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
                    return drawingBasis[functionName]();
                case 1:
                    return drawingBasis[functionName](values[0]);
                case 2:
                    return drawingBasis[functionName](values[0], values[1]);
                case 3:
                    return drawingBasis[functionName](values[0], values[1], values[2]);
                case 4:
                    return drawingBasis[functionName](values[0], values[1], values[2], values[3]);
                case 5:
                    return drawingBasis[functionName](values[0], values[1], values[2], values[3], values[4]);
                case 6:
                    return drawingBasis[functionName](values[0], values[1], values[2], values[3], values[4], values[5]);
                case 7:
                    return drawingBasis[functionName](values[0], values[1], values[2], values[3], values[4], values[5], values[6]);
                default:
                    return drawingBasis[functionName](values); // arrays longer than 7 terms have to be managed by the receiving function
            }
        } else {
            return drawingBasis[functionName](values);
        }
    }
    return null;
}



/// <summary>
/// DrawingBasis is a wrapper class to simplify use of the CanvasRenderingContext2D 
///
/// Remember everything in Javascript is executing in the client browser therefore 
/// all resources have to be "pushed" to the browser to be accessible to the
/// webgl context and rendering javascript.
/// </summary>



export class DrawingBasis {
    "use strict";
    constructor(canvasContext, isWebAssemblyClient, canvas) {
        // reference for GLContext
        this.context = canvasContext;
        // reference for .NetCalls  

        this.canvas = canvas;

        // is the rest of the Blazor page "in process" with the WebGL canvas?
        this.isWebAssemblyClient = isWebAssemblyClient;

        this.imageStorage = [];

        // we need to store information where the webgl methods can access the information
        this.pixelImageStorage = [];
        this.gradientStorage = [];
        this.patternStorage = [];
        this.transformStorage = [];
    }

    // oddly enough most methods will call directly on the context, however we will want to manage resources by name

    measureText(text) {
        const textMetrics = this.context.measureText(text);
        // convert to json object
        const result = {
            "width": textMetrics.width,
            "actualBoundingBoxAscent": textMetrics.actualBoundingBoxAscent,
            "actualBoundingBoxDescent": textMetrics.actualBoundingBoxDescent,
            "actualBoundingBoxLeft": textMetrics.actualBoundingBoxLeft,
            "actualBoundingBoxRight": textMetrics.actualBoundingBoxRight,
            "fontBoundingBoxAscent": textMetrics.fontBoundingBoxAscent,
            "fontBoundingBoxDescent": textMetrics.fontBoundingBoxDescent
        };
       

        return result;
    }


    // image processing
    createImageData(name, width, height) {
        this.pixelImageStorage[name] = this.context.createImageData(width, height);
    }

   
    // create an image and copy an large array of data, less than optimal
    createImageDataCopyByteArray(name, width, height, values) // r,g,b,a array
    {
        values = values[0];

        delete this.pixelImageStorage[name];

        // create a blank image, image data is always initially blank
        const imageData = this.context.createImageData(width, height);
        this.pixelImageStorage[name] = imageData;
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
                imageData.data[i] = binary_string.charCodeAt(i)[i];
            }
        }
    }

    loadImage(name, src) {
        const img = new Image();
        img.src = src;
        img.name = name;
        img.loadComplete = false;
        img.onload = function () {
            img.loadComplete = true;
        };
  
        this.imageStorage[name] = img;
    }

    loadPaintImage(name, src, x, y) {
        const img = new Image();
        img.src = src;
        img.name = name;
        img.loadComplete = false;
        this.imageStorage[name] = img;
        img.onload = function () {
            img.loadComplete = true;
            this.context.drawImage(img, x, y);
        };
        
        this.imageStorage[name] = img;
    }

    // grab an image from the current displayed canvas
    getImageData(name, x, y, width, height) {
        const image = this.context.getImageData(x, y, width, height);
        this.pixelImageStorage[name] = image;
    }


    // paint a whole image to the canvas
    putImageData(name, x, y) {
        this.context.putImageData(this.pixelImageStorage[name], x, y);
    }

    
    // paint part of the source image to the destination canvas
    putImageDataPartial(name, x, y, dx, dy, dWidth, dHeight) {
        this.context.putImageData(this.pixelImageStorage[name], x, y, dx, dy, dWidth, dHeight);
    }

    



    // webgl supports an assortment of functions for loading image files 

    setTransform(name) {
        this.context.setTransform(this.transformStorage[name]);
    }

    // patterns
    createPattern(name, imageName, repetition) {
        this.patternStorage[name] = this.context.createPattern(this.imageStorage[name], repetition);
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

