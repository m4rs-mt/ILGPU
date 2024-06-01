// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2021-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;
using System.Globalization;

namespace CudaIPC.Child
{
    class Program
    {
        /// <summary>
        /// A simple kernel writing the index to the data view.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void SimpleKernel(
            Index1D index,
            ArrayView<int> dataView)
        {
            dataView[index] = index;
        }

        /// <summary>
        /// Accepts a cuda device id, an ipc memory handle as hexstring and its length as arguments.
        /// It then maps that memory and executes a simple kernel on it.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("There should be 3 arguments:");
                Console.WriteLine("<device id> <ipc memory handle> <length>");
                return;
            }

            // Parse arguments
            int deviceId = int.Parse(args[0], CultureInfo.InvariantCulture);
            CudaIpcMemHandle ipcMemHandle = new CudaIpcMemHandle(Convert.FromHexString(args[1]));
            int length = int.Parse(args[2], CultureInfo.InvariantCulture);

            // Set up the correct accelerator
            using Context context = Context.CreateDefault();
            CudaDevice device = context.GetCudaDevice(deviceId);
            using CudaAccelerator accelerator = device.CreateCudaAccelerator(context);
            // device.PrintInformation();

            // Map exported memory
            MemoryBuffer cudaIpcMemoryBuffer =
                accelerator.MapFromIpcMemHandle(ipcMemHandle, length, sizeof(int), CudaIpcMemFlags.LazyEnablePeerAccess);
            ArrayView<int> arrayView = cudaIpcMemoryBuffer.AsArrayView<int>(0, length);

            // load and execute kernel
            Action<Index1D, ArrayView<int>> loadedSimpleKernel =
                accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(
                    SimpleKernel);
            loadedSimpleKernel(arrayView.IntExtent, arrayView);
        }
    }
}
