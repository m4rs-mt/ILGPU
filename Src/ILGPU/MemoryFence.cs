// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MemoryFence.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


using ILGPU.Intrinsic;

namespace ILGPU;

/// <summary>
/// Contains memory-fence functions.
/// </summary>
public static class MemoryFence
{
    /// <summary>
    /// A memory fence at the group level.
    /// </summary>
    [MemoryFenceIntrinsic]
    public static void GroupLevel() => throw new InvalidKernelOperationException();

    /// <summary>
    /// A memory fence at the device level.
    /// </summary>
    [MemoryFenceIntrinsic]
    public static void DeviceLevel() => throw new InvalidKernelOperationException();

    /// <summary>
    /// A memory fence at the system level.
    /// </summary>
    [MemoryFenceIntrinsic]
    public static void SystemLevel() => throw new InvalidKernelOperationException();
}
