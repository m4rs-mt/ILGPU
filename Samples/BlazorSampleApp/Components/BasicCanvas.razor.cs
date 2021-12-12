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
    public partial class BasicCanvas : ComponentBase, IAsyncDisposable
    {
#nullable disable
        private IJSObjectReference asyncModule = null;

        private IJSInProcessObjectReference module = null;

        protected IJSRuntime _jsRuntime;

        protected IJSInProcessRuntime _jsInProcessRuntime = null;

        public event Action<BasicCanvas> CanvasInitComplete = null;

#nullable enable


        [Parameter]
        public bool IsTransparent { get; set; } = false;

        [Parameter]
        public bool IsDesyncronized { get; set; } = false;

        [Parameter]
        public int Height { get; set; } = 600;

        [Parameter]
        public int Width { get; set; } = 800;

        [Parameter]
        public bool IsFullScreen { get; set; } = false;


        [Parameter]
        public string CanvasId { get; set; } = Guid.NewGuid().ToString();

        protected ElementReference _canvasRef;

        public ElementReference CanvasReference => this._canvasRef;

        public bool IsWebAssembley { get { return (_jsRuntime is IJSInProcessRuntime); } }

        public bool IsDisposing { get; set; } = false;


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

                if (IsWebAssembley)
                {
                    _jsInProcessRuntime = (IJSInProcessRuntime)value;

                }
            }
        }





        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {

                try
                {
                    if (IsWebAssembley)
                    {

                        module = await _jsInProcessRuntime.InvokeAsync<IJSInProcessObjectReference>("import", "./Scripts/BasicCanvas.js");

                        module.InvokeVoid("initializeBasicCanvas", CanvasId, IsWebAssembley, IsTransparent, IsDesyncronized);
                    }
                    else
                    {
                        asyncModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Scripts/BasicCanvas.js");

                        await asyncModule.InvokeVoidAsync("initializeBasicCanvas", CanvasId, IsWebAssembley, IsTransparent, IsDesyncronized);

                    }

                    if (CanvasInitComplete != null)
                        CanvasInitComplete(this);

                }
                catch (Exception ex)
                {
                    var crap = ex.Message;
                }

            }

        }

        public async ValueTask DisposeAsync()
        {
            IsDisposing = true;

            if (asyncModule != null)
            {
                await asyncModule.DisposeAsync();
            }
            module?.Dispose();
        }

        public async ValueTask InjectScript(string scriptText)
        {
            if (module != null)
            {
                module.InvokeVoid("InjectScript", scriptText);
            }
            else
            {
                await asyncModule.InvokeVoidAsync("InjectScript", scriptText);
            }
        }

        public async ValueTask SetFunction(string functionName, string functionText)
        {
            if (module != null)
            {
                module.InvokeVoid("InjectFunction", CanvasReference, functionName, functionText);
            }
            else
            {
                await asyncModule.InvokeVoidAsync("InjectFunction", CanvasReference, functionName, functionText);
            }
        }

        public async ValueTask SetGlobalFunction(string globalFunctionName, params object?[]? args)
        {
            if (module != null)
            {
                module.InvokeVoid(globalFunctionName, CanvasReference, args);
            }
            else
            {
                await asyncModule.InvokeVoidAsync(globalFunctionName, CanvasReference, args);
            }
        }

        public async ValueTask<T> GetGlobalFunction<T>(string globalFunctionName, params object?[]? args)
        {
            if (module != null)
            {
                return module.Invoke<T>(globalFunctionName, CanvasReference, args);
            }
            else
            {
                return await asyncModule.InvokeAsync<T>(globalFunctionName, CanvasReference, args);
            }
        }


        public async ValueTask SetValueBasicContext(string ValueName, params object?[]? args)
        {
            if (module != null)
            {
                module.InvokeVoid("setValueBasicContext", CanvasReference, ValueName, args);
            }
            else
            {
                await asyncModule.InvokeVoidAsync("setValueBasicContext", CanvasReference, ValueName, args);
            }
        }


        public async ValueTask<T> GetValueBasicContext<T>(string ValueName, params object?[]? args)
        {
            if (module != null)
            {
                return module.Invoke<T>("getValueBasicContext", CanvasReference, ValueName, args);
            }
            else
            {
                return await asyncModule.InvokeAsync<T>("getValueBasicContext", CanvasReference, ValueName, args);
            }
        }



        public async ValueTask SetFunctionBasicContext(string FunctionName, params object?[]? args)
        {
            if (module != null)
            {
                module.InvokeVoid("setFunctionBasicContext", CanvasReference, FunctionName, args);
            }
            else
            {
                await asyncModule.InvokeVoidAsync("setFunctionBasicContext", CanvasReference, FunctionName, args);
            }
        }


        public async ValueTask<T> GetFunctionBasicContext<T>(string FunctionName, params object?[]? args)
        {
            if (module != null)
            {
                return module.Invoke<T>("getFunctionBasicContext", CanvasReference, FunctionName, args);
            }
            else
            {
                return await asyncModule.InvokeAsync<T>("getFunctionBasicContext", CanvasReference, FunctionName, args);
            }
        }


        public async ValueTask SetFunctionDrawingBasis(string FunctionName, params object?[]? args)
        {
            if (module != null)
            {
                module.InvokeVoid("setFunctionDrawingBasis", CanvasReference, FunctionName, args);
            }
            else
            {
                await asyncModule.InvokeVoidAsync("setFunctionDrawingBasis", CanvasReference, FunctionName, args);
            }
        }


        public async ValueTask<T> GetFunctionDrawingBasis<T>(string FunctionName, params object?[]? args)
        {
            if (module != null)
            {
                return module.Invoke<T>("getFunctionDrawingBasis", CanvasReference, FunctionName, args);
            }
            else
            {
                return await asyncModule.InvokeAsync<T>("getFunctionDrawingBasis", CanvasReference, FunctionName, args);
            }
        }

    }
}
