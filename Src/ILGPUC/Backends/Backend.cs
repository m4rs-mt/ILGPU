// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Backend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPUC.Compilers;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Roslyn.Analysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Backends;

// These types will be consumed by external test frameworks
#pragma warning disable CA1515 // Consider making public types internal

/// <summary>
/// Represents the general type of a backend.
/// </summary>
public enum BackendType
{
    /// <summary>
    /// A metal backend.
    /// </summary>
    Metal,

    /// <summary>
    /// An OpenCL backend.
    /// </summary>
    OpenCL,

    /// <summary>
    /// A CUDA backend.
    /// </summary>
    Cuda,

    /// <summary>
    /// A ROCm backend.
    /// </summary>
    ROCm,

    /// <summary>
    /// A CPU backend for vectorized CPU execution.
    /// </summary>
    CPU,
}

/// <summary>
/// Represents a general ILGPU backend.
/// </summary>
/// <remarks>
/// Constructs a new generic backend.
/// </remarks>
/// <param name="backendType">The backend type.</param>
/// <param name="acceleratorType">The accelerator type.</param>
/// <param name="capabilities">The supported capabilities.</param>
abstract class Backend(
    BackendType backendType,
    AcceleratorType acceleratorType,
    AcceleratorCapabilities capabilities)
{
    /// <summary>
    /// Returns the associated backend type.
    /// </summary>
    public BackendType BackendType { get; } = backendType;

    /// <summary>
    /// Returns the associated accelerator type.
    /// </summary>
    public AcceleratorType AcceleratorType { get; } = acceleratorType;

    /// <summary>
    /// Returns the current warp size (if available).
    /// </summary>
    public virtual int? CurrentWarpSize { get; }

    /// <summary>
    /// Returns the supported capabilities.
    /// </summary>
    public AcceleratorCapabilities Capabilities { get; } = capabilities;

    /// <summary>
    /// Returns the underlying language configuration.
    /// </summary>
    public abstract LanguageConfiguration LanguageConfiguration { get; }

    /// <summary>
    /// Creates a platform-specific launcher emitter for this backend.
    /// </summary>
    public abstract LauncherEmitter CreateLauncherEmitter();

    /// <summary>
    /// Creates a platform-specific compiled kernel emitter for this backend.
    /// </summary>
    public abstract CompiledKernelEmitter CreateCompiledKernelEmitter();

    /// <summary>
    /// The maximum number of threads in a group of a GPU accelerator.
    /// </summary>
    protected const int MaxNumGPUThreadsPerGroup = 1024;

    /// <summary>
    /// Creates a new architecture specification used to specialize a module for this
    /// backend.
    /// </summary>
    protected abstract ArchitectureSpecification GetArchitectureSpecification();

    /// <summary>
    /// Creates a backend transformer to transform a given module into the right IR shape.
    /// </summary>
    /// <param name="properties">The compilation properties to use.</param>
    /// <returns>The transformer to be used.</returns>
    public Transformer CreateBackendTransformer(CompilationProperties properties)
    {
        var builder = Transformer.CreateBuilder();

        // Specialize for this accelerator
        builder.AddAcceleratorSpecializer(GetArchitectureSpecification());

        // Add general optimizations
        builder.AddBasicOptimizations();

        return builder.ToTransformer();
    }

    /// <summary>
    /// Generates code for the given module.
    /// </summary>
    /// <param name="properties">Compilation properties to use.</param>
    /// <param name="typeInformationManager">Shared type manager to use.</param>
    /// <param name="module">The module to generate code for.</param>
    /// <param name="launch">
    /// How the entry point is launched; derived from the entry point's first
    /// parameter when <see langword="null"/>.
    /// </param>
    /// <returns>The generated code.</returns>
    public CodeGenerationResult GenerateCode(
        CompilationProperties properties,
        TypeInformationManager typeInformationManager,
        Module module,
        EntryPointLaunch? launch = null) =>
        GenerateCode(
            properties,
            typeInformationManager,
            module,
            dumpWriter: null,
            launch: launch);

    /// <summary>
    /// Generates code for the given module, optionally dumping the post-backend-
    /// transform IR to <paramref name="dumpWriter"/>.
    /// </summary>
    /// <param name="properties">Compilation properties to use.</param>
    /// <param name="typeInformationManager">Shared type manager to use.</param>
    /// <param name="module">The module to generate code for.</param>
    /// <param name="dumpWriter">
    /// Optional writer that receives a normalized IR dump after backend-specific
    /// transforms are applied. Pass <see langword="null"/> to skip the dump.
    /// </param>
    /// <param name="dumpFormat">
    /// The IR printer format to use when writing the dump.
    /// Defaults to <see cref="IRPrinterFormat.ILGPU"/>.
    /// </param>
    /// <param name="launch">
    /// How the entry point is launched; derived from the entry point's first
    /// parameter when <see langword="null"/>.
    /// </param>
    /// <returns>The generated code.</returns>
    public CodeGenerationResult GenerateCode(
        CompilationProperties properties,
        TypeInformationManager typeInformationManager,
        Module module,
        TextWriter? dumpWriter,
        IRPrinterFormat dumpFormat = IRPrinterFormat.ILGPU,
        EntryPointLaunch? launch = null)
    {
        var transformer = CreateBackendTransformer(properties);
        var finalModule = transformer.Apply(properties, typeInformationManager, module);

        if (dumpWriter is not null)
        {
            finalModule.Dump(
                dumpWriter,
                IRDumpMode.Normalized,
                format: dumpFormat,
                point: IRDumpPoint.AfterBackendTransforms,
                annotation: $"backend: {BackendType}  " +
                $"opt: {properties.OptimizationLevel}");
        }

        if (!LanguageConfiguration.SupportsFeature(LanguageFeature.GenericAddressSpace))
            VerifyNoGenericAddressSpaces(finalModule);
        if (!LanguageConfiguration.SupportsFeature(LanguageFeature.Recursion))
            VerifyNoRecursion(finalModule);

        return GenerateCode(finalModule, EntryPointLaunch.Resolve(module, launch));
    }

    /// <summary>
    /// Rejects recursive calls, which this backend cannot run. The methods of a
    /// call cycle are never inlined, so the cycle remains unless optimization
    /// removes the calls that close it.
    /// </summary>
    /// <param name="module">The final module after all transformations.</param>
    private void VerifyNoRecursion(Module module)
    {
        var cycles = CallCycle.FindAll(module.Methods.ToArray());
        if (cycles.Count < 1)
            return;

        var cycle = cycles[0];
        throw new KernelCompilationException(
            KernelDiagnostic.UnsupportedRecursionId,
            string.Format(
                ErrorMessages.KernelDiagnosticUnsupportedRecursion,
                cycle.FormatMethodNames(),
                BackendType),
            cycle.Calls[0].Location);
    }

    /// <summary>
    /// Rejects generic pointers and references into shared or local memory,
    /// which this backend cannot express: they remain generic when they can
    /// point to several address spaces, are returned by a helper method or are
    /// stored to memory. Generic pointers into global memory or with an unknown
    /// address space (for example, pointers loaded from memory) are emitted in
    /// the backend's default (global) address space and are not rejected.
    /// </summary>
    /// <param name="module">The final module after all transformations.</param>
    private void VerifyNoGenericAddressSpaces(Module module)
    {
        var results = PointerAddressSpaces.AnalyzeModule(
            module.EntryPoint,
            PointerAddressSpaces.AnalysisFlags.IgnoreGenericAddressSpace);
        var visited = new HashSet<Value>();
        var worklist = new Stack<Value>();
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;

            foreach (var param in method.Parameters)
                VerifyNotGeneric(module, results, method, param);

            // Pure values and returned values are not part of the value chain
            // of any block: reach them as operands
            foreach (var block in method.Blocks)
            {
                foreach (var value in block)
                    worklist.Push(value);
                if (block.TerminationValue is { } terminationValue)
                    worklist.Push(terminationValue);
            }
            while (worklist.TryPop(out var value))
            {
                if (value is Method or TypeValue or BasicBlock || !visited.Add(value))
                    continue;
                VerifyNotGeneric(module, results, method, value);
                if (value is Store store)
                    VerifyNotStoredGeneric(module, results, method, store);
                foreach (var operand in value.Values)
                    worklist.Push(operand);
            }
        }
    }

    /// <summary>
    /// Throws if the given generic pointer or view can point to shared or local
    /// memory in a way the target cannot express: it can point to several
    /// address spaces, or it crosses a method boundary as a parameter or call
    /// result. A generic value that provably points into a single address space
    /// inside a method is emitted in that address space.
    /// </summary>
    private void VerifyNotGeneric(
        Module module,
        AddressSpaceResults results,
        Method method,
        Value value)
    {
        if (value.Type is not AddressSpaceType
            {
                AddressSpace: MemoryAddressSpace.Generic
            })
        {
            return;
        }

        const AddressSpaceFlags Spaces = AddressSpaceFlags.Global |
            AddressSpaceFlags.Shared | AddressSpaceFlags.Local;
        var flags = results[value].Flags & Spaces;
        if ((flags & (AddressSpaceFlags.Shared | AddressSpaceFlags.Local)) ==
            AddressSpaceFlags.None)
        {
            return;
        }

        bool severalSpaces = (flags & (flags - 1)) != AddressSpaceFlags.None;
        bool crossesMethod = value is Parameter || value is MethodCall ||
            IsPassedAcrossMethods(value);
        if (!severalSpaces && !crossesMethod)
            return;

        var reason = severalSpaces
            ? $"can point to {FormatSpaces(flags)} memory"
            : $"points to {FormatSpaces(flags)} memory but is passed to or " +
                "returned from a helper method as a generic pointer";
        throw GetGenericPointerException(module, method, value.Location, reason);
    }

    /// <summary>
    /// Throws if the given store writes a generic pointer or view into shared or
    /// local memory to memory, directly or as a structure field: a pointer loaded
    /// from memory points into the backend's default (global) address space.
    /// </summary>
    private void VerifyNotStoredGeneric(
        Module module,
        AddressSpaceResults results,
        Method method,
        Store store)
    {
        static bool IsGeneric(TypeValue type) =>
            type is AddressSpaceType
            {
                AddressSpace: MemoryAddressSpace.Generic
            };

        var value = store.Value;
        var info = results.GetAnalysisValue(value);
        var flags = AddressSpaceFlags.None;
        if (IsGeneric(value.Type))
        {
            flags = info.Data.Flags;
        }
        else if (value.Type is StructureType structureType)
        {
            for (int i = 0; i < structureType.NumFields; ++i)
            {
                if (!IsGeneric(structureType[i]))
                    continue;
                flags |= info.IsStructure && i < info.NumFields
                    ? info[i].Flags
                    : info.Data.Flags;
            }
        }

        flags &= AddressSpaceFlags.Shared | AddressSpaceFlags.Local;
        if (flags == AddressSpaceFlags.None)
            return;

        throw GetGenericPointerException(
            module,
            method,
            store.Location,
            $"points to {FormatSpaces(flags)} memory but is stored to memory " +
            "as a generic pointer");
    }

    /// <summary>
    /// Returns the exception rejecting a generic pointer or reference of the
    /// given method for the given reason.
    /// </summary>
    private NotSupportedException GetGenericPointerException(
        Module module,
        Method method,
        Location location,
        string reason)
    {
        var kernel = module.EntryPoint;
        var where = method == kernel
            ? string.Empty
            : $" in '{GetDisplayName(method)}'";
        var message = location.FormatErrorMessage(
            $"Kernel '{GetDisplayName(kernel)}': a pointer or reference{where} " +
            reason);
        return new NotSupportedException(
            $"{message}. {BackendType} does not support generic pointers; make " +
            "each pointer or reference point into a single address space.");
    }

    /// <summary>
    /// Returns true if the given value is returned from its method or passed to
    /// a called method.
    /// </summary>
    private static bool IsPassedAcrossMethods(Value value)
    {
        foreach (Use use in value.Uses)
        {
            if (use.Target is MethodCall ||
                use.Target is BasicBlock
                {
                    TerminationKind: BlockTerminationKind.Return
                })
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the address spaces of the given flags as a readable list.
    /// </summary>
    private static string FormatSpaces(AddressSpaceFlags flags) =>
        string.Join(
            " and ",
            new[]
            {
                (AddressSpaceFlags.Global, "global"),
                (AddressSpaceFlags.Shared, "shared"),
                (AddressSpaceFlags.Local, "local"),
            }
            .Where(space => (flags & space.Item1) != AddressSpaceFlags.None)
            .Select(space => space.Item2));

    /// <summary>
    /// Returns the user-facing name of a method.
    /// </summary>
    private static string GetDisplayName(Method method) =>
        method.Source is { } source
            ? $"{source.DeclaringType?.Name}.{source.Name}".TrimStart('.')
            : method.Name;

    /// <summary>
    /// Generates code for the given module.
    /// </summary>
    /// <param name="module">The module to generate code for.</param>
    /// <param name="launch">How the entry point is launched.</param>
    /// <returns>The generated code.</returns>
    protected virtual CodeGenerationResult GenerateCode(
        Module module,
        EntryPointLaunch launch)
    {
        var codeGenerator = new CodeGenerator(module, LanguageConfiguration, launch);
        return codeGenerator.GenerateCode();
    }

    /// <summary>
    /// Compiles generated source code into a platform binary using the given compiler
    /// manager.
    /// </summary>
    /// <typeparam name="TManager">
    /// A type that implements <see cref="ICompilerManager"/>.
    /// </typeparam>
    /// <param name="source">The code generation result containing source code.</param>
    /// <param name="manager">The compiler manager to use.</param>
    /// <param name="properties">
    /// The compilation properties driving this compile. Used by backends to map the
    /// ILGPU <see cref="CompilationProperties.OptimizationLevel"/> to a native
    /// compiler optimization flag.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The compilation result, or null if this backend does not support compilation.
    /// </returns>
    public virtual Task<CompilationResult?> CompileSourceAsync<TManager>(
        CodeGenerationResult source,
        TManager manager,
        CompilationProperties properties,
        CancellationToken ct = default)
        where TManager : ICompilerManager =>
        Task.FromResult<CompilationResult?>(null);

    /// <summary>
    /// Returns the native compiler flag that disables multiply-add fusion
    /// if <see cref="CompilationProperties.FusedMultiplyAddMode"/> turns it off.
    /// </summary>
    /// <param name="properties">The compilation properties.</param>
    /// <param name="flag">The flag of the native compiler.</param>
    /// <returns>The flag, or an empty string if fusion is allowed.</returns>
    protected static string GetFusionFlag(
        CompilationProperties properties,
        string flag) =>
        properties.FusedMultiplyAddMode == FusedMultiplyAddMode.Off
            ? flag
            : string.Empty;

    /// <summary>
    /// Translates an ILGPU <see cref="OptimizationLevel"/> to the corresponding
    /// <see cref="NativeOptimizationLevel"/>. ILGPU exposes O0..O2; we offset by one
    /// (O0 → native O1, O1 → native O2, O2 → native O3) so even an "unoptimized" IR
    /// build still gets basic register allocation and inlining from the native
    /// toolchain.
    /// </summary>
    /// <param name="level">The IR optimization level.</param>
    /// <returns>
    /// The native optimization level, never <see cref="NativeOptimizationLevel.O0"/>.
    /// </returns>
    protected static NativeOptimizationLevel GetNativeOptLevel(OptimizationLevel level)
        => level switch
        {
            OptimizationLevel.O0 => NativeOptimizationLevel.O1,
            OptimizationLevel.O1 => NativeOptimizationLevel.O2,
            OptimizationLevel.O2 => NativeOptimizationLevel.O3,
            _ => NativeOptimizationLevel.O2,
        };
}

#pragma warning restore CA1515
