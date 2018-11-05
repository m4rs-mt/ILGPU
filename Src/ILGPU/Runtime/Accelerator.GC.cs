// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: AcceleratorGC.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Instance

        /// <summary>
        /// True, iff the GC thread is enabled.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private volatile bool gcEnabled = true;

        /// <summary>
        /// The child-object GC thread
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Thread gcThread;

        /// <summary>
        /// Initializes the GC functionality.
        /// </summary>
        private void InitGC()
        {
            gcThread = new Thread(GCThread)
            {
                Name = "ILGPUAcceleratorGCThread",
            };
            gcThread.Start();
        }

        /// <summary>
        /// Disposes the GC functionality.
        /// </summary>
        private void DisposeGC()
        {
            lock (syncRoot)
            {
                gcEnabled = false;
                Monitor.Pulse(syncRoot);
            }
            gcThread.Join();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Requests a GC run.
        /// </summary>
        /// <remarks>This method is invoked in the scope of the locked <see cref="syncRoot"/> object.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RequestGC_SyncRoot()
        {
            if (RequestChildObjectsGC_SyncRoot || RequestKernelCacheGC_SyncRoot)
                Monitor.Pulse(syncRoot);
        }

        /// <summary>
        /// GC thread to clean up cached resources.
        /// </summary>
        private void GCThread()
        {
            lock (syncRoot)
            {
                while (gcEnabled)
                {
                    Monitor.Wait(syncRoot);

                    ChildObjectsGC_SyncRoot();
                    KernelCacheGC_SyncRoot();
                }
            }
        }

        #endregion
    }
}
