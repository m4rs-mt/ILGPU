// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                   Copyright (c) 2017 ILGPU Samples Project
//                                www.ilgpu.net
//  Based on a SharpDX Sample from https://github.com/sharpdx/SharpDX-Samples
//                   Copyright (c) 2015-2017 SharpDX Team
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Lightning;
using ILGPU.Runtime;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Device = SharpDX.Direct3D11.Device;

namespace ILGPU.SharpDX.SampleBuffer
{
    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector2 Position;
        public Vector2 TexCoord;

        public Vertex(float x, float y, float xTex, float yTex)
        {
            Position = new Vector2(x, y);
            TexCoord = new Vector2(xTex, yTex);
        }

        public Vertex Transform(float elapsedTime)
        {
            return new Vertex()
            {
                Position = Position * elapsedTime,
                TexCoord = TexCoord,
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RGBA
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public static RGBA operator *(RGBA first, float second)
        {
            return new RGBA()
            {
                R = first.R * second,
                G = first.G * second,
                B = first.B * second,
                A = first.A * second,
            };
        }

        public static RGBA operator +(RGBA first, RGBA second)
        {
            return new RGBA()
            {
                R = first.R + second.R,
                G = first.G + second.G,
                B = first.B + second.B,
                A = first.A + second.A,
            };
        }
    }

    static class Program
    {
        static void UpdateVertices(
            Index idx,
            ArrayView<Vertex> vertices,
            ArrayView<Vertex> refVertices,
            float elapsedTime)
        {
            vertices[idx] = refVertices[idx].Transform(elapsedTime);
        }

        static void UpdateTexture(
            Index2 idx,
            ArrayView2D<RGBA> colors,
            ArrayView2D<RGBA> refColors,
            ArrayView2D<RGBA> refColors2,
            float elapsedTime)
        {
            colors[idx] = (refColors[idx] * elapsedTime) + (refColors2[idx] * (1.0f - elapsedTime));
        }

        static void InitRandomTexture(DeviceContext context, DirectXTexture2D texture)
        {
            var random = new Random();
            var colors = new RGBA[texture.Length];
            for (uint x = 0; x < texture.Width; ++x)
            {
                for (uint y = 0; y < texture.Height; ++y)
                {
                    var idx = y * texture.Width + x;
                    colors[idx] = new RGBA()
                    {
                        R = random.NextFloat(0.0f, 1.0f),
                        G = random.NextFloat(0.0f, 1.0f),
                        B = random.NextFloat(0.0f, 1.0f),
                        A = 1.0f,
                    };
                }
            }
            GCHandle handle = GCHandle.Alloc(colors, GCHandleType.Pinned);

            try
            {
                var dataBox = new DataBox();
                dataBox.DataPointer = handle.AddrOfPinnedObject();
                dataBox.RowPitch = texture.Width * Marshal.SizeOf<RGBA>();
                dataBox.SlicePitch = dataBox.RowPitch * texture.Height;
                context.UpdateSubresource(dataBox, texture.Texture);
            }
            finally
            {
                handle.Free();
            }
        }

        [STAThread]
        static void Main()
        {
            var form = new RenderForm("Sample");
            var desc = new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription(
                    form.ClientSize.Width,
                    form.ClientSize.Height,
                    new Rational(60, 1),
                    Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = form.Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            Device device;
            SwapChain swapChain;
            Device.CreateWithSwapChain(
                DriverType.Hardware,
                DeviceCreationFlags.Debug,
                desc,
                out device,
                out swapChain);
            var context = device.ImmediateContext;

            var ilGPUContext = new Context();
            // Change to AcceleratorType.CPU for CPU mode
            var accelerator = device.CreateAssociatedAccelerator(ilGPUContext, AcceleratorType.Cuda);
            var lc = new LightningContext(accelerator);

            var vertexKernel = lc.LoadAutoGroupedStreamKernel<
                Index, ArrayView<Vertex>, ArrayView<Vertex>, float>(
                UpdateVertices);
            var textureKernel = lc.LoadAutoGroupedStreamKernel<
                Index2, ArrayView2D<RGBA>, ArrayView2D<RGBA>, ArrayView2D<RGBA>, float>(
                UpdateTexture);

            var dxInteropAccelerator = accelerator.CreateDirectXInteropAccelerator(device);
            var vertexBuffer = dxInteropAccelerator.CreateBuffer<Vertex>(6, DirectXBufferFlags.None);
            var referenceBuffer = dxInteropAccelerator.CreateBuffer<Vertex>(6, DirectXBufferFlags.None);

            var colorTexture = dxInteropAccelerator.CreateTexture2D(640, 480, Format.R32G32B32A32_Float);
            var referenceColorTexture = dxInteropAccelerator.CreateTexture2D(colorTexture.Width, colorTexture.Height, colorTexture.Format);
            var referenceColorTexture2 = dxInteropAccelerator.CreateTexture2D(colorTexture.Width, colorTexture.Height, colorTexture.Format);

            var vertexShader = new VertexShader(device, ShaderBytecode.CompileFromFile("Sample.hlsl", "VSMain", "vs_5_0"));
            var pixelShader = new PixelShader(device, ShaderBytecode.CompileFromFile("Sample.hlsl", "PSMain", "ps_5_0"));

            var signature = ShaderSignature.GetInputSignature(ShaderBytecode.CompileFromFile("Sample.hlsl", "VSMain", "vs_5_0"));

            var layout = new InputLayout(device, signature, new[]
            {
                new InputElement("Position", 0, Format.R32G32_Float, 0, 0),
                new InputElement("TexCoord", 0, Format.R32G32_Float, 8, 0)
            });

            // Setup reference buffer
            context.UpdateSubresource(
                new Vertex[]
                {
                    new Vertex(-1.0f, -1.0f, 0.0f, 0.0f),
                    new Vertex(1.0f, 1.0f, 1.0f, 1.0f),
                    new Vertex(1.0f, -1.0f, 1.0f, 0.0f),

                    new Vertex(-1.0f, 1.0f, 0.0f, 1.0f),
                    new Vertex(1.0f, 1.0f, 1.0f, 1.0f),
                    new Vertex(-1.0f, -1.0f, 0.0f, 0.0f),
                }, referenceBuffer.Buffer);

            // Setup reference colors
            InitRandomTexture(context, referenceColorTexture);
            InitRandomTexture(context, referenceColorTexture2);

            SamplerState sampler = new SamplerState(device, SamplerStateDescription.Default());

            context.InputAssembler.InputLayout = layout;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.VertexShader.SetShaderResource(0, vertexBuffer.ResourceView);
            context.PixelShader.SetShaderResource(1, colorTexture.ResourceView);
            context.PixelShader.SetSampler(0, sampler);
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);

            var clock = new Stopwatch();
            clock.Start();

            // Declare texture for rendering
            bool userResized = true;
            Texture2D backBuffer = null;
            RenderTargetView renderView = null;
            Texture2D depthBuffer = null;
            DepthStencilView depthView = null;

            // Setup handler on resize form
            form.UserResized += (sender, args) => userResized = true;

            // Main loop
            RenderLoop.Run(form, () =>
            {
                // If Form resized
                if (userResized)
                {
                    // Dispose all previous allocated resources
                    Utilities.Dispose(ref backBuffer);
                    Utilities.Dispose(ref renderView);
                    Utilities.Dispose(ref depthBuffer);
                    Utilities.Dispose(ref depthView);

                    // Resize the backbuffer
                    swapChain.ResizeBuffers(desc.BufferCount, form.ClientSize.Width, form.ClientSize.Height, Format.Unknown, SwapChainFlags.None);

                    // Get the backbuffer from the swapchain
                    backBuffer = Texture2D.FromSwapChain<Texture2D>(swapChain, 0);

                    // Renderview on the backbuffer
                    renderView = new RenderTargetView(device, backBuffer);

                    // Create the depth buffer
                    depthBuffer = new Texture2D(device, new Texture2DDescription()
                    {
                        Format = Format.D32_Float_S8X24_UInt,
                        ArraySize = 1,
                        MipLevels = 1,
                        Width = form.ClientSize.Width,
                        Height = form.ClientSize.Height,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.DepthStencil,
                        CpuAccessFlags = CpuAccessFlags.None,
                        OptionFlags = ResourceOptionFlags.None
                    });

                    // Create the depth buffer view
                    depthView = new DepthStencilView(device, depthBuffer);

                    // Setup targets and viewport for rendering
                    context.Rasterizer.SetViewport(new Viewport(0, 0, form.ClientSize.Width, form.ClientSize.Height, 0.0f, 1.0f));
                    context.OutputMerger.SetTargets(depthView, renderView);

                    // We are done resizing
                    userResized = false;
                }

                var time = clock.ElapsedMilliseconds / 1000.0f;

                using (var mapping = dxInteropAccelerator.MapBuffers(
                    context,
                    vertexBuffer,
                    referenceBuffer,
                    colorTexture,
                    referenceColorTexture,
                    referenceColorTexture2))
                {
                    var vertexFactor = 0.5f * (1.0f + GPUMath.Sin(GPUMath.PI * time / 5.0f));
                    vertexKernel(
                        vertexBuffer.Length,
                        vertexBuffer.View,
                        referenceBuffer.View,
                        vertexFactor);

                    var blendFactor = 0.5f * (1.0f + GPUMath.Sin(2.0f * GPUMath.PI * time));
                    textureKernel(
                        colorTexture.Size,
                        colorTexture.GetView<RGBA>(),
                        referenceColorTexture.GetView<RGBA>(),
                        referenceColorTexture2.GetView<RGBA>(),
                        blendFactor);

                    accelerator.Synchronize();
                }

                context.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                context.ClearRenderTargetView(renderView, Color.White);

                context.Draw(6, 0);
                swapChain.Present(0, PresentFlags.None);
            });

            Utilities.Dispose(ref vertexBuffer);
            Utilities.Dispose(ref referenceBuffer);
            Utilities.Dispose(ref colorTexture);
            Utilities.Dispose(ref colorTexture);
            Utilities.Dispose(ref colorTexture);
            Utilities.Dispose(ref referenceColorTexture);
            Utilities.Dispose(ref referenceColorTexture2);

            Utilities.Dispose(ref dxInteropAccelerator);
            Utilities.Dispose(ref lc);
            Utilities.Dispose(ref accelerator);
            Utilities.Dispose(ref ilGPUContext);
        }
    }
}
