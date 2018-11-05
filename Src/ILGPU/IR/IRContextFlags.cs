// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRContextFlags.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents flags for an <see cref="IRContext"/>.
    /// </summary>
    [Flags]
    public enum IRContextFlags : int
    {
        /// <summary>
        /// Default flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Enables debug information (if available),
        /// </summary>
        EnableDebugInformation = 1 << 0,

        /// <summary>
        /// Enables assertions.
        /// </summary>
        EnableAssertions = 1 << 1,

        /// <summary>
        /// Loads from mutable static fields are rejected by default.
        /// However, their current values can be inlined during jit
        /// compilation. Adding this flags causes values from mutable
        /// static fields to be inlined instead of rejected.
        /// </summary>
        InlineMutableStaticFieldValues = 1 << 2,

        /// <summary>
        /// Stores to static fields are rejected by default.
        /// Adding this flag causes stores to static fields
        /// to be silently ignored instead of rejected.
        /// </summary>
        IgnoreStaticFieldStores = 1 << 3,

        /// <summary>
        /// Represents an aggressive inlining policy.
        /// (all functions will be inlined).
        /// </summary>
        AggressiveInlining = 1 << 4,

        /// <summary>
        /// Represents fast math compilation flags.
        /// </summary>
        FastMath = 1 << 5,

        /// <summary>
        /// Forces the use of 32bit floats instead of 64bit floats.
        /// This affects all math operations (like Math.Sqrt(double)) and
        /// all 64bit float conversions. This settings might improve
        /// performance dramatically but might cause precision loss.
        /// </summary>
        Force32BitFloats = 1 << 6,

        /// <summary>
        /// Forces a .Net GC run after every context GC.
        /// </summary>
        ForceSystemGC = 1 << 7,
    }
}
