// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AcceleratorObject.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents an abstract accelerator object.
    /// </summary>
    public interface IAcceleratorObject : IDisposable
    {
        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        Accelerator Accelerator { get; }

        /// <summary>
        /// Returns the accelerator type of this object.
        /// </summary>
        AcceleratorType AcceleratorType { get; }
    }

    /// <summary>
    /// Represents the base class for all accelerator-dependent objects.
    /// </summary>
    /// <remarks>
    /// Note that accelerator objects are destroyed when their parent accelerator
    /// object is destroyed.
    /// </remarks>
    public abstract class AcceleratorObject : DisposeBase, IAcceleratorObject
    {
        #region Instance

        /// <summary>
        /// Constructs an accelerator object that lives on the CPU.
        /// </summary>
        protected AcceleratorObject()
        {
            AcceleratorType = AcceleratorType.CPU;
        }

        /// <summary>
        /// Constructs an accelerator object.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected AcceleratorObject(Accelerator accelerator)
        {
            Accelerator = accelerator;
            AcceleratorType = accelerator.AcceleratorType;
            accelerator.RegisterChildObject(this);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }

        /// <summary>
        /// Returns the accelerator type of this object.
        /// </summary>
        public AcceleratorType AcceleratorType { get; }

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
        /// <remarks>
        /// Should be less or equal to <see cref="NumberNewChildObjectsUntilGC"/>.
        /// </remarks>
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
        /// Returns the number of the registered child objects that depend
        /// on this accelerator object.
        /// </summary>
        /// <remarks>
        /// Note that this number is affected by the flags
        /// <see cref="ContextFlags.DisableAutomaticBufferDisposal"/> and
        /// <see cref="ContextFlags.DisableAutomaticKernelDisposal"/>.
        /// </remarks>
        public int NumberChildObjects
        {
            get
            {
                lock (syncRoot)
                    return childObjects.Count;
            }
        }

        /// <summary>
        /// True, if a GC run is requested to clean disposed child objects.
        /// </summary>
        /// <remarks>This method is invoked in the scope of the locked
        /// <see cref="syncRoot"/> object.</remarks>
        private bool RequestChildObjectsGC_SyncRoot =>
            childObjects.Count % NumberNewChildObjectsUntilGC == 0;

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
            if (!GCEnabled ||
                !AutomaticBufferDisposalEnabled && child is MemoryBuffer ||
                !AutomaticKernelDisposalEnabled && child is Kernel)
            {
                return;
            }

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
        /// <remarks>This method is invoked in the scope of the locked
        /// <see cref="syncRoot"/> object.</remarks>
        private void ChildObjectsGC_SyncRoot()
        {
            if (childObjects.Count < MinNumberOfChildObjectsInGC)
                return;

            var oldObjects = childObjects;
            childObjects = new List<WeakReference<AcceleratorObject>>();
            foreach (var childObject in oldObjects)
            {
                if (childObject.TryGetTarget(out AcceleratorObject _))
                    childObjects.Add(childObject);
            }
        }

        #endregion
    }
}
