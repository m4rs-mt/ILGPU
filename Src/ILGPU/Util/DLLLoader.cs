// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: DLLLoader.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ILGPU.Util
{
    /// <summary>
    /// A helper class for injecting native libraries from the X64 or the X86 lib folder.
    /// </summary>
    public static class DLLLoader
    {
#if WIN
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "This is a custom DLL-loading wrapper and not a general collection of native methods")]
        [DllImport("kernel32", BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr), In] string fileName);

        /// <summary>
        /// Loads the given library into the current process.
        /// </summary>
        /// <param name="libName">The library name.</param>
        /// <returns>True, iff the library was loaded successfully.</returns>
        public static bool LoadLib(string libName)
        {
            var rootDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string llvmLibPath;
            if (Environment.Is64BitProcess)
                llvmLibPath = Path.Combine(rootDir, "X64", libName);
            else
                llvmLibPath = Path.Combine(rootDir, "X86", libName);
            return LoadLibrary(llvmLibPath) != IntPtr.Zero;
        }
#else
        public static bool LoadLib(string libName)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
