// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Context.Builder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.OpenCL;
using ILGPU.Backends.PTX;
using ILGPU.IR.Intrinsics;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using System;
using System.Diagnostics;

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
            /// Enables all supported accelerators and puts the context into
            /// auto-assertion mode via <see cref="AutoAssertions"/>.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder Default() => AllAccelerators().AutoAssertions();

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
            public Builder IOOperations()
            {
                EnableIOOperations = true;
                return DebugSymbols(DebugSymbolsMode.Basic);
            }

            /// <summary>
            /// Turns on the internal IR verifier.
            /// </summary>
            /// <returns>The current builder instance.</returns>
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
            public Builder AutoAssertions() => Debugger.IsAttached ? Assertions() : this;

            /// <summary>
            /// Automatically enables all IO operations as soon as a debugger is attached.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder AutoIOOperations() =>
                Debugger.IsAttached ? IOOperations() : this;

            /// <summary>
            /// Automatically switches to <see cref="Debug()"/> mode if a debugger is
            /// attached.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder AutoDebug() => Debugger.IsAttached ? Debug() : this;

            /// <summary>
            /// Sets the optimization level to <see cref="OptimizationLevel.Debug"/>,
            /// calls <see cref="Assertions()"/> to turn on all debug assertion checks
            /// and calls <see cref="IOOperations"/> to turn on all debug outputs.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder Debug() =>
                Optimize(OptimizationLevel.Debug).
                Assertions().
                IOOperations().
                DebugSymbols(DebugSymbolsMode.Kernel);

            /// <summary>
            /// Sets the optimization level to <see cref="OptimizationLevel.Release"/>.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public Builder Release() =>
                Optimize(OptimizationLevel.Release);

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
        public static Builder Create() => new Builder();

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
        public static Context CreateDefaultAutoAssertions() =>
            Create(builder => builder.Default().AutoAssertions());

        /// <summary>
        /// Creates a default context by invoking the <see cref="Builder.Default()"/> and
        /// the <see cref="Builder.AutoDebug()"/> methods on the temporary builder
        /// instance.
        /// </summary>
        /// <returns>The created context.</returns>
        public static Context CreateDefaultAutoDebug() =>
            Create(builder => builder.Default().AutoDebug());

        #endregion
    }
}
