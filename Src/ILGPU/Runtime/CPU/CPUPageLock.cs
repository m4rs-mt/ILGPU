// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUPageLock.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes

namespace ILGPU.Runtime.CPU;

/// <summary>
/// Generic functions for locking and unlocking pages on all supported operating systems.
/// </summary>
public static class CPUPageLock
{
    #region Unsafe Imports

    [DllImport("kernel32.dll", SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private static extern bool VirtualLock(IntPtr lpAddress, nuint dwSize);


    [DllImport("kernel32.dll", SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private static extern bool VirtualUnlock(IntPtr lpAddress, nuint dwSize);

    [DllImport("libc", SetLastError = true)]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static extern int mlock(IntPtr addr, nuint len);

    [DllImport("libc", SetLastError = true)]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static extern int munlock(IntPtr addr, nuint len);

    /// <summary>
    /// Throws an invalid page locking exception.
    /// </summary>
    private static void ThrowInvalidPageLockException() =>
        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

    #endregion

    /// <summary>
    /// Locks the given memory pointer in memory space.
    /// </summary>
    /// <param name="ptr">The ptr to lock.</param>
    /// <param name="lengthInBytes">The number of bytes to lock.</param>
    /// <exception cref="PlatformNotSupportedException">
    /// Is thrown in case of an unknown operating system.
    /// </exception>
    public static unsafe void Lock(IntPtr ptr, nuint lengthInBytes)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!VirtualLock(ptr, lengthInBytes))
                ThrowInvalidPageLockException();
            return;
        }
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            if (mlock(ptr, lengthInBytes) != 0)
                ThrowInvalidPageLockException();
            return;
        }
        throw new PlatformNotSupportedException();
    }

    /// <summary>
    /// Unlocks the given memory pointer in memory space.
    /// </summary>
    /// <param name="ptr">The ptr to lock.</param>
    /// <param name="lengthInBytes">The number of bytes to lock.</param>
    /// <exception cref="PlatformNotSupportedException">
    /// Is thrown in case of an unknown operating system.
    /// </exception>
    public static unsafe void Unlock(IntPtr ptr, nuint lengthInBytes)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!VirtualUnlock(ptr, lengthInBytes))
                ThrowInvalidPageLockException();
            return;
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            if (munlock(ptr, lengthInBytes) != 0)
                ThrowInvalidPageLockException();
            return;
        }
        throw new PlatformNotSupportedException();
    }

}

/// <summary>
/// Represents the scope/duration of a page lock in generic CPU space.
/// </summary>
sealed class CPUPageLockScope<T> : PageLockScope<T> where T : unmanaged
{
    /// <summary>
    /// Constructs a page lock scope for the accelerator.
    /// </summary>
    /// <param name="addrOfLockedObject">The address of page locked object.</param>
    /// <param name="numElements">The number of elements.</param>
    public CPUPageLockScope(IntPtr addrOfLockedObject, long numElements)
        : base(accelerator: null, addrOfLockedObject, numElements)
    {
        CPUPageLock.Lock(addrOfLockedObject, (nuint)LengthInBytes);
    }

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        CPUPageLock.Unlock(AddrOfLockedObject, (nuint)LengthInBytes);
        base.DisposeAcceleratorObject(disposing);
    }
}

#pragma warning restore CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
