// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Context.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Backends.PTX;
using ILGPU.Frontend;
using ILGPU.Frontend.DebugInformation;
using ILGPU.IR;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.Resources;
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
    /// Represents the main ILGPU context.
    /// </summary>
    /// <remarks>Members of this class are thread safe.</remarks>
    public sealed partial class Context : DisposeBase, ICache
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
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline",
            Justification = "Complex initialization logic is required in this case")]
        [SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations",
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
                typeof(MethodImplAttribute).GetConstructor(new Type[] { typeof(MethodImplOptions) }),
                new object[] { MethodImplOptions.AggressiveInlining });

            // Ensure initialized runtime
            if (Runtime.Accelerator.Accelerators.Length < 1)
                throw new TypeLoadException(ErrorMessages.IntrinsicAcceleratorsBroken);
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a method builder in the .Net world.
        /// </summary>
        internal readonly struct MethodEmitter
        {
            /// <summary>
            /// Constructs a new method emitter.
            /// </summary>
            /// <param name="method">The desired internal method.</param>
            public MethodEmitter(
                DynamicMethod method)
            {
                Method = method;
                ILGenerator = method.GetILGenerator();
            }

            /// <summary>
            /// Returns the associated method builder.
            /// </summary>
            private DynamicMethod Method { get; }

            /// <summary>
            /// Returns the internal IL generator.
            /// </summary>
            public ILGenerator ILGenerator { get; }

            /// <summary>
            /// Finishes the building process.
            /// </summary>
            /// <returns>The emitted method.</returns>
            public MethodInfo Finish() => Method;
        }

        #endregion

        #region Instance

        private long idCounter = 0;
        private long functionHandleCounter = 0;
        private long nodeMarker = 0L;

        private readonly SemaphoreSlim codeGenerationSemaphore = new SemaphoreSlim(1);
        private readonly object assemblyLock = new object();
        private int assemblyVersion = 0;
        private AssemblyBuilder assemblyBuilder;
        private ModuleBuilder moduleBuilder;
        private volatile int typeBuilderIdx = 0;

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
            OptimizationLevel = optimizationLevel;
            Flags = flags.Prepare();

            // Initialize main contexts
            TypeContext = new IRTypeContext(this);
            IRContext = new IRContext(this);

            // Create frontend
            DebugInformationManager frontendDebugInformationManager =
                HasFlags(ContextFlags.EnableDebugInformation) ? DebugInformationManager : null;

            if (HasFlags(ContextFlags.EnableParallelCodeGenerationInFrontend))
                ILFrontend = new ILFrontend(frontendDebugInformationManager);
            else
                ILFrontend = new ILFrontend(frontendDebugInformationManager, 1);

            // Create default IL backend
            if (flags.HasFlags(ContextFlags.SkipCPUCodeGeneration))
                DefautltILBackend = new SkipCodeGenerationDefaultILBackend(this);
            else
                DefautltILBackend = new DefaultILBackend(this);

            // Initialize default transformer
            ContextTransformer = Optimizer.CreateTransformer(
                OptimizationLevel,
                TransformerConfiguration.Transformed,
                Flags);

            // Intrinsics
            IntrinsicManager = new IntrinsicImplementationManager();
            InitIntrinsics();

            // Initialize assembly builder and context data
            ReloadAssemblyBuilder();
        }

        #endregion

        #region Properties

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
        /// Returns the optimization level.
        /// </summary>
        public OptimizationLevel OptimizationLevel { get; }

        /// <summary>
        /// Returns the main debug-information manager.
        /// </summary>
        public DebugInformationManager DebugInformationManager { get; } = new DebugInformationManager();

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
        }

        /// <summary>
        /// Reloads the assembly builder.
        /// </summary>
        private void ReloadAssemblyBuilder()
        {
            var assemblyName = new AssemblyName(RuntimeAssemblyName)
            {
                Version = new Version(1, assemblyVersion++),
            };
            assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                assemblyName,
                AssemblyBuilderAccess.RunAndCollect);
            moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
        }

        /// <summary>
        /// Returns true if the current context has the given flags.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, if the current context has the given flags.</returns>
        public bool HasFlags(ContextFlags flags) => Flags.HasFlags(flags);

        /// <summary>
        /// Creates a new unique node marker.
        /// </summary>
        /// <returns>The new node marker.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeMarker NewNodeMarker() =>
            new NodeMarker(Interlocked.Add(ref nodeMarker, 1L));

        /// <summary>
        /// Creates a new unique node id.
        /// </summary>
        /// <returns>A new unique node id.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NodeId CreateNodeId() =>
            new NodeId(Interlocked.Add(ref idCounter, 1));

        /// <summary>
        /// Creates a new unique function handle.
        /// </summary>
        /// <returns>A new unique function handle.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long CreateFunctionHandle() =>
            Interlocked.Add(ref functionHandleCounter, 1);

        /// <summary>
        /// Releases the internal code-generation lock.
        /// </summary>
        internal void ReleaseCodeGenerationLock()
        {
            codeGenerationSemaphore.Release();
        }

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
        /// Begins a new code generation phase (async).
        /// </summary>
        /// <returns>The new code generation phase.</returns>
        public Task<ContextCodeGenerationPhase> BeginCodeGenerationAsync()
        {
            return Task.Run(new Func<ContextCodeGenerationPhase>(BeginCodeGeneration));
        }

        /// <summary>
        /// Begins a new code generation phase (async).
        /// </summary>
        /// <returns>The new code generation phase.</returns>
        public Task<ContextCodeGenerationPhase> BeginCodeGenerationAsync(IRContext irContext)
        {
            if (irContext == null)
                throw new ArgumentNullException(nameof(irContext));
            return Task.Run(() => BeginCodeGeneration(irContext));
        }

        /// <summary>
        /// Clears internal caches. However, this does not affect individual accelerator caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        /// <remarks>
        /// This method is not thread-safe.
        /// </remarks>
        public void ClearCache(ClearCacheMode mode)
        {
            IRContext.ClearCache(mode);
            TypeContext.ClearCache(mode);
            DebugInformationManager.ClearCache(mode);
            DefautltILBackend.ClearCache(mode);

            ReloadAssemblyBuilder();
        }

        #endregion

        #region Runtime Assembly

        /// <summary>
        /// Defines a new runtime type.
        /// </summary>
        /// <param name="attributes">The custom type attributes.</param>
        /// <param name="baseClass">The base class.</param>
        /// <returns>A new runtime type builder.</returns>
        private TypeBuilder DefineRuntimeType(TypeAttributes attributes, Type baseClass)
        {
            lock (assemblyLock)
                return moduleBuilder.DefineType(CustomTypeName + typeBuilderIdx++, attributes, baseClass);
        }

        /// <summary>
        /// Defines a new runtime class.
        /// </summary>
        /// <returns>A new runtime type builder.</returns>
        internal TypeBuilder DefineRuntimeClass(Type baseClass)
        {
            return DefineRuntimeType(
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout |
                TypeAttributes.Sealed,
                baseClass ?? typeof(object));
        }

        /// <summary>
        /// Defines a new runtime structure.
        /// </summary>
        /// <returns>A new runtime type builder.</returns>
        internal TypeBuilder DefineRuntimeStruct()
        {
            return DefineRuntimeType(
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.SequentialLayout |
                TypeAttributes.Sealed,
                typeof(ValueType));
        }

        /// <summary>
        /// Defines a new runtime method.
        /// </summary>
        /// <param name="returnType">The return type.</param>
        /// <param name="parameterTypes">All parameter types.</param>
        /// <returns>The defined method.</returns>
        internal MethodEmitter DefineRuntimeMethod(
            Type returnType,
            Type[] parameterTypes)
        {
            var typeBuilder = DefineRuntimeStruct();
            var type = typeBuilder.CreateType();

            var method = new DynamicMethod(
                LauncherMethodName,
                typeof(void),
                parameterTypes,
                type,
                true);
            return new MethodEmitter(method);
        }

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
        }

        #endregion
    }
}
