// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MetadataReaderOperation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Reflection.Metadata;
using System.Threading;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// An abstract provider for synchronized metadata reader operations.
    /// </summary>
    internal interface IMetadataReaderOperationProvider
    {
        /// <summary>
        /// Begins a synchronized metadata reader operation.
        /// </summary>
        /// <returns>The operation instance.</returns>
        MetadataReaderOperation BeginOperation();
    }

    /// <summary>
    /// Represents a synchronized metadata reader operation.
    /// </summary>
    /// <remarks>
    /// The current implementation of the <see cref="MetadataReader"/> seems to be
    /// thread safe based on the source code. However, this is not 100% safe.
    /// Wrap all operations using a thread-safe locking to ensure reliable functionality.
    /// </remarks>
    internal readonly struct MetadataReaderOperation : IDisposable
    {
        /// <summary>
        /// Constructs a new reader operation.
        /// </summary>
        /// <param name="reader">The parent reader.</param>
        /// <param name="syncLock">The synchronization object.</param>
        internal MetadataReaderOperation(MetadataReader reader, object syncLock)
        {
            Reader = reader;
            SyncLock = syncLock;

            Monitor.Enter(syncLock);
        }

        /// <summary>
        /// Returns the parent synchronization object.
        /// </summary>
        /// <remarks>
        /// Might be required in the future to synchronize accesses.
        /// </remarks>
        private object SyncLock { get; }

        /// <summary>
        /// Returns the parent reader.
        /// </summary>
        public MetadataReader Reader { get; }

        /// <summary>
        /// Releases the current synchronization lock.
        /// </summary>
        public void Dispose() => Monitor.Exit(SyncLock);
    }
}
