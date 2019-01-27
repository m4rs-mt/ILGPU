// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PeerAccess.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Instance

        /// <summary>
        /// Contains a collection of all peer accelerators.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly HashSet<Accelerator> storedPeerAccelerators = new HashSet<Accelerator>();

        #endregion

        #region Properties

        /// <summary>
        /// Returns the accelerators for which the peer access has been enabled.
        /// </summary>
        [Obsolete("This property will be removed in the future for performance reasons")]
        public IReadOnlyCollection<Accelerator> PeerAccelerators
        {
            get
            {
                lock (syncRoot)
                    return storedPeerAccelerators.ToArray();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Event handler to disable peer access to disposed accelerators.
        /// </summary>
        private void PeerAccessAcceleratorDestroyed(object sender, EventArgs e)
        {
            if (sender is Accelerator otherAccelerator)
                DisablePeerAccess(otherAccelerator);
        }

        /// <summary>
        /// Returns true iff peer access between the current and the given accelerator has been enabled.
        /// </summary>
        /// <param name="otherAccelerator">The target accelerator.</param>
        /// <returns></returns>
        public bool HasPeerAccess(Accelerator otherAccelerator)
        {
            if (otherAccelerator == null)
                throw new ArgumentNullException(nameof(otherAccelerator));
            lock (syncRoot)
                return storedPeerAccelerators.Contains(otherAccelerator);
        }

        /// <summary>
        /// Returns true iff the current accelerator can directly access the memory
        /// of the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        /// <returns>True, iff the current accelerator can directly access the memory
        /// of the given accelerator.</returns>
        public bool CanAccessPeer(Accelerator otherAccelerator)
        {
            if (otherAccelerator == null)
                throw new ArgumentNullException(nameof(otherAccelerator));
            lock (syncRoot)
            {
                Bind();
                return CanAccessPeerInternal(otherAccelerator);
            }
        }

        /// <summary>
        /// Returns true iff the current accelerator can directly access the memory
        /// of the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        /// <returns>True, iff the current accelerator can directly access the memory
        /// of the given accelerator.</returns>
        protected abstract bool CanAccessPeerInternal(Accelerator otherAccelerator);

        /// <summary>
        /// Enables peer access to the given accelerator.
        /// </summary>
        /// <param name="otherAccelerator">The other accelerator.</param>
        public bool EnablePeerAccess(Accelerator otherAccelerator)
        {
            lock (syncRoot)
            {
                if (HasPeerAccess(otherAccelerator))
                    return false;
                Bind();
                otherAccelerator.Disposed += PeerAccessAcceleratorDestroyed;
                storedPeerAccelerators.Add(otherAccelerator);
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
                storedPeerAccelerators.Remove(otherAccelerator);
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
