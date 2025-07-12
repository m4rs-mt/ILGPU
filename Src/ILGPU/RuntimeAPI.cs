// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: RuntimeAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace ILGPU;

/// <summary>
/// Represents a target platform.
/// </summary>
public enum TargetPlatform
{
    /// <summary>
    /// The target platform is 32-bit.
    /// </summary>
    Platform32Bit,

    /// <summary>
    /// The target platform is 64-bit.
    /// </summary>
    Platform64Bit,
}

/// <summary>
/// Extension methods for TargetPlatform related objects.
/// </summary>
public static class TargetPlatformExtensions
{
    /// <summary>
    /// Returns true if the current runtime platform is 64-bit.
    /// </summary>
    public static bool Is64Bit(this TargetPlatform targetPlatform) =>
        targetPlatform == TargetPlatform.Platform64Bit;
}

/// <summary>
/// An abstract runtime API that can be used in combination with the dynamic DLL
/// loader functionality.
/// </summary>
public abstract class RuntimeAPI
{
    /// <summary>
    /// Returns the current execution platform.
    /// </summary>
    public static TargetPlatform RuntimePlatform =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => TargetPlatform.Platform32Bit,
            Architecture.X64 => TargetPlatform.Platform64Bit,
            Architecture.Arm => TargetPlatform.Platform32Bit,
            Architecture.Arm64 => TargetPlatform.Platform64Bit,
            Architecture.Wasm => TargetPlatform.Platform64Bit,
            _ => throw new NotSupportedException(),
        };

    /// <summary>
    /// Returns the native OS platform.
    /// </summary>
    public static TargetPlatform OSTargetPlatform =>
        RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => TargetPlatform.Platform32Bit,
            Architecture.X64 => TargetPlatform.Platform64Bit,
            Architecture.Arm => TargetPlatform.Platform32Bit,
            Architecture.Arm64 => TargetPlatform.Platform64Bit,
            Architecture.Wasm => TargetPlatform.Platform64Bit,
            _ => throw new NotSupportedException(),
        };

    /// <summary>
    /// Returns true if the current runtime platform is equal to the OS platform.
    /// </summary>
    public static bool RunningOnNativePlatform => RuntimePlatform == OSTargetPlatform;

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
        if (!RunningOnNativePlatform)
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
