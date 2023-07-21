// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PeerAccess.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Instance

        /// <summary>
        /// Contains a collection of all peer accelerators.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private InlineList<InstanceId> storedPeerAccelerators =
            InlineList<InstanceId>.Create(16);

        #endregion

        #region Methods

        /// <summary>
        /// Event handler to disable peer access to disposed accelerators.
        /// </summary>
        private void PeerAccessAcceleratorDestroyed(object? sender, EventArgs e)
        {
            // Reject cases in which the sender is not another accelerator instance
            if (!(sender is Accelerator otherAccelerator))
                return;

            // Disable the peer access to this accelerator
            otherAccelerator.DisablePeerAccess(this);
            // Try to disable peer access to the other accelerator
            DisablePeerAccess(otherAccelerator);
        }

        /// <summary>
        /// Returns true if the current accelerator can directly access the memory
        /// of the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        /// <returns>True, if the current accelerator can directly access the memory
        /// of the given accelerator.</returns>
        public bool CanAccessPeer(Accelerator otherAccelerator)
        {
            if (otherAccelerator == null)
                throw new ArgumentNullException(nameof(otherAccelerator));
            Bind();
            return CanAccessPeerInternal(otherAccelerator);
        }

        /// <summary>
        /// Returns true if peer access between the current and the given accelerator
        /// has been enabled.
        /// </summary>
        /// <param name="otherAccelerator">The target accelerator.</param>
        /// <returns></returns>
        public bool HasPeerAccess(Accelerator otherAccelerator)
        {
            if (otherAccelerator == null)
                throw new ArgumentNullException(nameof(otherAccelerator));
            lock (syncRoot)
            {
                return storedPeerAccelerators.Contains(
                    otherAccelerator.InstanceId,
                    new InstanceId.Comparer());
            }
        }

        /// <summary>
        /// Returns true if the current accelerator can directly access the memory
        /// of the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        /// <returns>True, if the current accelerator can directly access the memory
        /// of the given accelerator.</returns>
        protected abstract bool CanAccessPeerInternal(Accelerator otherAccelerator);

        /// <summary>
        /// Tries to enable a bidirectional peer access between the current and the given
        /// accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        /// <returns>True, if the bidirectional access could be established.</returns>
        public bool EnableBidirectionalPeerAccess(Accelerator otherAccelerator) =>
            EnablePeerAccess(otherAccelerator) &&
            otherAccelerator.EnablePeerAccess(this);

        /// <summary>
        /// Enables peer access to the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        public bool EnablePeerAccess(Accelerator otherAccelerator)
        {
            lock (syncRoot)
            {
                if (HasPeerAccess(otherAccelerator))
                    return true;
                if (!CanAccessPeerInternal(otherAccelerator))
                    return false;
                Bind();
                otherAccelerator.Disposed += PeerAccessAcceleratorDestroyed;
                storedPeerAccelerators.Add(otherAccelerator.InstanceId);
                EnablePeerAccessInternal(otherAccelerator);
                return true;
            }
        }

        /// <summary>
        /// Enables peer access to the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        protected abstract void EnablePeerAccessInternal(Accelerator otherAccelerator);

        /// <summary>
        /// Disables peer access to the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        public bool DisablePeerAccess(Accelerator otherAccelerator)
        {
            lock (syncRoot)
            {
                if (!HasPeerAccess(otherAccelerator))
                    return false;
                Bind();
                otherAccelerator.Disposed -= PeerAccessAcceleratorDestroyed;
                storedPeerAccelerators.Remove(
                    otherAccelerator.InstanceId,
                    new InstanceId.Comparer());
                DisablePeerAccessInternal(otherAccelerator);
                return true;
            }
        }

        /// <summary>
        /// Disables peer access to the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        protected abstract void DisablePeerAccessInternal(Accelerator otherAccelerator);

        #endregion
    }
}
