// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accelerator.GC.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Instance

        /// <summary>
        /// True, if the GC thread is activated.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private volatile bool gcActivated = false;

        /// <summary>
        /// The child-object GC thread
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Thread gcThread;

        /// <summary>
        /// Initializes the GC functionality.
        /// </summary>
        [MemberNotNull(nameof(gcThread))]
        private void InitGC()
        {
            gcActivated = true;
            gcThread = new Thread(GCThread)
            {
                Name = $"ILGPU_{InstanceId}_GCThread",
                IsBackground = true,
            };
            gcThread.Start();
        }

        /// <summary>
        /// Disposes the GC functionality.
        /// </summary>
        private void DisposeGC_SyncRoot()
        {
            gcActivated = false;
            Monitor.PulseAll(syncRoot);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Requests a GC run.
        /// </summary>
        /// <remarks>This method is invoked in the scope of the locked
        /// <see cref="syncRoot"/> object.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RequestGC_SyncRoot()
        {
            if (RequestChildObjectsGC_SyncRoot || RequestKernelCacheGC_SyncRoot)
                Monitor.PulseAll(syncRoot);
        }

        /// <summary>
        /// GC thread to clean up cached resources.
        /// </summary>
        private void GCThread()
        {
            lock (syncRoot)
            {
                while (gcActivated)
                {
                    Monitor.Wait(syncRoot);

                    if (!gcActivated)
                        break;

                    ChildObjectsGC_SyncRoot();
                    KernelCacheGC_SyncRoot();
                }
            }
        }

        #endregion
    }
}
