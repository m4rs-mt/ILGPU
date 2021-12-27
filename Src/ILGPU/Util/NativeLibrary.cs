// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
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
    ///
    /// System.Runtime.InteropServices.NativeLibrary is not available for
    /// NETFRAMEWORK or NETSTANDARD.
    /// </summary>
#if NET5_0_OR_GREATER
    static class NativeLibrary
    {
        public static IntPtr Load(string path) =>
            System.Runtime.InteropServices.NativeLibrary.Load(path);

        public static void Free(IntPtr handle) =>
            System.Runtime.InteropServices.NativeLibrary.Free(handle);

        public static IntPtr GetExport(IntPtr handle, string name) =>
            System.Runtime.InteropServices.NativeLibrary.GetExport(handle, name);
    }
#else
    static class NativeLibrary
    {
        public static IntPtr Load(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return NativeLibrary_Windows.LoadLibrary(path);
            else
                return NativeLibrary_Unix.dlopen(path, NativeLibrary_Unix.RTLD_NOW);
        }

        public static void Free(IntPtr handle)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                NativeLibrary_Windows.FreeLibrary(handle);
            else
                _ = NativeLibrary_Unix.dlclose(handle);
        }

        public static IntPtr GetExport(IntPtr handle, string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return NativeLibrary_Windows.GetProcAddress(handle, name);
            else
                return NativeLibrary_Unix.dlsym(handle, name);
        }
    }

    static class NativeLibrary_Windows
    {
        private const string LibName = "kernel32.dll";

        [DllImport(
            LibName,
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true,
            SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport(
            LibName,
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true,
            SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static extern IntPtr GetProcAddress(
            IntPtr hModule,
            string procedureName);

        [DllImport(LibName, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static extern bool FreeLibrary(IntPtr hModule);
    }

    static class NativeLibrary_Unix
    {
        private const string LibName = "libdl";

        public const int RTLD_NOW = 0x002;

        [DllImport(
            LibName,
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static extern IntPtr dlopen(string dllToLoad, int flags);

        [DllImport(
            LibName,
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static extern IntPtr dlsym(IntPtr hModule, string procedureName);

        [DllImport(LibName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static extern int dlclose(IntPtr hModule);
    }
#endif
}

#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
