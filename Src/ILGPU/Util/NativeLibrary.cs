// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: NativeLibrary.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

namespace ILGPU.Util
{
    /// <summary>
    /// Provides cross-platform functions for loading a DLL and extracting function
    /// pointers.
    /// </summary>
    static class NativeLibrary
    {
        public static IntPtr Load(string path) =>
            System.Runtime.InteropServices.NativeLibrary.Load(path);

        public static void Free(IntPtr handle) =>
            System.Runtime.InteropServices.NativeLibrary.Free(handle);

        public static IntPtr GetExport(IntPtr handle, string name) =>
            System.Runtime.InteropServices.NativeLibrary.GetExport(handle, name);
    }
}

#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
