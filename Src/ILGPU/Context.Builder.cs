// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Context.Builder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.OpenCL;
using ILGPU.Backends.PTX;
using ILGPU.Backends.Velocity;
using ILGPU.IR.Intrinsics;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ILGPU
{
    partial class Context
    {
        #region Builder

        /// <summary>
        /// A context builder class.
        /// </summary>
        /// <remarks>
        /// If no accelerators will be added to this builder, the resulting context
        /// will use the default CPU accelerator
        /// <see cref="CPUDevice.Default"/>.
        /// </remarks>
        public sealed class Builder : ContextProperties
        {
            #region Instance

            /// <summary>
            /// Constructs a new builder instance.
            /// </summary>
            internal Builder()
            {
                // Register intrinsics
                PTXIntrinsics.Register(IntrinsicManager);
                CLIntrinsics.Register(IntrinsicManager);
                VelocityIntrinsics.Register(IntrinsicManager);

                // Setups an initial debug configuration
                DebugConfig(enableAssertions: true);
            }

            #endregion

            #region Properties

            /// <summary>
            /// All accelerator descriptions that have been registered.
            /// </summary>
            internal DeviceRegistry DeviceRegistry { get; } = new DeviceRegistry();

            /// <summary>
            /// Returns the underlying intrinsic manager.
            /// </summary>
            internal IntrinsicImplementationManager IntrinsicManager { get; } =
                new IntrinsicImplementationManager();

            #endregion

            #region Methods

            /// <summary>
            /// Enables all supported accelerators.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder Default() => AllAccelerators();

            /// <summary>
            /// Enables all supported accelerators.
            /// </summary>
            /// <remarks>
            /// Note that this function calls
            /// <see cref="CPUContextExtensions.DefaultCPU(Builder)"/> only to ensure
            /// that there is only one CPU accelerator by default.
            /// </remarks>
            /// <returns>The current builder instance.</returns>
            public Builder AllAccelerators() => this.DefaultCPU().OpenCL().Cuda();

            /// <summary>
            /// Enables all accelerators that fulfill the given predicate.
            /// </summary>
            /// <param name="predicate">
            /// The predicate to include a given accelerator description.
            /// </param>
            /// <returns>The current builder instance.</returns>
            public Builder AllAccelerators(
                Predicate<Device> predicate) =>
                this.CPU(predicate).OpenCL(predicate).Cuda(predicate);

            /// <summary>
            /// Specifies the inlining mode.
            /// </summary>
            /// <param name="inliningMode">The inlining mode to use.</param>
            /// <returns>The current builder instance.</returns>
            public Builder Inlining(InliningMode inliningMode)
            {
                InliningMode = inliningMode;
                return this;
            }

            /// <summary>
            /// Specifies the debug symbol mode to use.
            /// </summary>
            /// <param name="debugSymbolsMode">The symbols mode to use.</param>
            /// <returns>The current builder instance.</returns>
            [Obsolete("Use DebugConfig() instead to configure debugging")]
            public Builder DebugSymbols(DebugSymbolsMode debugSymbolsMode) =>
                debugSymbolsMode < DebugSymbolsMode
                ? throw new InvalidOperationException(
                    RuntimeErrorMessages.InvalidDowngradeOfDebugSymbols)
                : this;

            /// <summary>
            /// Turns on all assertion checks (including out-of-bounds checks) for view
            /// and array accesses.
            /// </summary>
            /// <remarks>
            /// Note that calling this function automatically switches the debug mode
            /// to at least <see cref="DebugSymbolsMode.Basic"/>.
            /// </remarks>
            /// <returns>The current builder instance.</returns>
            [Obsolete("Use DebugConfig() instead to configure debugging")]
            public Builder Assertions()
            {
                EnableAssertions = true;
                return DebugSymbols(DebugSymbolsMode.Basic);
            }

            /// <summary>
            /// Turns on all IO operations checks.
            /// accesses.
            /// </summary>
            /// <remarks>
            /// Note that calling this function automatically switches the debug mode
            /// to at least <see cref="DebugSymbolsMode.Basic"/>.
            /// </remarks>
            /// <returns>The current builder instance.</returns>
            [Obsolete("Use DebugConfig() instead to configure debugging")]
            public Builder IOOperations()
            {
                EnableIOOperations = true;
                return DebugSymbols(DebugSymbolsMode.Basic);
            }

            /// <summary>
            /// Turns on the internal IR verifier.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            [Obsolete("Use DebugConfig() instead to configure debugging")]
            public Builder Verify()
            {
                EnableVerifier = true;
                return this;
            }

            /// <summary>
            /// Enables fast generation of fast math methods using the math mode provided.
            /// </summary>
            /// <param name="mathMode">The math mode to use.</param>
            /// <returns>The current builder instance.</returns>
            public Builder Math(MathMode mathMode)
            {
                MathMode = mathMode;
                return this;
            }

            /// <summary>
            /// Specifies how to deal with static fields.
            /// </summary>
            /// <param name="fieldMode">The static field mode to use.</param>
            /// <returns>The current builder instance.</returns>
            public Builder StaticFields(StaticFieldMode fieldMode)
            {
                StaticFieldMode = fieldMode;
                return this;
            }

            /// <summary>
            /// Specifies how to deal with arrays.
            /// </summary>
            /// <param name="arrayMode">The array mode to use.</param>
            /// <returns>The current builder instance.</returns>
            public Builder Arrays(ArrayMode arrayMode)
            {
                ArrayMode = arrayMode;
                return this;
            }

            /// <summary>
            /// Specifies the caching mode for the context instance.
            /// </summary>
            /// <param name="cachingMode">The caching mode to use.</param>
            /// <returns>The current builder instance.</returns>
            public Builder Caching(CachingMode cachingMode)
            {
                CachingMode = cachingMode;
                return this;
            }

            /// <summary>
            /// Automatically enables all assertions as soon as a debugger is attached.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            [Obsolete("Use DebugConfig() instead to configure debugging")]
            public Builder AutoAssertions() => Debugger.IsAttached ? Assertions() : this;

            /// <summary>
            /// Automatically enables all IO operations as soon as a debugger is attached.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            [Obsolete("Use DebugConfig() instead to configure debugging")]
            public Builder AutoIOOperations() =>
                Debugger.IsAttached ? IOOperations() : this;

            /// <summary>
            /// Configures debug mode while taking the given settings into account.
            /// Note that debugging of <see cref="OptimizationLevel.O1"/> and
            /// <see cref="OptimizationLevel.O2"/> can only be enabled by setting
            /// <paramref name="forceDebuggingOfOptimizedKernels"/> to true.
            /// </summary>
            /// <param name="enableAssertions">True to enable debug assertions.</param>
            /// <param name="enableIOOperations">True to enable IO operations.</param>
            /// <param name="debugSymbolsMode">Configure use of debug symbols.</param>
            /// <param name="forceDebuggingOfOptimizedKernels">
            /// True to force the use of debug configuration in O1 and O2 builds.
            /// </param>
            /// <param name="enableIRVerifier">
            /// True to turn on the internal IR verifier.
            /// </param>
            /// <returns>The current builder instance.</returns>
            public Builder DebugConfig(
                bool enableAssertions = false,
                bool enableIOOperations = false,
                DebugSymbolsMode debugSymbolsMode = DebugSymbolsMode.Auto,
                bool forceDebuggingOfOptimizedKernels = false,
                bool enableIRVerifier = false)
            {
                if (debugSymbolsMode < DebugSymbolsMode)
                {
                    throw new InvalidOperationException(
                        RuntimeErrorMessages.InvalidDowngradeOfDebugSymbols);
                }

                EnableAssertions = enableAssertions;
                DebugSymbolsMode = debugSymbolsMode;
                EnableIOOperations = enableIOOperations;
                ForceDebuggingOfOptimizedKernels = forceDebuggingOfOptimizedKernels;
                EnableVerifier = enableIRVerifier;
                return this;
            }

            /// <summary>
            /// Sets the optimization level to <see cref="OptimizationLevel.Debug"/>,
            /// calls <see cref="Assertions()"/> to turn on all debug assertion checks
            /// and calls <see cref="IOOperations"/> to turn on all debug outputs.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder Debug() => Optimize(OptimizationLevel.Debug);

            /// <summary>
            /// Automatically switches to <see cref="Debug()"/> mode if a debugger is
            /// attached.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder AutoDebug() => Debugger.IsAttached ? Debug() : this;

            /// <summary>
            /// Sets the optimization level to <see cref="OptimizationLevel.Release"/>.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder Release() => Optimize(OptimizationLevel.Release);

            /// <summary>
            /// Specifies the optimization level.
            /// </summary>
            /// <param name="level">The optimization level to use.</param>
            /// <returns>The current builder instance.</returns>
            public Builder Optimize(OptimizationLevel level)
            {
                OptimizationLevel = level;
                return this;
            }

            /// <summary>
            /// Specifies the page locking mode.
            /// </summary>
            /// <param name="mode">The locking mode to use.</param>
            /// <returns>The current builder instance.</returns>
            public Builder PageLocking(PageLockingMode mode)
            {
                PageLockingMode = mode;
                return this;
            }

            /// <summary>
            /// Turns on profiling of all streams.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder Profiling()
            {
                EnableProfiling = true;
                return this;
            }

            /// <summary>
            /// Turns on LibDevice support.
            /// Automatically detects the CUDA SDK location.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder LibDevice()
            {
                // Find the CUDA installation path.
                var cudaEnvName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "CUDA_PATH"
                    : "CUDA_HOME";
                var cudaPath = Environment.GetEnvironmentVariable(cudaEnvName);
                if (string.IsNullOrEmpty(cudaPath))
                {
                    throw new NotSupportedException(string.Format(
                        RuntimeErrorMessages.NotSupportedLibDeviceEnvironmentVariable,
                        cudaEnvName));
                }
                var nvvmRoot = Path.Combine(cudaPath, "nvvm");

                // Find the NVVM DLL.
                var nvvmBinName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "bin"
                    : "lib64";
                var nvvmBinDir = Path.Combine(nvvmRoot, nvvmBinName);
                var nvvmSearchPattern =
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "nvvm64*.dll"
                    : "libnvvm*.so";
                var nvvmFiles = Directory.EnumerateFiles(nvvmBinDir, nvvmSearchPattern);
                LibNvvmPath = nvvmFiles.FirstOrDefault()
                    ?? throw new NotSupportedException(string.Format(
                        RuntimeErrorMessages.NotSupportedLibDeviceNotFoundNvvmDll,
                        nvvmBinDir));

                // Find the LibDevice Bitcode.
                var libDeviceDir = Path.Combine(nvvmRoot, "libdevice");
                var libDeviceFiles = Directory.EnumerateFiles(
                    libDeviceDir,
                    "libdevice.*.bc");
                LibDevicePath = libDeviceFiles.FirstOrDefault()
                    ?? throw new NotSupportedException(string.Format(
                        RuntimeErrorMessages.NotSupportedLibDeviceNotFoundBitCode,
                        libDeviceDir));

                return this;
            }

            /// <summary>
            /// Turns on LibDevice support.
            /// Explicitly specifies the LibDevice location.
            /// </summary>
            /// <param name="libNvvmPath">Path to LibNvvm DLL.</param>
            /// <param name="libDevicePath">Path to LibDevice bitcode.</param>
            /// <returns>The current builder instance.</returns>
            public Builder LibDevice(string libNvvmPath, string libDevicePath)
            {
                LibNvvmPath = libNvvmPath;
                LibDevicePath = libDevicePath;
                return this;
            }

            /// <summary>
            /// Converts this builder instance into a context instance.
            /// </summary>
            /// <returns>The created context instance.</returns>
            public Context ToContext() => new Context(
                this,
                DeviceRegistry.ToImmutable());

            #endregion

            #region Extensibility

            /// <summary>
            /// Sets an extension property.
            /// </summary>
            /// <typeparam name="T">The element type.</typeparam>
            /// <param name="key">The key.</param>
            /// <param name="value">The value to store.</param>
            public new void SetExtensionProperty<T>(string key, T value)
                where T : struct =>
                base.SetExtensionProperty(key, value);

            #endregion
        }

        /// <summary>
        /// Creates a new builder instance.
        /// </summary>
        /// <returns>The builder instance.</returns>
        public static Builder Create() => new();

        /// <summary>
        /// Creates a new context instance.
        /// </summary>
        /// <param name="buildingCallback">The user defined builder callback.</param>
        /// <returns>The created context.</returns>
        public static Context Create(Action<Builder> buildingCallback)
        {
            if (buildingCallback is null)
                throw new ArgumentNullException(nameof(buildingCallback));

            var builder = Create();
            buildingCallback(builder);
            return builder.ToContext();
        }

        /// <summary>
        /// Creates a default context by invoking the <see cref="Builder.Default()"/>
        /// method on the temporary builder instance.
        /// </summary>
        /// <returns>The created context.</returns>
        public static Context CreateDefault() => Create(builder => builder.Default());

        /// <summary>
        /// Creates a default context by invoking the <see cref="Builder.Default()"/> and
        /// the <see cref="Builder.AutoAssertions()"/> methods on the temporary builder
        /// instance.
        /// </summary>
        /// <returns>The created context.</returns>
        [Obsolete("Use CreateDefaultAutoDebug instead")]
        public static Context CreateDefaultAutoAssertions() =>
            CreateDefaultAutoDebug(enableAssertions: true);

        /// <summary>
        /// Creates a default context by invoking the <see cref="Builder.Default()"/> and
        /// the <see cref="Builder.AutoDebug()"/> methods on the temporary builder
        /// instance while offering the ability to configure an initial debug config.
        /// </summary>
        /// <param name="enableAssertions">True to enable debug assertions.</param>
        /// <param name="enableIOOperations">True to enable IO operations.</param>
        /// <param name="debugSymbolsMode">Configure use of debug symbols.</param>
        /// <param name="forceDebuggingOfOptimizedKernels">
        /// True to force the use of debug configuration in O1 and O2 builds.
        /// </param>
        /// <param name="enableIRVerifier">
        /// True to turn on the internal IR verifier.
        /// </param>
        /// <returns>The created context.</returns>
        public static Context CreateDefaultAutoDebug(
            bool enableAssertions = false,
            bool enableIOOperations = false,
            DebugSymbolsMode debugSymbolsMode = DebugSymbolsMode.Auto,
            bool forceDebuggingOfOptimizedKernels = false,
            bool enableIRVerifier = false) =>
            Create(builder => builder
                .Default()
                .DebugConfig(
                    enableAssertions,
                    enableIOOperations,
                    debugSymbolsMode,
                    forceDebuggingOfOptimizedKernels,
                    enableIRVerifier)
                .AutoDebug());

        #endregion
    }
}
