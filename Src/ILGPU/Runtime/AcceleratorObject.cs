// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: AcceleratorObject.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents the base class for all accelerator-dependent objects.
    /// </summary>
    /// <remarks>
    /// Note that accelerator objects are destroyed when their parent accelerator object is destroyed.
    /// </remarks>
    public abstract class AcceleratorObject : DisposeBase
    {
        #region Instance

        /// <summary>
        /// Constructs an accelerator object.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected AcceleratorObject(Accelerator accelerator)
        {
            Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
            accelerator.RegisterChildObject(this);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }

        #endregion
    }

    partial class Accelerator
    {
        #region Instance

        /// <summary>
        /// Constant to control GC invocations.
        /// </summary>
        private const int NumberNewChildObjectsUntilGC = 8192;

        /// <summary>
        /// Minimum number of child objects before we apply GC.
        /// </summary>
        private const int MinNumberOfChildObjectsInGC = 100;

        /// <summary>
        /// Main object for child synchronization.
        /// Note that this is a different synchronization object than the main
        /// <see cref="syncRoot"/> object.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly object childSyncRoot = new object();

        /// <summary>
        /// The child-object GC thread
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Thread childObjectGCThread;

        /// <summary>
        /// The list of linked child objects.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<WeakReference<AcceleratorObject>> childObjects = new List<WeakReference<AcceleratorObject>>();

        /// <summary>
        /// Initializes the child-object functionality.
        /// </summary>
        private void InitChildObjects()
        {
            childObjectGCThread = new Thread(ChildObjectsGCThread);
            childObjectGCThread.Start();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of the associated child objects that depend
        /// on this accelerator object.
        /// </summary>
        public int NumberChildObjects
        {
            get
            {
                lock (childSyncRoot)
                    return childObjects.Count;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Registers a child object with the current accelerator object.
        /// </summary>
        /// <param name="child">The child object to register.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RegisterChildObject<T>(T child)
            where T : AcceleratorObject
        {
            var objRef = new WeakReference<AcceleratorObject>(child);
            lock (childSyncRoot)
            {
                childObjects.Add(objRef);
                if ((childObjects.Count % NumberNewChildObjectsUntilGC) == 0)
                    Monitor.Pulse(childSyncRoot);
            }
        }

        #endregion

        #region Dispose Functionality

        /// <summary>
        /// Disposes all child objects that are still alive since they are not allowed
        /// to live longer than the parent accelerator object.
        /// </summary>
        private void DisposeChildObjects()
        {
            lock (childSyncRoot)
            {
                foreach (var childObject in childObjects)
                {
                    if (childObject.TryGetTarget(out AcceleratorObject obj))
                        obj.Dispose();
                }
                childObjects = null;
                Monitor.Pulse(childSyncRoot);
            }
            childObjectGCThread.Join();
        }

        /// <summary>
        /// GC thread to remove disposed children.
        /// </summary>
        private void ChildObjectsGCThread()
        {
            for (; ;)
            {
                lock (childSyncRoot)
                {
                    while (childObjects != null && childObjects.Count < MinNumberOfChildObjectsInGC)
                        Monitor.Wait(childSyncRoot);

                    if (childObjects == null)
                        break;

                    var collectedObjects = new List<WeakReference<AcceleratorObject>>();
                    foreach (var childObject in childObjects)
                    {
                        if (childObject.TryGetTarget(out AcceleratorObject obj))
                            collectedObjects.Add(childObject);
                    }
                    childObjects = collectedObjects;
                }
            }
        }

        #endregion
    }
}
