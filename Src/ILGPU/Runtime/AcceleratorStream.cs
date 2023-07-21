// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents an abstract kernel stream for asynchronous processing.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    public abstract class AcceleratorStream : AcceleratorObject
    {
        #region Instance

        private readonly Action synchronizeAction;

        /// <summary>
        /// Constructs a new accelerator stream.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected AcceleratorStream(Accelerator accelerator)
            : base(accelerator)
        {
            synchronizeAction = () => Synchronize();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Synchronizes all queued operations.
        /// </summary>
        public abstract void Synchronize();

        /// <summary>
        /// Synchronizes all queued operations asynchronously.
        /// </summary>
        /// <returns>A task object to wait for.</returns>
        public Task SynchronizeAsync() => Task.Run(synchronizeAction);

        /// <summary>
        /// Makes the associated accelerator the current one for this thread and
        /// returns a <see cref="ScopedAcceleratorBinding"/> object that allows
        /// to easily recover the old binding.
        /// </summary>
        /// <returns>A scoped binding object.</returns>
        public ScopedAcceleratorBinding BindScoped() => Accelerator.BindScoped();

        /// <summary>
        /// Adds a profiling marker into the stream.
        /// </summary>
        /// <returns>The profiling marker.</returns>
        public ProfilingMarker AddProfilingMarker() =>
            Accelerator.Context.Properties.EnableProfiling
            ? AddProfilingMarkerInternal()
            : throw new NotSupportedException(
                RuntimeErrorMessages.NotSupportedProfilingMarker);

        /// <summary>
        /// Adds a profiling marker into the stream.
        /// </summary>
        /// <returns>The profiling marker.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract ProfilingMarker AddProfilingMarkerInternal();

        #endregion
    }
}
