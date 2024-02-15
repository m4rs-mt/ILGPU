// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Backend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a target platform.
    /// </summary>
    public enum TargetPlatform
    {
        /// <summary>
        /// The target platform is 32-bit.
        /// </summary>
        Platform32Bit,

        /// <summary>
        /// The target platform is 64-bit.
        /// </summary>
        Platform64Bit,
    }

    /// <summary>
    /// Extension methods for TargetPlatform related objects.
    /// </summary>
    public static class TargetPlatformExtensions
    {
        /// <summary>
        /// Returns true if the current runtime platform is 64-bit.
        /// </summary>
        public static bool Is64Bit(this TargetPlatform targetPlatform) =>
            targetPlatform == TargetPlatform.Platform64Bit;
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
        /// A Velocity backend.
        /// </summary>
        Velocity,

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
    /// Represents an abstract backend extensions that can store additional data.
    /// </summary>
    public abstract class BackendExtension : CachedExtension { }

    /// <summary>
    /// Represents a general ILGPU backend.
    /// </summary>
    public abstract class Backend : CachedExtensionBase<BackendExtension>
    {
        #region Nested Types

        /// <summary>
        /// No backend hook.
        /// </summary>
        private readonly struct NoHook : IBackendHook
        {
            /// <summary>
            /// Performs no operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void FinishedCodeGeneration(
                IRContext context,
                Method entryPoint)
            { }

            /// <summary>
            /// Performs no operation.
            /// </summary>
            public void InitializedKernelContext(
                IRContext kernelContext,
                Method kernelMethod)
            { }

            /// <summary>
            /// Performs no operation.
            /// </summary>
            public void OptimizedKernelContext(
                IRContext kernelContext,
                Method kernelMethod)
            { }
        }

        /// <summary>
        /// Represents the current kernel context in scope of a backend instance.
        /// </summary>
        protected internal readonly ref struct BackendContext
        {
            #region Nested Types

            /// <summary>
            /// An enumerator backend methods.
            /// </summary>
            public struct Enumerator : IEnumerator<(Method, Allocas)>
            {
                #region Instance

                private References.Enumerator enumerator;
                private readonly Dictionary<Method, Allocas> allocaMapping;

                /// <summary>
                /// Constructs a new enumerator.
                /// </summary>
                /// <param name="context">The current backend context.</param>
                internal Enumerator(in BackendContext context)
                {
                    KernelMethod = context.KernelMethod;
                    enumerator = context.Methods.GetEnumerator();
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
                public (Method, Allocas) Current
                {
                    get
                    {
                        var method = enumerator.Current;
                        return (method, allocaMapping[method]);
                    }
                }

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                #endregion

                #region Methods

                /// <summary cref="IDisposable.Dispose"/>
                void IDisposable.Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext()
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current == KernelMethod)
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
            internal BackendContext(IRContext kernelContext, Method kernelMethod)
            {
                Context = kernelContext;
                KernelMethod = kernelMethod;
                Methods = References.CreateRecursive(
                    kernelMethod.Blocks,
                    new MethodCollections.AllMethods());
                allocaMapping = new Dictionary<Method, Allocas>();
                notImplementedIntrinsics = new List<Method>();

                var sharedAllocations = ImmutableArray.
                    CreateBuilder<AllocaInformation>(20);
                var dynamicSharedAllocations = ImmutableArray.
                    CreateBuilder<AllocaInformation>(1);
                int sharedMemorySize = 0;

                foreach (var method in Methods)
                {
                    // Check for an unsupported intrinsic function
                    if (method.HasFlags(MethodFlags.Intrinsic))
                        notImplementedIntrinsics.Add(method);

                    var allocas = Allocas.Create(method.Blocks);
                    allocaMapping.Add(method, allocas);

                    // Check for dynamic shared memory
                    sharedAllocations.AddRange(allocas.SharedAllocations.Allocas);
                    sharedMemorySize += allocas.SharedMemorySize;
                    dynamicSharedAllocations.AddRange(
                        allocas.DynamicSharedAllocations.Allocas);
                }

                // Store shared memory information
                SharedAllocations = new AllocaKindInformation(
                    sharedAllocations.ToImmutable(),
                    sharedMemorySize);
                DynamicSharedAllocations = new AllocaKindInformation(
                    dynamicSharedAllocations.ToImmutable(),
                    0);
                SharedMemorySpecification = new SharedMemorySpecification(
                    sharedMemorySize,
                    dynamicSharedAllocations.Count > 0);

                KernelInfo = null;
                if (kernelContext.Properties.EnableKernelInformation)
                    KernelInfo = CreateKernelInfo();
            }

            /// <summary>
            /// Creates a new kernel information object.
            /// </summary>
            /// <returns>The created kernel information object.</returns>
            private CompiledKernel.KernelInfo CreateKernelInfo()
            {
                var functionInfo = ImmutableArray.CreateBuilder<
                    CompiledKernel.FunctionInfo>(Count);
                functionInfo.Add(new CompiledKernel.FunctionInfo(
                    KernelMethod.Name,
                    KernelMethod.Source,
                    KernelAllocas.LocalMemorySize));
                foreach (var (method, allocas) in this)
                {
                    functionInfo.Add(new CompiledKernel.FunctionInfo(
                        method.Name,
                        method.Source,
                        allocas.LocalMemorySize));
                }
                return new CompiledKernel.KernelInfo(
                    SharedAllocations,
                    functionInfo.MoveToImmutable());
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
            /// Returns all methods
            /// </summary>
            public References Methods { get; }

            /// <summary>
            /// Returns the associated allocations.
            /// </summary>
            public Allocas KernelAllocas => allocaMapping[KernelMethod];

            /// <summary>
            /// Returns all shared allocations.
            /// </summary>
            public AllocaKindInformation SharedAllocations { get; }

            /// <summary>
            /// Returns all dynamic shared allocations.
            /// </summary>
            public AllocaKindInformation DynamicSharedAllocations { get; }

            /// <summary>
            /// Returns the associated shared memory specification.
            /// </summary>
            public SharedMemorySpecification SharedMemorySpecification { get; }

            /// <summary>
            /// Returns the number of all functions.
            /// </summary>
            public int Count => Methods.Count;

            /// <summary>
            /// Returns the associated kernel information object (if any).
            /// </summary>
            public CompiledKernel.KernelInfo? KernelInfo { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Ensures that all not-implemented intrinsics have a valid associated
            /// code generator that will implement this intrinsic.
            /// </summary>
            /// <typeparam name="TDelegate">
            /// The backend-specific delegate type.
            /// </typeparam>
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
        protected delegate ArgumentMapper CreateArgumentMapper(Context context);

        /// <summary>
        /// Represents a function to create backend-specific transformers.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="builder">The target transformer builder.</param>
        protected delegate void CreateTransformersHandler(
            Context context,
            ImmutableArray<Transformer>.Builder builder);

        #endregion

        #region Static

        /// <summary>
        /// Returns the current execution platform.
        /// </summary>
        public static TargetPlatform RuntimePlatform =>
            RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X86 => TargetPlatform.Platform32Bit,
                Architecture.X64 => TargetPlatform.Platform64Bit,
                Architecture.Arm => TargetPlatform.Platform32Bit,
                Architecture.Arm64 => TargetPlatform.Platform64Bit,
                Architecture.Wasm => TargetPlatform.Platform64Bit,
                _ => throw new NotSupportedException(),
            };

        /// <summary>
        /// Returns the native OS platform.
        /// </summary>
        public static TargetPlatform OSPlatform =>
            RuntimeInformation.OSArchitecture switch
            {
                Architecture.X86 => TargetPlatform.Platform32Bit,
                Architecture.X64 => TargetPlatform.Platform64Bit,
                Architecture.Arm => TargetPlatform.Platform32Bit,
                Architecture.Arm64 => TargetPlatform.Platform64Bit,
                Architecture.Wasm => TargetPlatform.Platform64Bit,
                _ => throw new NotSupportedException(),
            };

        /// <summary>
        /// Returns true if the current runtime platform is equal to the OS platform.
        /// </summary>
        public static bool RunningOnNativePlatform => RuntimePlatform == OSPlatform;

        /// <summary>
        /// Ensures that the current runtime platform is equal to the OS platform.
        /// If not, this method will throw a <see cref="NotSupportedException"/>.
        /// </summary>
        public static void EnsureRunningOnNativePlatform()
        {
            if (!RunningOnNativePlatform)
            {
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NativePlatformInvocationRequired,
                    RuntimePlatform,
                    OSPlatform));
            }
        }

        /// <summary>
        /// Ensures that the current runtime platform is equal to the given platform.
        /// If not, this method will throw a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="platform">The desired target platform.</param>
        public static void EnsureRunningOnPlatform(TargetPlatform platform)
        {
            if (RuntimePlatform != platform)
            {
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NotSupportedPlatform,
                    RuntimePlatform,
                    platform));
            }
        }

        /// <summary>
        /// Returns either the given target platform or the current one.
        /// </summary>
        /// <param name="platform">The nullable target platform.</param>
        /// <returns>The computed target platform.</returns>
        protected static TargetPlatform GetPlatform(TargetPlatform? platform) =>
            platform ?? RuntimePlatform;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new generic backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="backendType">The backend type.</param>
        /// <param name="argumentMapper">The argument mapper to use.</param>
        protected Backend(
            Context context,
            CapabilityContext capabilities,
            BackendType backendType,
            ArgumentMapper argumentMapper)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Capabilities = capabilities;
            BackendType = backendType;
            ArgumentMapper = argumentMapper;

            // Setup custom pointer types
            PointerArithmeticType =
                Context.TargetPlatform.Is64Bit()
                ? ArithmeticBasicValueType.UInt64
                : ArithmeticBasicValueType.UInt32;
            PointerType = context.TypeContext.GetPrimitiveType(
                PointerArithmeticType.GetBasicValueType());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the assigned context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the current runtime system instance.
        /// </summary>
        protected RuntimeSystem RuntimeSystem => Context.RuntimeSystem;

        /// <summary>
        /// Returns the supported capabilities.
        /// </summary>
        public CapabilityContext Capabilities { get; }

        /// <summary>
        /// Returns the associated backend type.
        /// </summary>
        public BackendType BackendType { get; }

        /// <summary>
        /// Returns the target platform.
        /// </summary>
        public TargetPlatform Platform => Context.TargetPlatform;

        /// <summary>
        /// Returns the associated <see cref="ArgumentMapper"/>.
        /// </summary>
        public ArgumentMapper ArgumentMapper { get; }

        /// <summary>
        /// Returns the transformer that is applied before the final compilation step.
        /// </summary>
        protected ImmutableArray<Transformer> KernelTransformers { get; private set; } =
            ImmutableArray<Transformer>.Empty;

        /// <summary>
        /// Returns type of a native pointer.
        /// </summary>
        public PrimitiveType PointerType { get; }

        /// <summary>
        /// Returns the pointer size of a native pointer type.
        /// </summary>
        public int PointerSize => PointerType.Size;

        /// <summary>
        /// Returns the basic type of a native pointer.
        /// </summary>
        public BasicValueType PointerBasicValueType => PointerType.BasicValueType;

        /// <summary>
        /// Returns the arithmetic type of a native pointer.
        /// </summary>
        public ArithmeticBasicValueType PointerArithmeticType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the associated kernel transformers.
        /// </summary>
        /// <param name="createTransformers">The target handler.</param>
        protected void InitializeKernelTransformers(
            Action<ImmutableArray<Transformer>.Builder> createTransformers)
        {
            Debug.Assert(createTransformers != null, "Invalid transformers");
            var builder = ImmutableArray.CreateBuilder<Transformer>();
            createTransformers(builder);
            KernelTransformers = builder.ToImmutable();
        }

        /// <summary>
        /// Pre-compiles the given entry point description into an IR method.
        /// </summary>
        /// <param name="entry">The desired entry point.</param>
        /// <returns>The pre-compiled IR method.</returns>
        public Method PreCompileKernelMethod(in EntryPointDescription entry) =>
            PreCompileKernelMethod(entry, new NoHook());

        /// <summary>
        /// Pre-compiles the given entry point description into an IR method.
        /// </summary>
        /// <typeparam name="TBackendHook">The backend hook type.</typeparam>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="backendHook">The backend hook.</param>
        /// <returns>The pre-compiled IR method.</returns>
        public Method PreCompileKernelMethod<TBackendHook>(
            in EntryPointDescription entry,
            TBackendHook backendHook)
            where TBackendHook : IBackendHook
        {
            entry.Validate();

            Method generatedKernelMethod;
            using (var codeGenerationPhase = Context.BeginCodeGeneration())
            {
                var mainContext = codeGenerationPhase.IRContext;

                Frontend.CodeGenerationResult generationResult;
                using (var frontendPhase = codeGenerationPhase.
                    BeginFrontendCodeGeneration())
                {
                    generationResult = frontendPhase.GenerateCode(entry.MethodSource);
                }

                if (codeGenerationPhase.IsFaulted)
                    throw codeGenerationPhase.LastException.AsNotNull();
                generatedKernelMethod = generationResult.Result.AsNotNull();
                codeGenerationPhase.Optimize();

                backendHook.FinishedCodeGeneration(mainContext, generatedKernelMethod);
            }
            return generatedKernelMethod;
        }

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization.
        /// </summary>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public CompiledKernel Compile(
            in EntryPointDescription entry,
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
            in EntryPointDescription entry,
            in KernelSpecialization specialization,
            TBackendHook backendHook)
            where TBackendHook : IBackendHook
        {
            var generatedKernelMethod = PreCompileKernelMethod(entry, backendHook);
            return Compile(
                generatedKernelMethod,
                entry,
                specialization,
                backendHook);
        }

        /// <summary>
        /// Compiles a given method into a compiled kernel.
        /// </summary>
        /// <param name="kernelMethod">The main IR kernel method.</param>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public CompiledKernel Compile(
            Method kernelMethod,
            in EntryPointDescription entry,
            in KernelSpecialization specialization) =>
            Compile(kernelMethod, entry, specialization, new NoHook());

        /// <summary>
        /// Compiles a given method into a compiled kernel.
        /// </summary>
        /// <typeparam name="TBackendHook">The backend hook type.</typeparam>
        /// <param name="kernelMethod">The main IR kernel method.</param>
        /// <param name="entry">The desired entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="backendHook">The backend hook.</param>
        /// <returns>The compiled kernel that represents the compilation result.</returns>
        public CompiledKernel Compile<TBackendHook>(
            Method kernelMethod,
            in EntryPointDescription entry,
            in KernelSpecialization specialization,
            TBackendHook backendHook)
            where TBackendHook : IBackendHook
        {
            try
            {
                // Import the all kernel functions into our kernel context
                using var kernelContext = kernelMethod.ExtractToContext(out var method);

                // Mark this method as a new entry point
                method.AddFlags(MethodFlags.EntryPoint);
                backendHook.InitializedKernelContext(kernelContext, method);

                // Apply backend optimizations
                foreach (var transformer in KernelTransformers)
                    kernelContext.Transform(transformer);
                backendHook.OptimizedKernelContext(kernelContext, method);

                // Compile kernel
                var backendContext = new BackendContext(kernelContext, method);
                var entryPoint = CreateEntryPoint(
                    entry,
                    backendContext,
                    specialization);
                if (entryPoint.IsImplicitlyGrouped &&
                    backendContext.SharedMemorySpecification.HasSharedMemory)
                {
                    throw new NotSupportedException(
                        ErrorMessages.NotSupportedSharedImplicitlyGroupedKernel);
                }

                return Compile(entryPoint, backendContext, specialization);
            }
            catch (TypeLoadException tle)
            {
                throw new InternalCompilerException(
                    string.Format(
                        ErrorMessages.CouldNotLoadType,
                        tle.Message,
                        Context.RuntimeAssemblyName),
                    tle);
            }
            catch (InternalCompilerException)
            {
                // If we already have an internal compiler exception, re-throw it.
                throw;
            }
            catch (Exception e)
            {
                // Wrap generic exceptions.
                throw new InternalCompilerException(
                    ErrorMessages.InternalCompilerError,
                    e);
            }
        }

        /// <summary>
        /// Creates a new entry point that is compatible with the current backend.
        /// </summary>
        /// <param name="entry">The entry point.</param>
        /// <param name="backendContext">
        /// The current kernel context containing all required functions.
        /// </param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The created entry point.</returns>
        protected virtual EntryPoint CreateEntryPoint(
            in EntryPointDescription entry,
            in BackendContext backendContext,
            in KernelSpecialization specialization) =>
            new EntryPoint(
                entry,
                backendContext.SharedMemorySpecification,
                specialization);

        /// <summary>
        /// Compiles a given compile unit with the specified entry point using
        /// the given kernel specialization and the placement information.
        /// </summary>
        /// <param name="entryPoint">The desired entry point.</param>
        /// <param name="backendContext">
        /// The current kernel context containing all required functions.
        /// </param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>
        /// The compiled kernel that represents the compilation result.
        /// </returns>
        protected abstract CompiledKernel Compile(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization);

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        /// <remarks>This method is not thread-safe.</remarks>
        public override void ClearCache(ClearCacheMode mode)
        {
            ArgumentMapper?.ClearCache(mode);
            base.ClearCache(mode);
        }

        #endregion
    }

    /// <summary>
    /// Represents a general ILGPU backend.
    /// </summary>
    /// <typeparam name="TDelegate">
    /// The intrinsic delegate type for backend implementations.
    /// </typeparam>
    public abstract class Backend<TDelegate> : Backend
        where TDelegate : Delegate
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="backendType">The backend type.</param>
        /// <param name="argumentMapper">The argument mapper to use.</param>
        protected Backend(
            Context context,
            CapabilityContext capabilities,
            BackendType backendType,
            ArgumentMapper argumentMapper)
            : base(context, capabilities, backendType, argumentMapper)
        {
            // NB: Initialized later by derived classes.
            IntrinsicProvider =
                Utilities.InitNotNullable<IntrinsicImplementationProvider<TDelegate>>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current intrinsic provider.
        /// </summary>
        public IntrinsicImplementationProvider<TDelegate> IntrinsicProvider
        {
            get;
            private set;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the current intrinsic provider.
        /// </summary>
        protected void InitIntrinsicProvider()
        {
            IntrinsicProvider?.Dispose();
            IntrinsicProvider = Context.IntrinsicManager.CreateProvider<TDelegate>(this);
        }

        /// <summary>
        /// Initializes the associated kernel transformers.
        /// </summary>
        /// <param name="createTransformers">The target handler.</param>
        protected new void InitializeKernelTransformers(
            Action<ImmutableArray<Transformer>.Builder> createTransformers) =>
            base.InitializeKernelTransformers(builder =>
            {
                // Specialize intrinsic functions
                var resolver = new IntrinsicResolver<TDelegate>(IntrinsicProvider);
                var specializer = new IntrinsicSpecializer<TDelegate>(IntrinsicProvider);
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
        protected override void Dispose(bool disposing) => base.Dispose(disposing);
        /*
        {
            if (disposing)
                IntrinsicProvider.Dispose();
            base.Dispose(disposing);
        }
        */

        #endregion
    }

    /// <summary>
    /// Extension methods for backend related objects.
    /// </summary>
    public static class BackendExtensions
    {
        /// <summary>
        /// Gets the underlying backend from the given accelerator.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>The associated accelerator backend.</returns>
        public static Backend GetBackend(this Accelerator accelerator) =>
            accelerator.Backend;
    }
}
