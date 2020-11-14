// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Accelerator.Launchers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Nested Types

        /// <summary>
        /// Represents an abstract kernel-launch loader.
        /// </summary>
        /// <typeparam name="TSource">The source delegate type.</typeparam>
        /// <typeparam name="TTarget">The target delegate type.</typeparam>
        private interface IKernelLaunchLoader<TSource, TTarget>
            where TSource : Delegate
            where TTarget : Delegate
        {
            /// <summary>
            /// Loads the internal launcher delegate using the <paramref name="source"/>
            /// CPU kernel delegate.
            /// </summary>
            /// <param name="accelerator">The parent accelerator.</param>
            /// <param name="source">The source kernel delegate to use.</param>
            /// <returns>
            /// The loaded launcher of type <typeparamref name="TTarget"/>.
            /// </returns>
            TTarget Load(Accelerator accelerator, TSource source);
        }

        #endregion

        #region Instance

        /// <summary>
        /// The internal async launch cache dictionary.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Dictionary<Delegate, Delegate> launchCache;

        /// <summary>
        /// Initializes the local launch cache.
        /// </summary>
        private void InitLaunchCache()
        {
            if (Context.HasFlags(ContextFlags.DisableKernelLaunchCaching))
                return;

            launchCache = new Dictionary<Delegate, Delegate>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if the launcher cache is enabled.
        /// </summary>
        private bool LaunchCacheEnabled => launchCache != null;

        #endregion

        #region Methods

        /// <summary>
        /// Gets or loads the given action using the provided launcher loaded.
        /// </summary>
        /// <typeparam name="TSource">The source delegate type load.</typeparam>
        /// <typeparam name="TTarget">The target kernel delegate type.</typeparam>
        /// <typeparam name="TLaunchLoader">The specialized launcher loader.</typeparam>
        /// <param name="action">The action to load.</param>
        /// <returns>The loaded target delegate launcher.</returns>
        private TTarget GetOrLoadLauncher<TSource, TTarget, TLaunchLoader>(
            TSource action)
            where TLaunchLoader : struct, IKernelLaunchLoader<TSource, TTarget>
            where TSource : Delegate
            where TTarget : Delegate
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            // Create a new launcher loader.
            TLaunchLoader loader = default;

            // Early exit for disabled launch caches
            if (!LaunchCacheEnabled)
                return loader.Load(this, action);

            // Synchronizes accesses with the launch cache
            lock (syncRoot)
            {
                // Try to load a previously loaded delegate and ensure that the loaded
                // launcher is compatible with the desired target delegate type
                if (!launchCache.TryGetValue(action, out var launcher) ||
                    !(launcher is TTarget))
                {
                    // Load the launcher using the provided loader
                    launcher = loader.Load(this, action);
                    launchCache.Add(action, launcher);
                }
                return launcher as TTarget;
            }
        }

        /// <summary>
        /// Clears the internal cache.
        /// </summary>
        private void ClearLaunchCache_SyncRoot()
        {
            if (!LaunchCacheEnabled)
                return;
            launchCache.Clear();
        }

        #endregion
    }
}
