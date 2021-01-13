// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ContextFlags.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
    /// [24 - 32] = accelerator settings
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
        /// Enables debug symbols (if available),
        /// </summary>
        EnableDebugSymbols = 1 << 0,

        /// <summary>
        /// Enables debug information in kernels (if available).
        /// </summary>
        EnableKernelDebugInformation = 1 << 1,

        /// <summary>
        /// Enables inline source-code annotations when generating kernels.
        /// </summary>
        /// <remarks>
        /// Note that this is only supported if debug information is activated.
        /// </remarks>
        EnableInlineSourceAnnotations = 1 << 2,

        /// <summary>
        /// Enables assertions.
        /// </summary>
        EnableAssertions = 1 << 3,

        /// <summary>
        /// Enables detailed kernel statistics about all compiled kernel functions.
        /// </summary>
        EnableKernelStatistics = 1 << 4,

        /// <summary>
        /// Enables the internal IR verifier.
        /// </summary>
        EnableVerifier = 1 << 5,

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
        /// However, their current values can be inlined during JIT
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
        [Obsolete("This flag is no longer required")]
        ForceSystemGC = 1 << 16,

        /// <summary>
        /// Skips the internal IR code generation phase for CPU kernels (debug flag).
        /// </summary>
        /// <remarks>
        /// Caution: this avoids general kernel code-analysis and verification checks.
        /// </remarks>
        SkipCPUCodeGeneration = 1 << 17,

        /// <summary>
        /// Represents an aggressive inlining policy.
        /// (all functions will be inlined).
        /// </summary>
        [Obsolete("AggressiveInlining is now enabled by default. To enable " +
            "conservative inlining behavior specify the flag " +
            "ContextFlags.ConservativeInlining.")]
        AggressiveInlining = 1 << 18,

        /// <summary>
        /// No functions will be inlined at all.
        /// </summary>
        NoInlining = 1 << 19,

        /// <summary>
        /// Disables the on-the-fly constant propagation functionality
        /// (e.g. for debugging purposes).
        /// </summary>
        DisableConstantPropagation = 1 << 20,

        /// <summary>
        /// Enables basic inlining heuristics and disables aggressive inlining behavior
        /// to reduce the overall code size.
        /// </summary>
        ConservativeInlining = 1 << 21,

        // Accelerator settings

        /// <summary>
        /// Disables all kernel-loading caches.
        /// </summary>
        /// <remarks>
        /// However, IR nodes, type information and debug information will still
        /// be cached, since they are used for different kernel compilation operations.
        /// If you want to clear those caches as well, you will have to clear them
        /// manually using <see cref="Context.ClearCache(ClearCacheMode)"/>.
        /// </remarks>
        DisableKernelCaching = 1 << 24,

        /// <summary>
        /// Disables automatic disposal of memory buffers in the scope of ILGPU GC
        /// threads.
        /// It should only be used by experienced users.
        /// </summary>
        /// <remarks>
        /// In theory, allocated memory buffers will be disposed automatically by the
        /// .Net GC. However, disposing accelerator objects before their associated
        /// memory buffers have been freed will end up in exceptions and sometimes
        /// driver crashes on different systems. If you disable automatic buffer
        /// disposal, you have to ensure that all accelerator child objects have been
        /// freed manually before disposing the associated accelerator object.
        /// </remarks>
        [Obsolete]
        DisableAutomaticBufferDisposal = 1 << 25,

        /// <summary>
        /// Disables automatic disposal of kernels in the scope of ILGPU GC threads.
        /// This is dangerous as the 'default' kernel-loading methods do not return
        /// <see cref="Runtime.Kernel"/> instances that can be disposed manually.
        /// It should only be used by experienced users.
        /// </summary>
        /// <remarks>
        /// In theory, allocated accelerator kernels will be disposed automatically by
        /// the .Net GC. However, disposing accelerator objects before their
        /// associated kernels have been freed will end up in exceptions and sometimes
        /// driver crashes on different systems. If you disable automatic kernel
        /// disposal, you have to ensure that all accelerator child objects have been
        /// freed manually before disposing the associated accelerator object.
        /// </remarks>
        [Obsolete]
        DisableAutomaticKernelDisposal = 1 << 26,

        /// <summary>
        /// Disables kernel caching and automatic disposal of memory buffers and kernels.
        /// It should only be used by experienced users.
        /// </summary>
        [Obsolete]
        DisableAcceleratorGC =
            DisableKernelCaching |
            DisableAutomaticBufferDisposal |
            DisableAutomaticKernelDisposal |
            DisableKernelLaunchCaching,

        /// <summary>
        /// Enforces the use of the default PTX backend features.
        /// </summary>
        DefaultPTXBackendFeatures = 1 << 27,

        /// <summary>
        /// Enables the use of enhanced PTX backend features to improve performance of
        /// the kernel programs being generated.
        /// </summary>
        EnhancedPTXBackendFeatures = 1 << 28,

        /// <summary>
        /// Disables the implicit kernel launch cache.
        /// </summary>
        /// <remarks>
        /// However, IR nodes, type information and debug information will still
        /// be cached, since they are used for different kernel compilation operations.
        /// If you want to clear those caches as well, you will have to clear them
        /// manually using <see cref="Context.ClearCache(ClearCacheMode)"/>.
        /// </remarks>
        DisableKernelLaunchCaching = 1 << 29,
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
        public static bool HasFlags(
            this ContextFlags flags,
            ContextFlags flagsToCheck) =>
            (flags & flagsToCheck) == flagsToCheck;

        /// <summary>
        /// Prepares the given flags by toggling convenient flag combinations.
        /// </summary>
        /// <param name="flags">The flags to prepare.</param>
        /// <returns>The prepared flags.</returns>
        internal static ContextFlags Prepare(this ContextFlags flags)
        {
            if (flags.HasFlags(ContextFlags.EnableInlineSourceAnnotations))
                flags |= ContextFlags.EnableKernelDebugInformation;

            if (flags.HasFlags(ContextFlags.EnableKernelDebugInformation))
                flags |= ContextFlags.EnableDebugSymbols;

            if (flags.HasFlags(ContextFlags.NoInlining))
                flags &= ~ContextFlags.ConservativeInlining;

            if (flags.HasFlags(ContextFlags.ConservativeInlining))
                flags &= ~ContextFlags.NoInlining;

            return flags;
        }
    }
}
