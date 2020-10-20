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
using ILGPU.Backends.OpenCL;
using ILGPU.Backends.PTX;
using ILGPU.Frontend;
using ILGPU.Frontend.DebugInformation;
using ILGPU.IR;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1810:InitializeReferenceTypeStaticFieldsInline",
            Justification = "Complex initialization logic is required in this case")]
        [SuppressMessage(
            "Microsoft.Design",
            "CA1065:DoNotRaiseExceptionsInUnexpectedLocations",
            Justification = "Internal initialization check that should never fail")]
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

            // Ensure initialized runtime
            if (Accelerator.Accelerators.Length < 1)
            {
                throw new TypeLoadException(
                    ErrorMessages.IntrinsicAcceleratorsBroken);
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Will be called when a new accelerator has been created.
        /// </summary>
        public event EventHandler<Accelerator> AcceleratorCreated;

        #endregion

        #region Instance

        private long methodHandleCounter = 0;

        private readonly SemaphoreSlim codeGenerationSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Constructs a new ILGPU main context
        /// </summary>
        public Context()
            : this(DefaultFlags)
        { }

        /// <summary>
        /// Constructs a new ILGPU main context
        /// </summary>
        /// <param name="flags">The context flags.</param>
        public Context(ContextFlags flags)
#if DEBUG
            : this(flags, OptimizationLevel.Debug)
#else
            : this(flags, OptimizationLevel.Release)
#endif
        { }

        /// <summary>
        /// Constructs a new ILGPU main context
        /// </summary>
        /// <param name="optimizationLevel">The optimization level.</param>
        public Context(OptimizationLevel optimizationLevel)
            : this(DefaultFlags, optimizationLevel)
        { }

        /// <summary>
        /// Constructs a new ILGPU main context
        /// </summary>
        /// <param name="optimizationLevel">The optimization level.</param>
        /// <param name="flags">The context flags.</param>
        public Context(ContextFlags flags, OptimizationLevel optimizationLevel)
        {
            // Enable debug information automatically when a debugger is attached
            if (Debugger.IsAttached)
                flags |= DefaultDebug;

            OptimizationLevel = optimizationLevel;
            Flags = flags.Prepare();
            TargetPlatform = Backend.RuntimePlatform;

            // Initialize enhanced PTX backend feature flags
            if (optimizationLevel > OptimizationLevel.O1 &&
                !Flags.HasFlags(ContextFlags.DefaultPTXBackendFeatures))
            {
                Flags |= ContextFlags.EnhancedPTXBackendFeatures;
            }

            // Initialize verifier
            Verifier = flags.HasFlags(ContextFlags.EnableVerifier)
                ? Verifier.Instance
                : Verifier.Empty;

            // Initialize main contexts
            TypeContext = new IRTypeContext(this);
            IRContext = new IRContext(this);

            // Create frontend
            DebugInformationManager frontendDebugInformationManager =
                HasFlags(ContextFlags.EnableDebugSymbols)
                ? DebugInformationManager
                : null;

            ILFrontend = HasFlags(ContextFlags.EnableParallelCodeGenerationInFrontend)
                ? new ILFrontend(frontendDebugInformationManager)
                : new ILFrontend(frontendDebugInformationManager, 1);

            // Create default IL backend
            DefautltILBackend = flags.HasFlags(ContextFlags.SkipCPUCodeGeneration)
                ? new SkipCodeGenerationDefaultILBackend(this)
                : new DefaultILBackend(this);

            // Initialize default transformer
            ContextTransformer = Optimizer.CreateTransformer(
                OptimizationLevel,
                TransformerConfiguration.Transformed,
                Flags);

            // Intrinsics
            IntrinsicManager = new IntrinsicImplementationManager();
            InitIntrinsics();

        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current target platform.
        /// </summary>
        public TargetPlatform TargetPlatform { get; }

        /// <summary>
        /// Returns the main IR context.
        /// </summary>
        public IRContext IRContext { get; }

        /// <summary>
        /// Returns the associated context flags.
        /// </summary>
        public ContextFlags Flags { get; }

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
        /// Returns the optimization level.
        /// </summary>
        public OptimizationLevel OptimizationLevel { get; }

        /// <summary>
        /// Returns the main debug-information manager.
        /// </summary>
        public DebugInformationManager DebugInformationManager { get; } =
            new DebugInformationManager();

        /// <summary>
        /// Returns the main type context.
        /// </summary>
        public IRTypeContext TypeContext { get; }

        /// <summary>
        /// Returns the default context transformer.
        /// </summary>
        public Transformer ContextTransformer { get; }

        /// <summary>
        /// Returns the underlying intrinsic manager.
        /// </summary>
        public IntrinsicImplementationManager IntrinsicManager { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes all intrinsics.
        /// </summary>
        private void InitIntrinsics()
        {
            PTXIntrinsics.Register(IntrinsicManager);
            CLIntrinsics.Register(IntrinsicManager);
        }

        /// <summary>
        /// Returns true if the current context has the given flags.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, if the current context has the given flags.</returns>
        public bool HasFlags(ContextFlags flags) => Flags.HasFlags(flags);

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

            RuntimeSystem.Instance.ClearCache(mode);
            base.ClearCache(mode);
        }

        /// <summary>
        /// Raises the corresponding <see cref="AcceleratorCreated"/> event.
        /// </summary>
        /// <param name="accelerator">The new accelerator.</param>
        internal void OnAcceleratorCreated(Accelerator accelerator) =>
            AcceleratorCreated?.Invoke(this, accelerator);

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
