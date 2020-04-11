// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MemoryFence.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using System.Threading;

namespace ILGPU
{
    /// <summary>
    /// Contains memory-fence functions.
    /// </summary>
    public static class MemoryFence
    {
        /// <summary>
        /// A memory fence at the group level.
        /// </summary>
        [MemoryBarrierIntrinsic(MemoryBarrierKind.GroupLevel)]
        public static void GroupLevel() => Interlocked.MemoryBarrier();

        /// <summary>
        /// A memory fence at the device level.
        /// </summary>
        [MemoryBarrierIntrinsic(MemoryBarrierKind.DeviceLevel)]
        public static void DeviceLevel() => Interlocked.MemoryBarrier();

        /// <summary>
        /// A memory fence at the system level.
        /// </summary>
        [MemoryBarrierIntrinsic(MemoryBarrierKind.SystemLevel)]
        public static void SystemLevel() => Interlocked.MemoryBarrier();
    }
}
