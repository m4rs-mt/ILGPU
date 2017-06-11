// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Backend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Reflection;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a target platform.
    /// </summary>
    public enum TargetPlatform
    {
        /// <summary>
        /// The X86 target platform.
        /// </summary>
        X86,

        /// <summary>
        /// The X64 target platform.
        /// </summary>
        X64,
    }

    /// <summary>
    /// Represents a general ILGPU backend.
    /// </summary>
    public abstract class Backend : DisposeBase
    {
        #region Static

        /// <summary>
        /// Returns the current execution platform.
        /// </summary>
        public static TargetPlatform RuntimePlatform =>
            IntPtr.Size == 8 ? TargetPlatform.X64 : TargetPlatform.X86;

        /// <summary>
        /// Returns the native OS platform.
        /// </summary>
        public static TargetPlatform OSPlatform =>
            Environment.Is64BitOperatingSystem ? TargetPlatform.X64 : TargetPlatform.X86;

        /// <summary>
        /// Returns true iff the current runtime platform is equal to the OS platform.
        /// </summary>
        public static bool RunningOnNativePlatform => RuntimePlatform == OSPlatform;

        /// <summary>
        /// Ensures that the current runtime platform is equal to the OS platform.
        /// If not, this method will throw a <see cref="NotSupportedException"/>.
        /// </summary>
        public static void EnsureRunningOnNativePlatform()
        {
            if (!RunningOnNativePlatform)
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NativePlatformInvocationRequired,
                    RuntimePlatform,
                    OSPlatform));
        }

        /// <summary>
        /// Ensures that the current runtime platform is equal to the given platform.
        /// If not, this method will throw a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="platform">The desired target platform.</param>
        public static void EnsureRunningOnPlatform(TargetPlatform platform)
        {
            if (RuntimePlatform != platform)
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NotSupportedPlatform,
                    RuntimePlatform,
                    platform));
        }

        /// <summary>
        /// Returns either the given target platform or the current one.
        /// </summary>
        /// <param name="platform">The nullable target platform.</param>
        /// <returns>The computed target platform.</returns>
        protected static TargetPlatform GetPlatform(TargetPlatform? platform)
        {
            if (platform.HasValue)
                return platform.Value;
            else
                return RuntimePlatform;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new generic backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="platform">The target platform.</param>
        protected Backend(Context context, TargetPlatform platform)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Platform = platform;

            IntPtrType = platform == TargetPlatform.X64 ? typeof(long) : typeof(int);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the assigned context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the target platform.
        /// </summary>
        public TargetPlatform Platform { get; }

        /// <summary>
        /// Returns the int-pointer type for this backend.
        /// </summary>
        public Type IntPtrType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Specifies the target backend for the given compile unit.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        internal abstract void TargetUnit(CompileUnit unit);

        /// <summary>
        /// Creates a compatbile ABI specification for the given compile unit.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <returns>The created ABI specification.</returns>
        internal abstract ABI.ABISpecification CreateABISpecification(CompileUnit unit);

        /// <summary>
        /// Compiles a given compile unit with the specified entry point.
        /// </summary>
        /// <param name="unit">The compile unit to compile.</param>
        /// <param name="entry">The desired entry point.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public abstract CompiledKernel Compile(CompileUnit unit, MethodInfo entry);

        #endregion
    }
}
