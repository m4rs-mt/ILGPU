// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ImplicitKernelLauncherArgument.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a launcher argument for an implicit-stream launcher.
    /// </summary>
    sealed class ImplicitKernelLauncherArgument
    {
        #region Static

        /// <summary>
        /// Represents a method to load the a kernel argument from a launcher instance.
        /// </summary>
        public static readonly MethodInfo GetKernelMethod =
            typeof(ImplicitKernelLauncherArgument).GetProperty(nameof(Kernel)).GetGetMethod(false);

        /// <summary>
        /// Represents a method to load the a stream argument from a launcher instance.
        /// </summary>
        public static readonly MethodInfo GetStreamMethod =
            typeof(ImplicitKernelLauncherArgument).GetProperty(nameof(Stream)).GetGetMethod(false);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new instance argument.
        /// </summary>
        /// <param name="kernel">The kernel argument.</param>
        /// <param name="stream">The accelerator stream.</param>
        public ImplicitKernelLauncherArgument(Kernel kernel, AcceleratorStream stream)
        {
            Debug.Assert(kernel != null && stream != null);
            Kernel = kernel;
            Stream = stream;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated kernel.
        /// </summary>
        public Kernel Kernel { get; }

        /// <summary>
        /// Returns the associated accelerator stream.
        /// </summary>
        public AcceleratorStream Stream { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Emits code for loading a kernel from an instance argument.
        /// </summary>
        /// <param name="argumentIdx">The index of the launcher parameter.</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        public static void EmitLoadKernelArgument(int argumentIdx, ILGenerator ilGenerator)
        {
            Debug.Assert(argumentIdx >= 0);
            Debug.Assert(ilGenerator != null);

            ilGenerator.Emit(OpCodes.Ldarg, argumentIdx);
            ilGenerator.Emit(OpCodes.Call, GetKernelMethod);
        }

        /// <summary>
        /// Emits code for loading an accelerator stream from an instance argument.
        /// </summary>
        /// <param name="argumentIdx">The index of the launcher parameter.</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        public static void EmitLoadAcceleratorStream(int argumentIdx, ILGenerator ilGenerator)
        {
            Debug.Assert(argumentIdx >= 0);
            Debug.Assert(ilGenerator != null);

            ilGenerator.Emit(OpCodes.Ldarg, argumentIdx);
            ilGenerator.Emit(OpCodes.Call, GetStreamMethod);
        }

        #endregion
    }
}
