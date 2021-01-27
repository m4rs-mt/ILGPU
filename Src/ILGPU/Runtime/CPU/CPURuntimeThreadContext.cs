// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPURuntimeThreadContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a runtime context for a single thread.
    /// </summary>
    sealed class CPURuntimeThreadContext
    {
        #region Thread Static

        /// <summary>
        /// Represents the current context.
        /// </summary>
        [ThreadStatic]
        private static CPURuntimeThreadContext currentContext;

        /// <summary>
        /// Returns the current warp runtime context.
        /// </summary>
        public static CPURuntimeThreadContext Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Trace.Assert(
                    currentContext != null,
                    ErrorMessages.InvalidKernelOperation);
                return currentContext;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Creates a new runtime thread context.
        /// </summary>
        /// <param name="laneIdx">The current lane index.</param>
        public CPURuntimeThreadContext(int laneIdx)
        {
            if (laneIdx < 0)
                throw new ArgumentOutOfRangeException(nameof(laneIdx));
            LaneIndex = laneIdx;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The current lane index within the CPU accelerator.
        /// </summary>
        public int LaneIndex { get; private set; }

        /// <summary>
        /// Returns the grid index within the scheduled thread grid.
        /// </summary>
        public Index3 GridIndex { get; internal set; }

        /// <summary>
        /// Returns the group index within the scheduled thread grid.
        /// </summary>
        public Index3 GroupIndex { get; internal set; }

        /// <summary>
        /// Returns the linear thread index within this thread group.
        /// </summary>
        public int LinearGroupIndex { get; internal set; }

        #endregion

        #region Methods

        /// <summary>
        /// Makes the current context the active one for this thread.
        /// </summary>
        internal void MakeCurrent() => currentContext = this;

        #endregion
    }
}
