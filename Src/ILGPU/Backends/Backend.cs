// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Backend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Transformations;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

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
    /// Represents the general type of a backend.
    /// </summary>
    public enum BackendType
    {
        /// <summary>
        /// An IL backend.
        /// </summary>
        IL,

        /// <summary>
        /// A PTX backend.
        /// </summary>
        PTX,

        /// <summary>
        /// An OpenCL source backend.
        /// </summary>
        OpenCL
    }

    /// <summary>
    /// Represents general backend-specific code-generation flags.
    /// </summary>
    [Flags]
    public enum BackendFlags : int
    {
        /// <summary>
        /// The default flags (none).
        /// </summary>
        None = 0,
    }

    /// <summary>
    /// Represents a general ILGPU backend.
    /// </summary>
    public abstract class Backend : DisposeBase, ICache
    {
        #region Nested Types

        /// <summary>
        /// No backend hook.
        /// </summary>
        private readonly struct NoHook : IBackendHook
        {
            /// <summary cref="IBackendHook.FinishedCodeGeneration(IRContext, Method)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void FinishedCodeGeneration(IRContext context, Method entryPoint) { }

            /// <summary cref="IBackendHook.InitializedKernelContext(IRContext, Method)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InitializedKernelContext(IRContext kernelContext, Method kernelMethod) { }

            /// <summary cref="IBackendHook.OptimizedKernelContext(IRContext, Method)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OptimizedKernelContext(IRContext kernelContext, Method kernelMethod) { }
        }

        /// <summary>
        /// Represents the current kernel context in scope of a backend instance.
        /// </summary>
        protected readonly ref struct BackendContext
        {
            #region Nested Types

            /// <summary>
            /// An enumerator backend methods.
            /// </summary>
            public struct Enumerator : IEnumerator<(Method, Scope, Allocas)>
            {
                #region Instance

                private CachedScopeProvider.Enumerator enumerator;
                private readonly Dictionary<Method, Allocas> allocaMapping;

                /// <summary>
                /// Constructs a new enumerator.
                /// </summary>
                /// <param name="context">The current backend context.</param>
                internal Enumerator(in BackendContext context)
                {
                    KernelMethod = context.KernelMethod;
                    enumerator = context.ScopeProvider.GetEnumerator();
                    allocaMapping = context.allocaMapping;
                }

                #endregion

                #region Properties

                /// <summary>
                /// Returns the associated kernel method.
                /// </summary>
                public Method KernelMethod { get; }

                /// <summary>
                /// Returns the current node.
                /// </summary>
                public (Method, Scope, Allocas) Current
                {
                    get
                    {
                        var (method, scope) = enumerator.Current;
                        return (method, scope, allocaMapping[method]);
                    }
                }

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                #endregion

                #region Methods

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext()
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.Item1 == KernelMethod)
                            continue;
                        return true;
                    }
                    return false;
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();

                #endregion
            }

            #endregion

            #region Instance

            private readonly List<Method> notImplementedIntrinsics;
            private readonly Dictionary<Method, Allocas> allocaMapping;

            /// <summary>
            /// Constructs a new backend context.
            /// </summary>
            /// <param name="kernelContext">The current kernel context.</param>
            /// <param name="kernelMethod">The kernel function.</param>
            /// <param name="abi">The current ABI.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal BackendContext(
                IRContext kernelContext,
                Method kernelMethod,
                ABI abi)
            {
                Context = kernelContext;
                KernelMethod = kernelMethod;
                ScopeProvider = new CachedScopeProvider();
                allocaMapping = new Dictionary<Method, Allocas>();
                notImplementedIntrinsics = new List<Method>();

                var toProcess = new Stack<Scope>();
                var currentScope = ScopeProvider[kernelMethod];

                var sharedAllocations = ImmutableArray.CreateBuilder<AllocaInformation>(20);
                var sharedMemorySize = 0;

                for (; ; )
                {
                    // Check for an unsupported intrinsic function
                    if (currentScope.Method.HasFlags(MethodFlags.Intrinsic))
                        notImplementedIntrinsics.Add(currentScope.Method);

                    var allocas = Allocas.Create(currentScope, abi);
                    allocaMapping.Add(currentScope.Method, allocas);

                    sharedAllocations.AddRange(allocas.SharedAllocations.Allocas);
                    sharedMemorySize += allocas.SharedMemorySize;

                    foreach (Value value in currentScope.Values)
                    {
                        if (value is MethodCall call &&
                            ScopeProvider.Resolve(call.Target, out var targetScope))
                            toProcess.Push(targetScope);
                    }

                    if (toProcess.Count < 1)
                        break;
                    currentScope = toProcess.Pop();
                }

                SharedAllocations = new AllocaKindInformation(
                    sharedAllocations.ToImmutable(),
                    sharedMemorySize);
            }

            #endregion

            #region Properties

            /// <summary>
            /// The associated kernel context.
            /// </summary>
            public IRContext Context { get; }

            /// <summary>
            /// Returns the main kernel method.
            /// </summary>
            public Method KernelMethod { get; }

            /// <summary>
            /// Returns the associated kernel scope.
            /// </summary>
            public Scope KernelScope => ScopeProvider[KernelMethod];

            /// <summary>
            /// Returns the associated allocations.
            /// </summary>
            public Allocas KernelAllocas => allocaMapping[KernelMethod];

            /// <summary>
            /// Returns the associated scope provider.
            /// </summary>
            public CachedScopeProvider ScopeProvider { get; }

            /// <summary>
            /// Returns all required shared allocations.
            /// </summary>
            public AllocaKindInformation SharedAllocations { get; }

            /// <summary>
            /// Returns the number of all functions.
            /// </summary>
            public int Count => ScopeProvider.Count;

            /// <summary>
            /// Returns the number of all secondary functions
            /// excluding the primary kernel function.
            /// </summary>
            public int NumFunctions => ScopeProvider.Count;

            #endregion

            #region Methods

            /// <summary>
            /// Ensures that all not-implemented intrinsics have a valid associated
            /// code generator that will implement this intrinsic.
            /// </summary>
            /// <typeparam name="TDelegate">The backend-specific delegate type.</typeparam>
            /// <param name="provider">The implementation provider to use.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnsureIntrinsicImplementations<TDelegate>(
                IntrinsicImplementationProvider<TDelegate> provider)
                where TDelegate : Delegate
            {
                // Iterate over all not-implemented intrinsic and ensure that a valid
                // code generation has been registered that will implement this intrinsic.
                foreach (var intrinsic in notImplementedIntrinsics)
                {
                    if (!provider.TryGetMapping(intrinsic, out var _))
                        throw new NotSupportedIntrinsicException(intrinsic);
                }
            }

            /// <summary>
            /// Returns an enumerator to enumerate all entries.
            /// </summary>
            /// <returns>An enumerator to enumerate all entries.</returns>
            public Enumerator GetEnumerator() => new Enumerator(this);

            #endregion
        }

        /// <summary>
        /// Represents a function to create backend-specific argument mappers.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="abi">The current ABI.</param>
        protected delegate ArgumentMapper CreateArgumentMapper(
            Context context,
            ABI abi);

        /// <summary>
        /// Represents a function to create backend-specific transformers.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="abi">The current ABI.</param>
        /// <param name="builder">The target transformer builder.</param>
        protected delegate void CreateTransformersHandler(
            Context context,
            ABI abi,
            ImmutableArray<Transformer>.Builder builder);

        #endregion

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
        /// <param name="backendType">The backend type.</param>
        /// <param name="backendFlags">The backend flags.</param>
        /// <param name="abi">The current ABI.</param>
        /// <param name="argumentMapperProvider">The provider for argument mappers.</param>
        protected Backend(
            Context context,
            BackendType backendType,
            BackendFlags backendFlags,
            ABI abi,
            Func<ABI, ArgumentMapper> argumentMapperProvider)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            BackendType = backendType;
            BackendFlags = backendFlags;
            ABI = abi ?? throw new ArgumentNullException(nameof(abi));
            ArgumentMapper = argumentMapperProvider(abi);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the assigned context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the associated backend type.
        /// </summary>
        public BackendType BackendType { get; }

        /// <summary>
        /// Returns the associated backend flags.
        /// </summary>
        public BackendFlags BackendFlags { get; }

        /// <summary>
        /// Returns the target platform.
        /// </summary>
        public TargetPlatform Platform => ABI.TargetPlatform;

        /// <summary>
        /// Returns the current ABI.
        /// </summary>
        public ABI ABI { get; }

        /// <summary>
        /// Returns the associated <see cref="ArgumentMapper"/>.
        /// </summary>
        public ArgumentMapper ArgumentMapper { get; }

        /// <summary>
        /// Returns the transformer that is applied before the final compilation step.
        /// </summary>
        protected ImmutableArray<Transformer> KernelTransformers { get; private set; } =
            ImmutableArray<Transformer>.Empty;

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the associated kernel transformers.
        /// </summary>
        /// <param name="createTransformers">The target handler.</param>
        protected void InitializeKernelTransformers(Action<ImmutableArray<Transformer>.Builder> createTransformers)
        {
            Debug.Assert(createTransformers != null, "Invalid transformers");
            var builder = ImmutableArray.CreateBuilder<Transformer>();
            createTransformers(builder);
            KernelTransformers = builder.ToImmutable();
        }

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization.
        /// </summary>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public CompiledKernel Compile(
            MethodInfo entry,
            in KernelSpecialization specialization) =>
            Compile(entry, specialization, new NoHook());

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization.
        /// </summary>
        /// <typeparam name="TBackendHook">The backend hook type.</typeparam>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="backendHook">The backend hook.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public virtual CompiledKernel Compile<TBackendHook>(
            MethodInfo entry,
            in KernelSpecialization specialization,
            TBackendHook backendHook)
            where TBackendHook : IBackendHook
        {
            using (var kernelContext = new IRContext(Context))
            {
                IRContext mainContext;
                Method generatedKernelMethod;
                using (var codeGenerationPhase = Context.BeginCodeGeneration())
                {
                    mainContext = codeGenerationPhase.IRContext;

                    Frontend.CodeGenerationResult generationResult;
                    using (var frontendCodeGenerationPhase = codeGenerationPhase.BeginFrontendCodeGeneration())
                        generationResult = frontendCodeGenerationPhase.GenerateCode(entry);

                    generatedKernelMethod = generationResult.Result;
                    codeGenerationPhase.Optimize();

                    backendHook.FinishedCodeGeneration(mainContext, generatedKernelMethod);
                }

                // Import the all kernel functions into our context
                var scopeProvider = new CachedScopeProvider();
                var kernelMethod = kernelContext.Import(generatedKernelMethod, scopeProvider);
                backendHook.InitializedKernelContext(kernelContext, kernelMethod);

                // Apply backend optimizations
                foreach (var transformer in KernelTransformers)
                    kernelContext.Transform(transformer);
                backendHook.OptimizedKernelContext(kernelContext, kernelMethod);

                // Compile kernel
                var backendContext = new BackendContext(kernelContext, kernelMethod, ABI);
                var entryPoint = CreateEntryPoint(
                    kernelMethod.Source as MethodInfo,
                    backendContext,
                    specialization);
                return Compile(entryPoint, backendContext, specialization);
            }
        }

        /// <summary>
        /// Creates a new entry point that is compatible with the current backend.
        /// </summary>
        /// <param name="method">The entry point method.</param>
        /// <param name="backendContext">The current kernel context containing all required functions.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The created entry point.</returns>
        protected virtual EntryPoint CreateEntryPoint(
            MethodInfo method,
            in BackendContext backendContext,
            in KernelSpecialization specialization) =>
            new EntryPoint(
                method,
                backendContext.SharedAllocations.TotalSize,
                specialization);

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization and the placement information.
        /// </summary>
        /// <param name="entryPoint">The desired entry point.</param>
        /// <param name="backendContext">The current kernel context containing all required functions.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        protected abstract CompiledKernel Compile(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization);

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        /// <remarks>This method is not thread-safe.</remarks>
        public virtual void ClearCache(ClearCacheMode mode)
        {
            ArgumentMapper?.ClearCache(mode);
        }

        #endregion
    }

    /// <summary>
    /// Represents a general ILGPU backend.
    /// </summary>
    /// <typeparam name="TDelegate">The intrinsic delegate type for backend implementations.</typeparam>
    public abstract class Backend<TDelegate> : Backend
        where TDelegate : Delegate
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="backendType">The backend type.</param>
        /// <param name="backendFlags">The backend flags.</param>
        /// <param name="abi">The current ABI.</param>
        /// <param name="argumentMapperProvider">The provider for argument mappers.</param>
        protected Backend(
            Context context,
            BackendType backendType,
            BackendFlags backendFlags,
            ABI abi,
            Func<ABI, ArgumentMapper> argumentMapperProvider)
            : base(context, backendType, backendFlags, abi, argumentMapperProvider)
        {
            IntrinsicProvider = context.IntrinsicManager.CreateProvider<TDelegate>(this);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current intrinsic provider.
        /// </summary>
        public IntrinsicImplementationProvider<TDelegate> IntrinsicProvider { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the associated kernel transformers.
        /// </summary>
        /// <typeparam name="TConfiguration">The configuration type.</typeparam>
        /// <param name="configuration">The specializer configuration.</param>
        /// <param name="createTransformers">The target handler.</param>
        protected void InitializeKernelTransformers<TConfiguration>(
            TConfiguration configuration,
            Action<ImmutableArray<Transformer>.Builder> createTransformers)
            where TConfiguration : IIntrinsicSpecializerConfiguration
        {
            InitializeKernelTransformers(builder =>
            {
                // Specialize intrinsic functions
                var resolver = new IntrinsicResolver<TDelegate>(IntrinsicProvider);
                var specializer = new IntrinsicSpecializer<TConfiguration, TDelegate>(
                    configuration,
                    IntrinsicProvider);
                var lowerThreadIntrinsics = new LowerThreadIntrinsics();

                // Perform two general passes to specialize ILGPU-specific intrinsic
                // functions that are invoked by other specialized functions.
                // TODO: determine the number of passes automatically
                const int NumPasses = 2;
                for (int i = 0; i < NumPasses; ++i)
                {
                    builder.Add(Transformer.Create(
                            TransformerConfiguration.Transformed,
                            lowerThreadIntrinsics, resolver, specializer));
                }

                createTransformers(builder);
            });
        }

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        /// <remarks>This method is not thread-safe.</remarks>
        public override void ClearCache(ClearCacheMode mode)
        {
            base.ClearCache(mode);
            IntrinsicProvider.ClearCache(mode);
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                IntrinsicProvider.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
