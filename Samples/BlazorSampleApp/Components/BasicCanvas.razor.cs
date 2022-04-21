
// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicCanvas.razor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorSampleApp.Components
{

#nullable disable
    public partial class BasicCanvas : ComponentBase, IAsyncDisposable
    {

        private IJSObjectReference asyncModule = null;

        private IJSInProcessObjectReference module = null;

        protected IJSRuntime _jsRuntime;

        protected IJSInProcessRuntime _jsInProcessRuntime = null;

        public event Action<BasicCanvas> CanvasInitComplete = null;




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

        private bool IsDisposing = false;

        [Inject]
        public IJSRuntime JS_Runtime
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
                        asyncModule = await JS_Runtime.InvokeAsync<IJSObjectReference>("import", "./Scripts/BasicCanvas.js");

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
            if (IsDisposing) return;

            if (module != null)
            {
                module.InvokeVoid("InjectScript", scriptText);
            }
            else
            {
                await asyncModule.InvokeVoidAsync("InjectScript", scriptText);
            }
        }

#nullable enable       
        public async ValueTask SetValueBasicContext(string ValueName, params object?[]? args)
        {
#nullable disable
            if (IsDisposing) return;

            if (module != null)
            {
                module.InvokeVoid("setValueBasicContext", CanvasReference, ValueName, args);
            }
            else
            {                
                await asyncModule.InvokeVoidAsync("setValueBasicContext", CanvasReference, ValueName, args);
            }
        }


#nullable enable
        public async ValueTask<T> GetValueBasicContext<T>(string ValueName, params object?[]? args)
        {
#nullable disable
            if (IsDisposing) return default(T);

            if (module != null)
            {
                return module.Invoke<T>("getValueBasicContext", CanvasReference, ValueName, args);
            }
            else
            {
                return await asyncModule.InvokeAsync<T>("getValueBasicContext", CanvasReference, ValueName, args);
            }
        }


#nullable enable 
        public async ValueTask SetFunctionBasicContext(string FunctionName, params object?[]? args)
        {
#nullable disable
            if (IsDisposing) return;

            if (module != null)
            {
                module.InvokeVoid("setFunctionBasicContext", CanvasReference, FunctionName, args);
            }
            else
            {
                await asyncModule.InvokeVoidAsync("setFunctionBasicContext", CanvasReference, FunctionName, args);
            }
        }

#nullable enable 
        public async ValueTask<T> GetFunctionBasicContext<T>(string FunctionName, params object?[]? args)
        {
#nullable disable
            if (IsDisposing) return default(T);

            if (module != null)
            {
                return module.Invoke<T>("getFunctionBasicContext", CanvasReference, FunctionName, args);
            }
            else
            {
                return await asyncModule.InvokeAsync<T>("getFunctionBasicContext", CanvasReference, FunctionName, args);
            }
        }

#nullable enable 
        public async ValueTask SetFunctionDrawingBasis(string FunctionName, params object?[]? args)
        {
#nullable disable
            if (IsDisposing) return;

            if (module != null)
            {
                module.InvokeVoid("setFunctionDrawingBasis", CanvasReference, FunctionName, args);
            }
            else
            {
                await asyncModule.InvokeVoidAsync("setFunctionDrawingBasis", CanvasReference, FunctionName, args);
            }
        }

#nullable enable 
        public async ValueTask<T> GetFunctionDrawingBasis<T>(string FunctionName, params object?[]? args)
        {
#nullable disable
            if (IsDisposing) return default(T);

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
