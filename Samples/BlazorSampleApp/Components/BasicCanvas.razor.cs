// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2021 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: BasicCanvas.razor.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------



using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorSampleApp.Components
{
    /// <summary>
    /// This wraps up a HTML/webgl canvas for 2D webgl rendering from Blazor.
    /// 
    /// Note server side Blazor maintains a link between the client web browser
    /// and the webserver, this could be a 100 milisecond delay or more. Therefore
    /// one may have to use javascript to make a page appear performant.
    /// </summary>
    public partial class BasicCanvas
    {

        [Parameter]
        public bool IsRenderedFromServer { get; set; } = false;

        [Parameter]
        public bool IsTransparent { get; set; } = false;


        [Parameter]
        public int Height { get; set; } = 600;

        [Parameter]
        public int Width { get; set; } = 800;

       

        //unique canvas id
        [Parameter]
        public string CanvasId { get; set; } = Guid.NewGuid().ToString();

        protected ElementReference _canvasRef;
        public ElementReference CanvasReference => this._canvasRef;

        private IJSRuntime _jsRuntime;

        [Inject]
        public IJSRuntime JsRuntime
        {
            get
            {
                return _jsRuntime;
            }
            set
            {
                _jsRuntime = value;

                // Is this a web assembly application?
                if (value is IJSInProcessRuntime)
                {
                    FastJSRuntime = (IJSInProcessRuntime)value;
                }
            }
        }

        // Only used in Blazor WebAssembly applications.
        public IJSInProcessRuntime FastJSRuntime { get; set; } = null;


        public event Action<BasicCanvas> CanvasInitComplete;

        /// <summary>
        /// Once the razor page is rendered we need to link the canvas to give access to Blazor C#
        /// </summary>
        /// <param name="firstRender"></param>
        /// <returns></returns>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {

            if (firstRender)
            {

                try
                {
                    // Are we running on WebAssembly?
                    if (JsRuntime is IJSInProcessRuntime)
                    {
                        // then await is not needed
                        FastJSRuntime = (IJSInProcessRuntime)JsRuntime;
                        FastJSRuntime.InvokeVoid("initializeBasicCanvas", CanvasId, true, IsTransparent);

                    }
                    else
                    {
                        await JsRuntime.InvokeVoidAsync("initializeBasicCanvas", CanvasId, false, IsTransparent);
                    }

                    if (CanvasInitComplete != null)
                        CanvasInitComplete(this);

                }
                catch (Exception ex)
                {
                    var crap = ex.Message;
                }

                await base.OnAfterRenderAsync(firstRender);

            }
        }

#nullable enable   /// Allow nullable reference types




        /// for performance reasons we will often have separate javascripts we may need to call from blazor
        public async Task SetGlobalFunction(string globalFunctionName, params object?[]? args)
        {
            // Are we running on WebAssembly?
            if (FastJSRuntime != null)
            {
                FastJSRuntime.InvokeVoid(globalFunctionName, CanvasReference, args);
            }
            else
            {
#pragma warning disable CS8604 // Possible null reference argument.
                await JsRuntime.InvokeVoidAsync(globalFunctionName, CanvasReference, args);
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        /// <summary>
        /// We want to return a value/object from the webgl drawing context
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ValueName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task<T> GetValue<T>(string ValueName, params object?[]? args)
        {
            // Are we running on WebAssembly?
            if (FastJSRuntime != null)
            {
                return FastJSRuntime.Invoke<T>("getValueBasicCanvas", CanvasReference, ValueName, args);
            }
            else
            {
#pragma warning disable CS8604 // Possible null reference argument.
                return await JsRuntime.InvokeAsync<T>("getValueBasicCanvas", CanvasReference, ValueName, args);
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        /// <summary>
        /// Used to set a value on the webgl 2d drawing context associated with our canvas.
        /// </summary>
        /// <param name="ValueName"></param>
        /// <param name="args"></param>
        /// <returns></returns>

        public async Task SetValue(string ValueName, params object?[]? args)
        {
            // are we running on webassembly?
            if (FastJSRuntime != null)
            {
                FastJSRuntime.InvokeVoid("setValueBasicCanvas", CanvasReference, ValueName, args);
            }
            else
            {
#pragma warning disable CS8604 // Possible null reference argument.
                await JsRuntime.InvokeVoidAsync("setValueBasicContext", CanvasReference, ValueName, args);
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        /// <summary>
        /// Used to call a function on the webgl 2d drawing context associated with our canvas.
        /// </summary>
        /// <param name="FunctionName"></param>
        /// <param name="args"></param>
        /// <returns></returns>

        public async Task SetFunction(string FunctionName, params object?[]? args)
        {
            // Are we running on WebAssembly?
            if (FastJSRuntime != null)
            {
                FastJSRuntime.InvokeVoid("setFunctionBasicCanvas", CanvasReference, FunctionName, args);
            }
            else
            {
#pragma warning disable CS8604 // Possible null reference argument.
                await JsRuntime.InvokeVoidAsync("setFunctionBasicContext", CanvasReference, FunctionName, args);
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }

        /// <summary>
        /// Used to call a function on the DrawingBasis object associated with our canvas.
        /// 
        /// Some advanced functions require JavaScript support objects accessible to webgl context. 
        /// </summary>
        /// <param name="FunctionName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task SetFunctionDrawingBasis(string FunctionName, params object?[]? args)
        {
            // Are we running on WebAssembly?
            if (FastJSRuntime != null)
            {
                FastJSRuntime.InvokeVoid("setFunctionDrawingBasis", CanvasReference, FunctionName, args);
            }
            else
            {
#pragma warning disable CS8604 // Possible null reference argument.
                await JsRuntime.InvokeVoidAsync("setFunctionDrawingBasis", CanvasReference, FunctionName, args);
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }


    }
}
