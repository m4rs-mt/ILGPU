// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorObject.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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
        /// Constructs an accelerator object.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected AcceleratorObject(Accelerator accelerator)
        {
            Accelerator = accelerator;
            accelerator?.RegisterChildObject(this);
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
        public AcceleratorType AcceleratorType =>
            Accelerator?.AcceleratorType ?? AcceleratorType.CPU;

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected sealed override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // Leave the dispose functionality to the associated accelerator object
            if (Accelerator != null)
                Accelerator.DisposeChildObject_AcceleratorObject(this, disposing);
            else
                DisposeAcceleratorObject_Accelerator(disposing);
        }

        /// <summary>
        /// Disposes this accelerator object. Implementations of this function can
        /// assume that the associated accelerator is currently bound and active.
        /// </summary>
        /// <param name="disposing">
        /// True, if the method is not called by the finalizer.
        /// </param>
        /// <remarks>
        /// This function is called by the owning accelerator instance. This can either
        /// be <see cref="Accelerator.DisposeChildObject_AcceleratorObject(
        /// AcceleratorObject, bool)"/> or
        /// <see cref="Accelerator.DisposeChildObjects_SyncRoot(bool)"/>.
        /// </remarks>
        internal void DisposeAcceleratorObject_Accelerator(bool disposing)
        {
            if (!MarkDisposed_Unsafe())
                return;
            DisposeAcceleratorObject(disposing);
        }

        /// <summary>
        /// Disposes this accelerator object. Implementations of this function can
        /// assume that the associated accelerator is currently bound and active.
        /// </summary>
        /// <param name="disposing">
        /// True, if the method is not called by the finalizer.
        /// </param>
        /// <remarks>
        /// This function is called by the owning accelerator instance. This can either
        /// be <see cref="Accelerator.DisposeChildObject_AcceleratorObject(
        /// AcceleratorObject, bool)"/> or
        /// <see cref="Accelerator.DisposeChildObjects_SyncRoot(bool)"/>.
        /// </remarks>
        protected abstract void DisposeAcceleratorObject(bool disposing);

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
            new List<WeakReference<AcceleratorObject>>(MinNumberOfChildObjectsInGC);

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of the registered child objects that depend on this
        /// accelerator object.
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
        /// True, if a GC run is requested to clean disposed child objects.
        /// </summary>
        /// <remarks>This method is invoked in the scope of the locked
        /// <see cref="syncRoot"/> object.</remarks>
        private bool RequestChildObjectsGC_SyncRoot =>
            (childObjects.Count + 1) % NumberNewChildObjectsUntilGC == 0;

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

        /// <summary>
        /// Perform an action on each child object.
        /// </summary>
        /// <typeparam name="T">The type of child object.</typeparam>
        /// <param name="callback">The action to perform on the object.</param>
        protected void ForEachChildObject<T>(Action<T> callback)
            where T : AcceleratorObject
        {
            lock (syncRoot)
            {
                foreach (var child in childObjects)
                {
                    if (child.TryGetTarget(out var acceleratorObject) &&
                        acceleratorObject is T t)
                    {
                        callback(t);
                    }
                }
            }
        }

        #endregion

        #region Dispose Functionality

        /// <summary>
        /// Disposes the given child object associated with this accelerator instance.
        /// CAUTION: this function is invoked by the
        /// <see cref="AcceleratorObject.Dispose(bool)"/> function only. It should
        /// never be called in a different context.
        /// </summary>
        /// <param name="acceleratorObject">The object to dispose.</param>
        /// <param name="disposing">
        /// True, if the method is not called by the finalizer.
        /// </param>
        internal void DisposeChildObject_AcceleratorObject(
            AcceleratorObject acceleratorObject,
            bool disposing)
        {
            // Assert that this object belongs to us.
            Debug.Assert(
                acceleratorObject.Accelerator == this,
                "Invalid accelerator association");

            // Lock the current accelerator syncLock to avoid ongoing disposes
            lock (syncRoot)
            {
                // Check the native pointer of the current accelerator
                if (IsDisposed)
                    return;

                // Ensure that we have a valid accelerator binding
                var binding = BindScoped();

                // Dispose the actual accelerator object
                acceleratorObject.DisposeAcceleratorObject_Accelerator(disposing);

                // Recover the current binding
                binding.Recover();
            }
        }

        /// <summary>
        /// Disposes all child objects that are still alive since they are not allowed
        /// to live longer than the parent accelerator object.
        /// </summary>
        private void DisposeChildObjects_SyncRoot(bool disposing)
        {
            foreach (var childObject in childObjects)
            {
                // Try to get the actual child object and dispose it
                if (childObject.TryGetTarget(out AcceleratorObject? obj))
                {
                    // We can safely dispose the object at this point since the current
                    // accelerator is bound and the syncRoot lock is acquired
                    obj.DisposeAcceleratorObject_Accelerator(disposing);
                }
            }
            childObjects.Clear();
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
                if (childObject.TryGetTarget(out AcceleratorObject? _))
                    childObjects.Add(childObject);
            }
        }

        #endregion
    }
}
