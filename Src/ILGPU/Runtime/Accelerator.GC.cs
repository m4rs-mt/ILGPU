// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accelerator.GC.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ILGPU.Runtime;

partial class Accelerator
{
    #region Instance

    /// <summary>
    /// True, if the GC thread is activated.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private volatile bool _gcActivated = false;

    /// <summary>
    /// The child-object GC thread
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Thread _gcThread;

    /// <summary>
    /// Initializes the GC functionality.
    /// </summary>
    [MemberNotNull(nameof(_gcThread))]
    private void InitGC()
    {
        _gcActivated = true;
        _gcThread = new Thread(GCThread)
        {
            Name = $"ILGPU_{InstanceId}_GCThread",
            IsBackground = true,
        };
        _gcThread.Start();
    }

    /// <summary>
    /// Disposes the GC functionality.
    /// </summary>
    private void DisposeGC_Locked()
    {
        _gcActivated = false;
        Monitor.PulseAll(_lock);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Requests a GC run.
    /// </summary>
    /// <remarks>This method is invoked in the scope of the locked <see cref="_lock"/>
    /// object.
    /// </remarks>
    private void RequestGC_SyncRoot()
    {
        if (RequestChildObjectsGC_SyncRoot)
            Monitor.PulseAll(_lock);
    }

    /// <summary>
    /// GC thread to clean up cached resources.
    /// </summary>
    private void GCThread()
    {
        lock (_lock)
        {
            while (_gcActivated)
            {
                Monitor.Wait(_lock);

                if (!_gcActivated)
                    break;

                ChildObjectsGC_SyncRoot();
            }
        }
    }

    #endregion
}
