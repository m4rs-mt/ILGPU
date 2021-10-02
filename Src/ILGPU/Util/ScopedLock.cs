// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ScopedLock.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Threading;

namespace ILGPU.Util
{
    /// <summary>
    /// Represents read only scoped lock based on a <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    public readonly struct ReadOnlyScopedLock : IDisposable
    {
        private readonly ReaderWriterLockSlim syncLock;

        /// <summary>
        /// Constructs a new read scope and acquires the lock.
        /// </summary>
        /// <param name="readerWriterLock">The parent lock.</param>
        public ReadOnlyScopedLock(ReaderWriterLockSlim readerWriterLock)
        {
            syncLock = readerWriterLock;
            syncLock.EnterReadLock();
        }

        /// <summary>
        /// Releases the read lock.
        /// </summary>
        public readonly void Dispose() =>
            syncLock.ExitReadLock();
    }

    /// <summary>
    /// Represents write scoped lock based on a <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    public readonly struct WriteScopedLock : IDisposable
    {
        private readonly ReaderWriterLockSlim syncLock;

        /// <summary>
        /// Constructs a new write scope and acquires the lock.
        /// </summary>
        /// <param name="readerWriterLock">The parent lock.</param>
        public WriteScopedLock(ReaderWriterLockSlim readerWriterLock)
        {
            syncLock = readerWriterLock;
            syncLock.EnterWriteLock();
        }

        /// <summary>
        /// Releases the write lock.
        /// </summary>
        public readonly void Dispose() =>
            syncLock.ExitWriteLock();
    }

    /// <summary>
    /// Represents an upgradeable read scoped lock based on a
    /// <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    public readonly struct UpgradeableScopedLock : IDisposable
    {
        private readonly ReaderWriterLockSlim syncLock;

        /// <summary>
        /// Constructs a new upgradeable read scope and acquires the lock.
        /// </summary>
        /// <param name="readerWriterLock">The parent lock.</param>
        public UpgradeableScopedLock(ReaderWriterLockSlim readerWriterLock)
        {
            syncLock = readerWriterLock;
            syncLock.EnterUpgradeableReadLock();
        }

        /// <summary>
        /// Enters a new write lock.
        /// </summary>
        public WriteScopedLock EnterWriteScope() =>
            new WriteScopedLock(syncLock);

        /// <summary>
        /// Releases the upgradeable read lock.
        /// </summary>
        public readonly void Dispose() =>
            syncLock.ExitUpgradeableReadLock();
    }

    /// <summary>
    /// Additional extensions for scoped locks.
    /// </summary>
    public static class LockExtensions
    {
        /// <summary>
        /// Enters a new read scope.
        /// </summary>
        public static ReadOnlyScopedLock EnterReadScope(
            this ReaderWriterLockSlim readerWriterLock) =>
            new ReadOnlyScopedLock(readerWriterLock);

        /// <summary>
        /// Enters a new upgradeable read scope.
        /// </summary>
        public static UpgradeableScopedLock EnterUpgradeableReadScope(
            this ReaderWriterLockSlim readerWriterLock) =>
            new UpgradeableScopedLock(readerWriterLock);

        /// <summary>
        /// Enters a new write scope.
        /// </summary>
        public static WriteScopedLock EnterWriteScope(
            this ReaderWriterLockSlim readerWriterLock) =>
            new WriteScopedLock(readerWriterLock);
    }
}
