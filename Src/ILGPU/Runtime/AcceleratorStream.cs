// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: AcceleratorStream.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents an abstract kernel stream for async processing.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
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
        public Task SynchronizeAsync()
        {
            return Task.Run(synchronizeAction);
        }

        #endregion
    }
}
