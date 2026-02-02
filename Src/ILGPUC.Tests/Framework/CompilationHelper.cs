// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationHelper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Metal;
using ILGPU.Util;
using ILGPUC.Backends;
using ILGPUC.Backends.CPU;
using ILGPUC.Backends.Cuda;
using ILGPUC.Backends.Metal;
using ILGPUC.Backends.OpenCL;
using ILGPUC.Backends.ROCm;
using ILGPUC.Compilers;
using ILGPUC.Frontend;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.Transformations;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IRModule = ILGPUC.IR.ModuleValues.Module;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Wraps the ILGPUC compilation pipeline for test use, exposing intermediate results
/// at each stage instead of writing files.
/// </summary>
sealed class CompilationHelper : DisposeBase
{
    /// <summary>Flag used to ensure intrinsics are only initialized once.</summary>
    private static int s_intrinsicsInitialized;

    /// <summary>The backend type used when no explicit backend is specified.</summary>
    private readonly BackendType _defaultBackendType;

    /// <summary>Shared type information manager used across all compilation stages.</summary>
    private readonly TypeInformationManager _typeManager;

    /// <summary>
    /// Creates a new <see cref="CompilationHelper"/> using the given default backend.
    /// </summary>
    /// <param name="backendType">The backend to use when none is explicitly specified.</param>
    public CompilationHelper(BackendType backendType = BackendType.CPU)
    {
        _defaultBackendType = backendType;
        _typeManager = new TypeInformationManager();
        EnsureIntrinsicsInitialized();
    }

    /// <summary>
    /// Initializes the intrinsic registry exactly once across all test instances.
    /// </summary>
    private static void EnsureIntrinsicsInitialized()
    {
        if (Interlocked.CompareExchange(ref s_intrinsicsInitialized, 1, 0) == 0)
            Intrinsics.Init();
    }

    /// <summary>
    /// Stage 0: IL -> sealed Module (unoptimized).
    /// </summary>
    public IRModule CompileToModule(
        MethodInfo method,
        CompilationProperties? props = null,
        BackendType? backendType = null)
    {
        var properties = props ?? new CompilationProperties();
        var bt = backendType ?? _defaultBackendType;

        var frontend = new ILFrontend(bt, GetAssemblyDir(method));
        frontend.LoadMethods([method]);

        var moduleBuilder = new ModuleBuilder(
            properties,
            new Generation(),
            Location.Unknown,
            _typeManager);
        frontend.GenerateCode(moduleBuilder, [method]);

        var entryPointHandle = moduleBuilder.GetMethod(method).Declaration.Handle;
        return moduleBuilder.Seal(entryPointHandle);
    }

    /// <summary>
    /// Stage 1: dump normalized IR at any pipeline point.
    /// </summary>
    public string GetNormalizedIR(
        MethodInfo method,
        IRDumpPoint point,
        CompilationProperties? props = null,
        BackendType? backend = null)
    {
        var properties = props ?? new CompilationProperties();
        var bt = backend ?? _defaultBackendType;

        var rawModule = CompileToModule(method, properties, bt);

        return point switch
        {
            IRDumpPoint.AfterFrontend => DumpNormalized(rawModule, point),
            IRDumpPoint.AfterGlobalOpt => DumpAfterGlobalOpt(
                rawModule, properties, point),
            IRDumpPoint.AfterBackendTransforms => DumpAfterBackendTransforms(
                rawModule, properties, bt, point),
            _ => throw new ArgumentOutOfRangeException(nameof(point))
        };
    }

    /// <summary>
    /// Stage 2: generate backend source code.
    /// </summary>
    public CodeGenerationResult GenerateBackendCode(
        MethodInfo method,
        BackendType backend,
        CompilationProperties? props = null)
    {
        var properties = props ?? new CompilationProperties();
        var rawModule = CompileToModule(method, properties, backend);

        // Apply global optimizations
        var optTransformer = properties.OptimizationLevel.CreateTransformer();
        var optimizedModule = optTransformer.Apply(properties, _typeManager, rawModule);

        // Generate code
        var backendInstance = CreateBackend(backend);
        return backendInstance.GenerateCode(properties, _typeManager, optimizedModule);
    }

