// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ContextFlags.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU
{
    /// <summary>
    /// Represents flags for a <see cref="Context"/>.
    /// </summary>
    /// <remarks>
    /// [ 0 -  7] = debugging settings
    /// [ 8 - 15] = code generation settings
    /// [16 - 23] = transformation settings
    /// </remarks>
    [Flags]
    public enum ContextFlags : int
    {
        /// <summary>
        /// Default flags.
        /// </summary>
        None = 0,

        // Debugging settings

        /// <summary>
        /// Enables debug information (if available),
        /// </summary>
        EnableDebugInformation = 1 << 0,

        /// <summary>
        /// Enables inline source-code annotations when generating kernels.
        /// </summary>
        /// <remarks>Note that this is only supported if debug information is activated.</remarks>
        EnableInlineSourceAnnotations = 1 << 1,

        /// <summary>
        /// Enables assertions.
        /// </summary>
        EnableAssertions = 1 << 2,

        //
        // Code generation settings
        //

        /// <summary>
        /// Enables parallel code generation in frontend.
        /// Note that this does not affect parallel transformations.
        /// </summary>
        EnableParallelCodeGenerationInFrontend = 1 << 8,

        /// <summary>
        /// Loads from mutable static fields are rejected by default.
        /// However, their current values can be inlined during jit
        /// compilation. Adding this flags causes values from mutable
        /// static fields to be inlined instead of rejected.
        /// </summary>
        InlineMutableStaticFieldValues = 1 << 9,

        /// <summary>
        /// Stores to static fields are rejected by default.
        /// Adding this flag causes stores to static fields
        /// to be silently ignored instead of rejected.
        /// </summary>
        IgnoreStaticFieldStores = 1 << 10,

        /// <summary>
        /// Represents fast math compilation flags.
        /// </summary>
        FastMath = 1 << 11,

        /// <summary>
        /// Forces the use of 32bit floats instead of 64bit floats.
        /// This affects all math operations (like Math.Sqrt(double)) and
        /// all 64bit float conversions. This settings might improve
        /// performance dramatically but might cause precision loss.
        /// </summary>
        Force32BitFloats = 1 << 12,

        //
        // Transformation settings
        //

        /// <summary>
        /// Forces a .Net GC run after every context GC.
        /// </summary>
        ForceSystemGC = 1 << 16,

        /// <summary>
        /// Skips the internal IR code generation phase for CPU kernels (debug flag).
        /// </summary>
        /// <remarks>
        /// Caution: this avoids general kernel code-analysis and verfication checks.
        /// </remarks>
        SkipCPUCodeGeneration = 1 << 17,

        /// <summary>
        /// Represents an aggressive inlining policy.
        /// (all functions will be inlined).
        /// </summary>
        AggressiveInlining = 1 << 18,

        /// <summary>
        /// Represents an convservative inlining policy.
        /// (only functions that are marked with "aggressive inlinine" will be inlined).
        /// </summary>
        ConservativeInlining = 1 << 19,

        /// <summary>
        /// Represents an convservative inlining policy.
        /// (only functions that are marked with "aggressive inlinine" will be inlined).
        /// </summary>
        NoInlining = 1 << 20,

        /// <summary>
        /// Disables the on-the-fly constant propagation functionality
        /// (e.g. for debugging purposes).
        /// </summary>
        DisableConstantPropagation = 1 << 21,
    }

    /// <summary>
    /// Helper methods for the <see cref="ContextFlags"/> enumeration.
    /// </summary>
    public static class ContextFlagsExtensions
    {
        /// <summary>
        /// Determines whether one or more bits are set in the current flags.
        /// </summary>
        /// <param name="flags">The current flags.</param>
        /// <param name="flagsToCheck">The flags to check.</param>
        /// <returns>True, the requested bits are set.</returns>
        public static bool HasFlags(this ContextFlags flags, ContextFlags flagsToCheck) =>
            (flags & flagsToCheck) == flagsToCheck;

        /// <summary>
        /// Prepares the given flags by toggeling convenient flag combinations.
        /// </summary>
        /// <param name="flags">The flags to prepare.</param>
        /// <returns>The prepared flags.</returns>
        internal static ContextFlags Prepare(this ContextFlags flags)
        {
            if (flags.HasFlags(ContextFlags.EnableInlineSourceAnnotations))
                flags |= ContextFlags.EnableDebugInformation;

            return flags;
        }
    }
}
