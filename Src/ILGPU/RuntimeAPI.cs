// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: RuntimeAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// An abstract runtime API that can be used in combination with the dynamic DLL
    /// loader functionality of the class <see cref="RuntimeSystem"/>.
    /// </summary>
    public abstract class RuntimeAPI
    {
        /// <summary>
        /// Loads a runtime API that is implemented via compile-time known classes.
        /// </summary>
        /// <typeparam name="T">The abstract class type to implement.</typeparam>
        /// <typeparam name="TWindows">The Windows implementation.</typeparam>
        /// <typeparam name="TLinux">The Linux implementation.</typeparam>
        /// <typeparam name="TMacOS">The MacOS implementation.</typeparam>
        /// <typeparam name="TNotSupported">The not-supported implementation.</typeparam>
        /// <returns>The loaded runtime API.</returns>
        internal static T LoadRuntimeAPI<
            T,
            TWindows,
            TLinux,
            TMacOS,
            TNotSupported>()
            where T : RuntimeAPI
            where TWindows : T, new()
            where TLinux : T, new()
            where TMacOS : T, new()
            where TNotSupported : T, new()
        {
            if (!Backends.Backend.RunningOnNativePlatform)
                return new TNotSupported();
            try
            {
                T instance =
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? new TLinux()
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? new TMacOS() as T
                    : new TWindows();
                // Try to initialize the new interface
                if (!instance.Init())
                    instance = new TNotSupported();
                return instance;
            }
            catch (Exception ex) when (
                ex is DllNotFoundException ||
                ex is EntryPointNotFoundException)
            {
                // In case of a critical initialization exception fall back to the
                // not supported API
                return new TNotSupported();
            }
        }

        /// <summary>
        /// Returns true if the runtime API instance is supported on this platform.
        /// </summary>
        public abstract bool IsSupported { get; }

        /// <summary>
        /// Initializes the runtime API implementation.
        /// </summary>
        /// <returns>
        /// True, if the API instance could be initialized successfully.
        /// </returns>
        public abstract bool Init();
    }
}
