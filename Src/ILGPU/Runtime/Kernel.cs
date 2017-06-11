// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Kernel.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents the base class for all runtime kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class Kernel : DisposeBase
    {
        #region Constants

        internal const int KernelInstanceParamIdx = 0;
        internal const int KernelStreamParamIdx = 1;
        internal const int KernelParamDimensionIdx = 2;
        internal const int KernelParameterOffset = KernelParamDimensionIdx + 1;

        #endregion

        #region Instance

        /// <summary>
        /// Stores the associated kernel launcher.
        /// </summary>
        private MethodInfo launcher;

        /// <summary>
        /// Stores the created implicit-stream kernel launcher.
        /// </summary>
        private MethodInfo implicitStreamLauncher;

        /// <summary>
        /// Constructs a new kernel.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="compiledKernel">The source kernel.</param>
        /// <param name="launcher">The launcher method for the given kernel.</param>
        protected Kernel(
            Accelerator accelerator,
            CompiledKernel compiledKernel,
            MethodInfo launcher)
        {
            Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
            CompiledKernel = compiledKernel ?? throw new ArgumentNullException(nameof(compiledKernel));
            Launcher = launcher;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context => CompiledKernel.Context;

        /// <summary>
        /// Returns the compiled kernel.
        /// </summary>
        public CompiledKernel CompiledKernel { get; }

        /// <summary>
        /// Returns the associated kernel launcher.
        /// </summary>
        public MethodInfo Launcher
        {
            get { return launcher; }
            internal set
            {
                launcher = value;
                if (value != null)
                    implicitStreamLauncher = CreateImplicitStreamLauncher();
            }
        }

        /// <summary>
        /// Returns the default stream of the associated accelerator.
        /// </summary>
        internal AcceleratorStream DefaultStream => Accelerator.DefaultStream;

        #endregion

        #region Methods

        /// <summary>
        /// Builds a new impicit-stream launcher for the current launcher.
        /// </summary>
        /// <returns>The constructed implicit-stream launcher.</returns>
        private MethodInfo CreateImplicitStreamLauncher()
        {
            var @params = Launcher.GetParameters();
            var launcherParamTypes = new Type[@params.Length - KernelParamDimensionIdx + 1];
            launcherParamTypes[0] = typeof(ImplicitKernelLauncherArgument);
            for (int i = 1, e = launcherParamTypes.Length; i < e; ++i)
                launcherParamTypes[i] = @params[i + KernelParamDimensionIdx - 1].ParameterType;
            var method = new DynamicMethod(
                Launcher.Name + "ImplicitStreamLauncher",
                typeof(void),
                launcherParamTypes,
                typeof(ImplicitKernelLauncherArgument),
                true);

            var ilGenerator = method.GetILGenerator();

            // Load kernel
            ImplicitKernelLauncherArgument.EmitLoadKernelArgument(
                0,
                ilGenerator);

            // Load accelerator stream
            ImplicitKernelLauncherArgument.EmitLoadAcceleratorStream(
                0,
                ilGenerator);

            // Load actual params
            for (int i = 1, e = launcherParamTypes.Length; i < e; ++i)
                ilGenerator.Emit(OpCodes.Ldarg, i);

            // Call actual launcher
            ilGenerator.Emit(OpCodes.Call, Launcher);

            // Return
            ilGenerator.Emit(OpCodes.Ret);

            return method;
        }

        /// <summary>
        /// Creates a launcher delegate for this kernel.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <returns>The created delegate.</returns>
        /// <remarks>Note that the first argument is the accelerator stream.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TDelegate CreateLauncherDelegate<TDelegate>()
            where TDelegate : class
        {
            return (Launcher.CreateDelegate(typeof(TDelegate), this) as object) as TDelegate;
        }

        /// <summary>
        /// Creates a launcher delegate for this kernel using the default
        /// accelerator stream of the associated accelerator.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <returns>The created delegate.</returns>
        /// <remarks>This method is a simple wrapper for <see cref="CreateStreamLauncherDelegate{TDelegate}(AcceleratorStream)"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TDelegate CreateStreamLauncherDelegate<TDelegate>()
            where TDelegate : class
        {
            return CreateStreamLauncherDelegate<TDelegate>(DefaultStream);
        }

        /// <summary>
        /// Creates a launcher delegate for this kernel while binding the accelerator-stream
        /// parameter to the given stream.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="stream">The accelerator stream to use.</param>
        /// <returns>The created delegate.</returns>
        /// <remarks>Note that the resulting delegate will not accept an additional stream argument.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TDelegate CreateStreamLauncherDelegate<TDelegate>(AcceleratorStream stream)
            where TDelegate : class
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            Debug.Assert(implicitStreamLauncher != null, "Invalid implicit stream launcher");
            return (implicitStreamLauncher.CreateDelegate(typeof(TDelegate),
                new ImplicitKernelLauncherArgument(this, stream)) as object) as TDelegate;
        }

        /// <summary>
        /// Invokes the associated launcher via reflection.
        /// </summary>
        /// <typeparam name="T">The index type T.</typeparam>
        /// <param name="dimension">The grid dimension.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="args">The kernel arguments.</param>
        /// <param name="dynSharedMemArraySizes">The number of elements for each dynamically-sized shared memory array.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvokeLauncher<T>(
            T dimension,
            AcceleratorStream stream,
            object[] args,
            int[] dynSharedMemArraySizes)
            where T : IIndex
        {
            var numSharedMemVars = dynSharedMemArraySizes == null ? 0 : dynSharedMemArraySizes.Length;
            var entryPoint = CompiledKernel.EntryPoint;
            if (entryPoint.NumDynamicallySizedSharedMemoryVariables != numSharedMemVars)
                throw new ArgumentException(RuntimeErrorMessages.InvalidNumberOfDynamicallySharedMemoryVariableArgs);
            if (entryPoint.NumUniformVariables != args.Length)
                throw new ArgumentException(RuntimeErrorMessages.InvalidNumberOfUniformArgs);

            var reflectionArgs = new object[KernelParameterOffset + args.Length + numSharedMemVars];
            reflectionArgs[KernelInstanceParamIdx] = this;
            reflectionArgs[KernelStreamParamIdx] = stream ?? throw new ArgumentNullException(nameof(stream));
            reflectionArgs[KernelParamDimensionIdx] = dimension;
            args.CopyTo(reflectionArgs, KernelParameterOffset);
            if (numSharedMemVars > 0)
                dynSharedMemArraySizes.CopyTo(reflectionArgs, KernelParameterOffset + args.Length);
            Launcher.Invoke(null, reflectionArgs);
        }

        #endregion

        #region Launch Methods

        /// <summary>
        /// Launches the current kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The grid dimension.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Launch<TIndex>(TIndex dimension, params object[] args)
            where TIndex : struct, IIndex
        {
            Launch(dimension, DefaultStream, args);
        }

        /// <summary>
        /// Launches the current kernel with the given arguments.
        /// </summary>
        /// <param name="dimension">The grid dimension.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Launch(int dimension, params object[] args)
        {
            Launch(dimension, DefaultStream, args);
        }

        /// <summary>
        /// Launches the current grouped kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The grid and group dimensions.</param>
        /// <param name="dynSharedMemArraySizes">The number of elements for each dynamically-sized shared memory array.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Launch<TIndex>(TIndex dimension, int[] dynSharedMemArraySizes, params object[] args)
            where TIndex : struct, IGroupedIndex
        {
            Launch(dimension, DefaultStream, dynSharedMemArraySizes, args);
        }

        /// <summary>
        /// Launches the current kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The grid dimension.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Launch<TIndex>(TIndex dimension, AcceleratorStream stream, params object[] args)
            where TIndex : struct, IIndex
        {
            InvokeLauncher(dimension, stream, args, null);
        }

        /// <summary>
        /// Launches the current kernel with the given arguments.
        /// </summary>
        /// <param name="dimension">The grid dimension.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Launch(int dimension, AcceleratorStream stream, params object[] args)
        {
            InvokeLauncher(new Index(dimension), stream, args, null);
        }

        /// <summary>
        /// Launches the current grouped kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="dimension">The grid and group dimensions.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="dynSharedMemArraySizes">The number of elements for each dynamically-sized shared memory array.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Launch<TIndex>(
            TIndex dimension,
            AcceleratorStream stream,
            int[] dynSharedMemArraySizes,
            params object[] args)
            where TIndex : struct, IGroupedIndex
        {
            InvokeLauncher(dimension, stream, args, dynSharedMemArraySizes);
        }

        #endregion
    }
}
