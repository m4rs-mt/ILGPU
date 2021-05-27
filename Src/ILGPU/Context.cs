// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Context.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.IL;
using ILGPU.Frontend;
using ILGPU.Frontend.DebugInformation;
using ILGPU.IR;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPU
{
    /// <summary>
    /// Represents an abstract context extensions that can store additional data.
    /// </summary>
    public abstract class ContextExtension : CachedExtension { }

    /// <summary>
    /// Represents the main ILGPU context.
    /// </summary>
    /// <remarks>Members of this class are thread-safe.</remarks>
    public sealed partial class Context : CachedExtensionBase<ContextExtension>
    {
        #region Constants

        /// <summary>
        /// The name of the dynamic runtime assembly.
        /// </summary>
        public const string RuntimeAssemblyName = RuntimeSystem.AssemblyName;

        /// <summary>
        /// Represents the general ILGPU assembly name.
        /// </summary>
        public const string AssemblyName = "ILGPU";

        /// <summary>
        /// Represents the general ILGPU assembly module name.
        /// </summary>
        public const string FullAssemblyModuleName = AssemblyName + ".dll";

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents an enumerable collection of all devices of a specific type.
        /// </summary>
        /// <typeparam name="TDevice">The device class type.</typeparam>
        public readonly ref struct DeviceCollection<TDevice>
            where TDevice : Device
        {
            #region Nested Types

            /// <summary>
            /// Returns an enumerator to enumerate all registered devices of the parent
            /// type.
            /// </summary>
            public ref struct Enumerator
            {
                private List<Device>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new use enumerator.
                /// </summary>
                /// <param name="devices">The list of all devices.</param>
                internal Enumerator(List<Device> devices)
                {
                    enumerator = devices.GetEnumerator();
                }

                /// <summary>
                /// Returns the current use.
                /// </summary>
                public TDevice Current => enumerator.Current as TDevice;

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() => enumerator.MoveNext();
            }

            #endregion

            private readonly List<Device> devices;

            /// <summary>
            /// Constructs a new device collection.
            /// </summary>
            /// <param name="deviceList">The list of all devices.</param>
            internal DeviceCollection(List<Device> deviceList)
            {
                devices = deviceList;
            }

            /// <summary>
            /// Returns the device type of this collection.
            /// </summary>
            public readonly AcceleratorType AcceleratorType =>
                DeviceTypeAttribute.GetAcceleratorType(typeof(TDevice));

            /// <summary>
            /// Returns the number of registered devices.
            /// </summary>
            public readonly int Count => devices.Count;

            /// <summary>
            /// Returns the i-th device.
            /// </summary>
            /// <param name="deviceIndex">
            /// The relative device index of the specific device type. 0 here refers to
            /// the first device of this type, 1 to the second, etc.
            /// </param>
            /// <returns>The i-th device.</returns>
            public readonly TDevice this[int deviceIndex]
            {
                get
                {
                    if (deviceIndex < 0)
                        throw new ArgumentOutOfRangeException(nameof(deviceIndex));
                    return deviceIndex < Count
                        ? devices[deviceIndex] as TDevice
                        : throw new NotSupportedException(
                            RuntimeErrorMessages.NotSupportedTargetAccelerator);
                }
            }

            /// <summary>
            /// Returns an enumerator to enumerate all uses devices.
            /// </summary>
            /// <returns>The enumerator.</returns>
            public readonly Enumerator GetEnumerator() => new Enumerator(devices);
        }

        #endregion

        #region Static

        /// <summary>
        /// Returns the current ILGPU version.
        /// </summary>
        public static string Version { get; }

        /// <summary>
        /// Represents an aggressive inlining attribute builder.
        /// </summary>
        /// <remarks>Note that this attribute will not enforce inlining.</remarks>
        internal static CustomAttributeBuilder InliningAttributeBuilder { get; }

        /// <summary>
        /// Initializes all static context attributes.
        /// </summary>
        static Context()
        {
            var versionString = Assembly.GetExecutingAssembly().
                GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            int offset = 0;
            for (int i = 0; i < 3; ++i)
                offset = versionString.IndexOf('.', offset + 1);
            Version = versionString.Substring(0, offset);

            InliningAttributeBuilder = new CustomAttributeBuilder(
                typeof(MethodImplAttribute).GetConstructor(
                    new Type[] { typeof(MethodImplOptions) }),
                new object[] { MethodImplOptions.AggressiveInlining });
        }

        #endregion

        #region Events

        /// <summary>
        /// Will be called when a new accelerator has been created.
        /// </summary>
        public event EventHandler<Accelerator> AcceleratorCreated;

        #endregion

        #region Instance

        /// <summary>
        /// The global counter for all method handles.
        /// </summary>
        private long methodHandleCounter;

        /// <summary>
        /// The synchronization semaphore for frontend workers.
        /// </summary>
        private readonly SemaphoreSlim codeGenerationSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// An internal mapping of accelerator types to individual devices.
        /// </summary>
        private readonly Dictionary<AcceleratorType, List<Device>> deviceMapping;

        /// <summary>
        /// Constructs a new ILGPU main context
        /// </summary>
        /// <param name="builder">The parent builder instance.</param>
        /// <param name="devices">The array of accelerator descriptions.</param>
        internal Context(
            Builder builder,
            ImmutableArray<Device> devices)
        {
            InstanceId = InstanceId.CreateNew();
            TargetPlatform = Backend.RuntimePlatform;
            RuntimeSystem = new RuntimeSystem();
            Properties = builder.InstantiateProperties();

            // Initialize verifier
            Verifier = builder.EnableVerifier ? Verifier.Instance : Verifier.Empty;

            // Initialize main contexts
            TypeContext = new IRTypeContext(this);
            IRContext = new IRContext(this);

            // Initialize intrinsic manager
            IntrinsicManager = builder.IntrinsicManager;

            // Create frontend
            DebugInformationManager frontendDebugInformationManager =
                Properties.DebugSymbolsMode > DebugSymbolsMode.Disabled
                ? DebugInformationManager
                : null;

            ILFrontend = builder.EnableParallelCodeGenerationInFrontend
                ? new ILFrontend(this, frontendDebugInformationManager)
                : new ILFrontend(this, frontendDebugInformationManager, 1);

            // Create default IL backend
            DefautltILBackend = new DefaultILBackend(this);

            // Initialize default transformer
            ContextTransformer = Optimizer.CreateTransformer(
                Properties.OptimizationLevel,
                TransformerConfiguration.Transformed,
                Properties.InliningMode);

            // Initialize all devices
            Devices = devices;
            if (devices.IsDefaultOrEmpty)
            {
                // Add a default CPU device
                Devices = ImmutableArray.Create<Device>(CPUDevice.Default);
            }

            // Create a mapping
            deviceMapping = new Dictionary<AcceleratorType, List<Device>>(Devices.Length);
            foreach (var device in Devices)
            {
                if (!deviceMapping.TryGetValue(device.AcceleratorType, out var devs))
                {
                    devs = new List<Device>(8);
                    deviceMapping.Add(device.AcceleratorType, devs);
                }
                devs.Add(device);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current instance id.
        /// </summary>
        internal InstanceId InstanceId { get; }

        /// <summary>
        /// All registered devices.
        /// </summary>
        public ImmutableArray<Device> Devices { get; }

        /// <summary>
        /// Returns the current target platform.
        /// </summary>
        public TargetPlatform TargetPlatform { get; }

        /// <summary>
        /// Returns the associated runtime system class.
        /// </summary>
        public RuntimeSystem RuntimeSystem { get; }

        /// <summary>
        /// Returns true if this context uses assertion checks.
        /// </summary>
        public ContextProperties Properties { get; }

        #endregion

        #region Internal Properties

        /// <summary>
        /// Returns the main IR context.
        /// </summary>
        internal IRContext IRContext { get; }

        /// <summary>
        /// Returns the associated IL frontend.
        /// </summary>
        internal ILFrontend ILFrontend { get; }

        /// <summary>
        /// Returns the associated default IL backend.
        /// </summary>
        internal ILBackend DefautltILBackend { get; }

        /// <summary>
        /// Returns the internal verifier instance.
        /// </summary>
        internal Verifier Verifier { get; }

        /// <summary>
        /// Returns the main debug-information manager.
        /// </summary>
        internal DebugInformationManager DebugInformationManager { get; } =
            new DebugInformationManager();

        /// <summary>
        /// Returns the main type context.
        /// </summary>
        internal IRTypeContext TypeContext { get; }

        /// <summary>
        /// Returns the default context transformer.
        /// </summary>
        internal Transformer ContextTransformer { get; }

        /// <summary>
        /// Returns the underlying intrinsic manager.
        /// </summary>
        internal IntrinsicImplementationManager IntrinsicManager { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a specific device of the given type using a relative device index.
        /// </summary>
        /// <typeparam name="TDevice">The device class type.</typeparam>
        /// <param name="deviceIndex">
        /// The relative device index of the specific device type. 0 here refers to the
        /// first device of this type, 1 to the second, etc.
        /// </param>
        /// <returns>The device instance.</returns>
        public TDevice GetDevice<TDevice>(int deviceIndex)
            where TDevice : Device =>
            GetDevices<TDevice>()[deviceIndex];

        /// <summary>
        /// Gets all devices of the given type.
        /// </summary>
        /// <typeparam name="TDevice">The device class type.</typeparam>
        /// <returns>All device instances.</returns>
        public DeviceCollection<TDevice> GetDevices<TDevice>()
            where TDevice : Device
        {
            var type = DeviceTypeAttribute.GetAcceleratorType(typeof(TDevice));
            return deviceMapping.TryGetValue(type, out var devices)
                ? new DeviceCollection<TDevice>(devices)
                : new DeviceCollection<TDevice>(new List<Device>());
        }

        /// <summary>
        /// Attempts to return the most optimal single device.
        /// </summary>
        /// <param name="PreferCPU">Always returns CPU device 0.</param>
        /// <returns>Selected device.</returns>
        public Device GetBestDevice(bool PreferCPU = false)
        {
            if(PreferCPU)
            {
                return deviceMapping.TryGetValue(AcceleratorType.CPU, out var devices)
                    ? devices.First()
                    : throw new NotSupportedException(
                            RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            var sorted = Devices.Sort((d1, d2) => d1.MemorySize.CompareTo(d2.MemorySize))
                .Where(d => d.AcceleratorType != AcceleratorType.CPU);

            if(sorted.Count() < 0)
            {
                return deviceMapping.TryGetValue(AcceleratorType.CPU, out var devices)
                    ? devices.First()
                    : throw new NotSupportedException(
                            RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
            else
            {
                return sorted.First();
            }
        }

        /// <summary>
        /// Attempts to return the most optimal set of devices.
        /// </summary>
        /// <param name="PreferCPU">Always returns first CPU device.</param>
        /// <param name="MatchingDevicesOnly">Only returns matching devices.</param>
        /// <returns>Selected devices.</returns>
        public IEnumerable<Device> GetBestDevices(bool PreferCPU = false, bool MatchingDevicesOnly = false)
        {
            if (PreferCPU)
            {
                return deviceMapping.TryGetValue(AcceleratorType.CPU, out var devices)
                    ? devices
                    : throw new NotSupportedException(
                            RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }

            var sorted = Devices.Sort((d1, d2) => d1.MemorySize.CompareTo(d2.MemorySize))
                                .Where(d => d.AcceleratorType != AcceleratorType.CPU);

            if (sorted.Count() < 0)
            {
                return deviceMapping.TryGetValue(AcceleratorType.CPU, out var devices)
                    ? devices
                    : throw new NotSupportedException(
                            RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
            else
            {
                if(MatchingDevicesOnly)
                {
                    return sorted.Where(
                        d => d.AcceleratorType == sorted.First().AcceleratorType);
                }
                else
                {
                    return sorted;
                }
            }
        }

        /// <summary>
        /// Creates a new unique method handle.
        /// </summary>
        /// <returns>A new unique method handle.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long CreateMethodHandle() =>
            Interlocked.Add(ref methodHandleCounter, 1);

        /// <summary>
        /// Releases the internal code-generation lock.
        /// </summary>
        internal void ReleaseCodeGenerationLock() =>
            codeGenerationSemaphore.Release();

        /// <summary>
        /// Begins a new code generation phase.
        /// </summary>
        /// <returns>The new code generation phase.</returns>
        public ContextCodeGenerationPhase BeginCodeGeneration() =>
            BeginCodeGeneration(IRContext);

        /// <summary>
        /// Begins a new code generation phase.
        /// </summary>
        /// <returns>The new code generation phase.</returns>
        public ContextCodeGenerationPhase BeginCodeGeneration(IRContext irContext)
        {
            if (irContext == null)
                throw new ArgumentNullException(nameof(irContext));
            codeGenerationSemaphore.Wait();
            return new ContextCodeGenerationPhase(this, irContext);
        }

        /// <summary>
        /// Begins a new code generation phase (asynchronous).
        /// </summary>
        /// <returns>The new code generation phase.</returns>
        public Task<ContextCodeGenerationPhase> BeginCodeGenerationAsync() =>
            Task.Run(new Func<ContextCodeGenerationPhase>(BeginCodeGeneration));

        /// <summary>
        /// Begins a new code generation phase (asynchronous).
        /// </summary>
        /// <returns>The new code generation phase.</returns>
        public Task<ContextCodeGenerationPhase> BeginCodeGenerationAsync(
            IRContext irContext) =>
            irContext == null
            ? throw new ArgumentNullException(nameof(irContext))
            : Task.Run(() => BeginCodeGeneration(irContext));

        /// <summary>
        /// Clears internal caches. However, this does not affect individual accelerator
        /// caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        /// <remarks>
        /// This method is not thread-safe.
        /// </remarks>
        public override void ClearCache(ClearCacheMode mode)
        {
            IRContext.ClearCache(mode);
            TypeContext.ClearCache(mode);
            DebugInformationManager.ClearCache(mode);
            DefautltILBackend.ClearCache(mode);
            RuntimeSystem.ClearCache(mode);

            base.ClearCache(mode);
        }

        /// <summary>
        /// Raises the corresponding <see cref="AcceleratorCreated"/> event.
        /// </summary>
        /// <param name="accelerator">The new accelerator.</param>
        internal void OnAcceleratorCreated(Accelerator accelerator) =>
            AcceleratorCreated?.Invoke(this, accelerator);

        #endregion

        #region Enumerable

        /// <summary>
        /// Returns an accelerator description enumerator.
        /// </summary>
        public ImmutableArray<Device>.Enumerator GetEnumerator() =>
            Devices.GetEnumerator();

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                codeGenerationSemaphore.Dispose();
                IRContext.Dispose();

                ILFrontend.Dispose();
                DefautltILBackend.Dispose();

                DebugInformationManager.Dispose();
                TypeContext.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// A single code generation phase.
    /// </summary>
    public sealed class ContextCodeGenerationPhase : DisposeBase
    {
        #region Instance

        /// <summary>
        /// Constructs a new code generation phase.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="irContext">The current IR context.</param>
        internal ContextCodeGenerationPhase(
            Context context,
            IRContext irContext)
        {
            Debug.Assert(context != null, "Invalid context");
            Debug.Assert(irContext != null, "Invalid IR context");
            Context = context;
            IRContext = irContext;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the main context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the current IR context.
        /// </summary>
        public IRContext IRContext { get; }

        /// <summary>
        /// Returns true if the code generation has failed.
        /// </summary>
        public bool IsFaulted => Context.ILFrontend.IsFaulted;

        /// <summary>
        /// Returns the exception from code generation failure.
        /// </summary>
        public Exception LastException => Context.ILFrontend.LastException;

        #endregion

        #region Methods

        /// <summary>
        /// Starts a new frontend code-generation phase.
        /// </summary>
        /// <returns>The frontend code-generation phase.</returns>
        public CodeGenerationPhase BeginFrontendCodeGeneration() =>
            Context.ILFrontend.BeginCodeGeneration(IRContext);

        /// <summary>
        /// Optimizes the IR.
        /// </summary>
        public void Optimize() => IRContext.Optimize();

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Context.ReleaseCodeGenerationLock();
            base.Dispose(disposing);
        }

        #endregion
    }
}
