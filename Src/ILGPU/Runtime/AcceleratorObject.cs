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
        #region Constants

        /// <summary>
        /// Constant to control GC invocations.
        /// </summary>
        private const int NumberNewChildObjectsUntilGC = 4096;

        /// <summary>
        /// Minimum number of child objects before we apply GC.
        /// </summary>
        /// <remarks>Should be less or equal to <see cref="NumberNewChildObjectsUntilGC"/></remarks>
        private const int MinNumberOfChildObjectsInGC = 1024;

        #endregion

        #region Instance

        /// <summary>
        /// The list of linked child objects.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<WeakReference<AcceleratorObject>> childObjects =
            new List<WeakReference<AcceleratorObject>>();

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
                lock (syncRoot)
                    return childObjects.Count;
            }
        }

        /// <summary>
        /// True, iff a GC run is requested to clean disposed child objects.
        /// </summary>
        /// <remarks>This method is invoked in the scope of the locked <see cref="syncRoot"/> object.</remarks>
        private bool RequestChildObjectsGC_SyncRoot =>
            (childObjects.Count % NumberNewChildObjectsUntilGC) == 0;

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
            lock (syncRoot)
            {
                childObjects.Add(objRef);
                RequestGC_SyncRoot();
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
            lock (syncRoot)
            {
                foreach (var childObject in childObjects)
                {
                    if (childObject.TryGetTarget(out AcceleratorObject obj))
                        obj.Dispose();
                }
                childObjects.Clear();
            }
        }

        /// <summary>
        /// GC method to clean disposed child objects.
        /// </summary>
        /// <remarks>This method is invoked in the scope of the locked <see cref="syncRoot"/> object.</remarks>
        private void ChildObjectsGC_SyncRoot()
        {
            if (childObjects.Count < MinNumberOfChildObjectsInGC)
                return;

            var oldObjects = childObjects;
            childObjects = new List<WeakReference<AcceleratorObject>>();
            foreach (var childObject in oldObjects)
            {
                if (childObject.TryGetTarget(out AcceleratorObject obj))
                    childObjects.Add(childObject);
            }
        }

        #endregion
    }
}