    /// <summary>
    /// Stage 2b: compile via native compiler.
    /// </summary>
    public async Task<CompilationResult?> CompileToNativeAsync(
        MethodInfo method,
        BackendType backend,
        CompilationProperties? props = null,
        CancellationToken ct = default)
    {
        var properties = props ?? new CompilationProperties();
        var rawModule = CompileToModule(method, properties, backend);

        var optTransformer = properties.OptimizationLevel.CreateTransformer();
        var optimizedModule = optTransformer.Apply(properties, _typeManager, rawModule);

        var backendInstance = CreateBackend(backend);
        var codeGenResult = backendInstance.GenerateCode(
            properties, _typeManager, optimizedModule);

        var compilerManager = await CompilerManagerFactory
            .GetCompilerManagerAsync(backend).ConfigureAwait(false);
        return await backendInstance
            .CompileSourceAsync(codeGenResult, compilerManager, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Applies global optimizations and returns a normalized IR dump at
    /// <see cref="IRDumpPoint.AfterGlobalOpt"/>.
    /// </summary>
    private string DumpAfterGlobalOpt(
        IRModule rawModule,
        CompilationProperties properties,
        IRDumpPoint point)
    {
        var optTransformer = properties.OptimizationLevel.CreateTransformer();
        var optimizedModule = optTransformer.Apply(properties, _typeManager, rawModule);
        return DumpNormalized(
            optimizedModule,
            point,
            $"opt: {properties.OptimizationLevel}");
    }

    /// <summary>
    /// Applies global optimizations followed by backend-specific transforms and returns
    /// the normalized IR dump captured at <see cref="IRDumpPoint.AfterBackendTransforms"/>.
    /// </summary>
    private string DumpAfterBackendTransforms(
        IRModule rawModule,
        CompilationProperties properties,
        BackendType backendType,
        IRDumpPoint point)
    {
        var optTransformer = properties.OptimizationLevel.CreateTransformer();
        var optimizedModule = optTransformer.Apply(properties, _typeManager, rawModule);

        var backendInstance = CreateBackend(backendType);

        // Apply backend transforms and dump
        using var sw = new StringWriter();
        backendInstance.GenerateCode(
            properties,
            _typeManager,
            optimizedModule,
            sw,
            IRPrinterFormat.LLVM);

        // The GenerateCode with a dumpWriter writes the IR dump to sw.
        // But we want just the IR dump, not the source code.
        // Actually, looking at Backend.cs:155-174, the dumpWriter receives the
        // normalized IR dump. So sw.ToString() is the IR dump.
        return sw.ToString();
    }

    /// <summary>
    /// Serializes <paramref name="module"/> to a normalized LLVM-style IR string,
    /// optionally tagging the output with <paramref name="annotation"/>.
    /// </summary>
    private static string DumpNormalized(
        IRModule module,
        IRDumpPoint point,
        string? annotation = null)
    {
        using var sw = new StringWriter();
        module.Dump(sw, IRDumpMode.Normalized, IRPrinterFormat.LLVM, point, annotation);
        return sw.ToString();
    }

    /// <summary>
    /// Instantiates the <see cref="Backend"/> implementation for the given
    /// <paramref name="type"/> with fixed, test-suitable defaults.
    /// </summary>
    internal static Backend CreateBackend(BackendType type) => type switch
    {
        BackendType.CPU => new CPUBackend(vectorWidth: 8),
        BackendType.Cuda => new CudaBackend(
            CudaArchitecture.SM_80, CudaInstructionSet.ISA_80),
        BackendType.Metal => new MetalBackend(
            MetalTargetOS.macOS, MetalGPUFamily.Apple7),
        BackendType.ROCm => new ROCmBackend("gfx1100"),
        // The plain OpenCLBackend base class inherits Backend.CompileSourceAsync
        // which always returns null — only the concrete vendor subclasses
        // (OpenCLIntelBackend / OpenCLAmdBackend) override it to actually
        // dispatch to the compiler manager. Use the Intel variant here so
        // NativeCompilation tests route through ocloc; the OpenCL Docker
        // container ships intel-ocloc and reports OpenCLIntel as available.
        BackendType.OpenCL => new OpenCLIntelBackend(2, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    /// <summary>
    /// Returns the directory containing the assembly that declares
    /// <paramref name="method"/>, or <see langword="null"/> if unavailable.
    /// Used as the assembly search path for the IL frontend.
    /// </summary>
    private static string? GetAssemblyDir(MethodInfo method) =>
        Path.GetDirectoryName(method.DeclaringType?.Assembly.Location);
}
