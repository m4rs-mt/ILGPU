// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Form.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Mandelbrot
{
    public partial class Form : System.Windows.Forms.Form
    {
        public Form()
        {
            InitializeComponent();
            Mandelbrot.CompileKernel(false);
        }


        private void Draw(int[] data, int width, int height, int iterations, Color color)
        {
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < width * height; i++)
            {
                int x = i % width;
                int y = i / width;
                if (data[i] == iterations)
                    bmp.SetPixel(x, y, color);
                else
                    bmp.SetPixel(x, y, Color.FromArgb((int)(4000000000 / ((data[i] < 1) ? 1 : data[i]))));
            }
            pictureBox1.Image = bmp;
        }


        private void mandelbrot_CB(object sender, EventArgs e)
        {
            int width = pictureBox1.Width;
            int height = pictureBox1.Height;
            int iterations = 1000;
            int[] data = new int[width * height];

            Utils.InitWatch();
            Mandelbrot.CalcCPU(data, width, height, iterations); // Single thread CPU
            Utils.PrintElapsedTime("CPU Mandelbrot");
            Draw(data, width, height, iterations, Color.Blue);

            Mandelbrot.Dispose();
            Mandelbrot.CompileKernel(false);
            Utils.InitWatch();
            Mandelbrot.CalcGPU(data, width, height, iterations); // ILGPU-CPU-Mode
            Utils.PrintElapsedTime("ILGPU-CPU Mandelbrot");
            Draw(data, width, height, iterations, Color.Black);

            Mandelbrot.Dispose();
            Mandelbrot.CompileKernel(true);
            Utils.InitWatch();
            Mandelbrot.CalcGPU(data, width, height, iterations); // ILGPU-GPU-Mode
            Utils.PrintElapsedTime("ILGPU-CUDA Mandelbrot");
            Draw(data, width, height, iterations, Color.Red);
        }


        private void formClosing_CB(object sender, FormClosingEventArgs e)
        {
            Mandelbrot.Dispose();
        }

    }
}
