// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelCompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Backends.CPU;
using ILGPUC.Compilers;
using ILGPUC.Frontend;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.Transformations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA2000 // ownership transferred to MulticastWriter or returned

namespace ILGPUC;

/// <summary>
/// Result of compiling a single kernel for a single backend.
/// </summary>
readonly record struct KernelCompilationResult(
    string ClassName,
    string SourceCode,
    AcceleratorType AcceleratorType);

/// <summary>
/// Result of compiling multiple kernels across multiple backends.
/// </summary>
sealed record KernelCompilationBatch(
    IReadOnlyList<KernelCompilationResult> Kernels,
    IReadOnlyDictionary<AcceleratorType, string> Registrars);

/// <summary>
/// Controls if and where normalized IR dumps are written during compilation.
/// </summary>
/// <param name="Points">
/// Flags indicating which pipeline stages should write dump files.
/// </param>
/// <param name="Directory">
/// Root directory for dump output. Must be non-null when <paramref name="Points"/>
/// is not <see cref="IRDumpPoint.None"/>.
/// </param>
/// <param name="PrintToConsole">
/// When <see langword="true"/>, normalized IR at every pipeline stage and the
/// generated GPU source code are also printed to <see cref="Console.Out"/>.
/// Independent of <paramref name="Points"/> and <paramref name="Directory"/>.
/// </param>
/// <param name="Format">
/// The IR printer format to use for all dump output.
/// Defaults to <see cref="IRPrinterFormat.ILGPU"/> (ILGPU-native format).
/// Use <see cref="IRPrinterFormat.LLVM"/> for LLVM-style output.
/// </param>
readonly record struct IRDumpSettings(
    IRDumpPoint Points,
    string? Directory,
    bool PrintToConsole = false,
    IRPrinterFormat Format = IRPrinterFormat.ILGPU)
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="point"/> is active for
    /// file-based dumping.
    /// </summary>
    public bool HasPoint(IRDumpPoint point) => (Points & point) != 0;
}

/// <summary>
/// Facade API that encapsulates the ILGPUC compilation pipeline:
/// MethodInfo + Backend -> IL frontend -> IR -> Optimizer -> Backend codegen ->
/// CompiledKernel C# source.
/// </summary>
sealed class KernelCompiler
{
    private static int _intrinsicsInitialized;

    private readonly CompilationProperties _properties;
    private readonly TypeInformationManager _typeManager;
    private readonly Transformer _optimizationTransformer;

    /// <summary>
    /// The compilation properties used by this compiler instance.
    /// </summary>
    public CompilationProperties Properties => _properties;

    /// <summary>
    /// Creates a new kernel compiler with the given properties.
    /// </summary>
    public KernelCompiler(CompilationProperties? properties = null)
    {
        _properties = properties ?? new CompilationProperties();
        _typeManager = new TypeInformationManager();
        _optimizationTransformer = Optimizer.CreateTransformer(
            _properties.OptimizationLevel);

        // Thread-safe one-time initialization of intrinsics
        if (Interlocked.CompareExchange(ref _intrinsicsInitialized, 1, 0) == 0)
            Intrinsics.Init();
    }

    /// <summary>
    /// Compiles a single kernel method for a single backend.
    /// </summary>
    /// <param name="method">The kernel entry-point method.</param>
    /// <param name="backend">The backend to target.</param>
    /// <param name="compilerManager">
    /// Optional compiler manager for binary compilation.
    /// </param>
    /// <param name="dump">
    /// IR dump settings; pass <see langword="default"/> to skip.
    /// </param>
    /// <param name="launchParamTypeNames">
    /// Optional user-facing C# type names for each kernel parameter, in
    /// declaration order. When provided, these names are used in the generated
    /// <c>Launch</c> method signature instead of the synthetic
    /// <c>Struct_{id}</c> fallback. Required for kernels that take view-
    /// dependent struct parameters; otherwise the generated code references
    /// an undeclared synthetic struct name.
    /// </param>
    /// <param name="launchParamFieldAccessors">
    /// Optional per-parameter struct field accessor names (one inner array
    /// per parameter, <see langword="null"/> for non-struct params). Used by
    /// the launch-side marshaling code to access user struct fields by their
    /// original names.
    /// </param>
    /// <param name="kernelName">
    /// Optional user-facing kernel method name. When provided, the generated
    /// class is named <c>{kernelName}_CompiledKernel</c> so the rewritten
    /// call sites resolve correctly. When <see langword="null"/>, falls back
    /// to the IR-derived internal entry point name.
    /// </param>
    /// <param name="indexDimOverride">
    /// Optional user-facing override functionality to change behavior of indexed
    /// dimension parameters for launch-index propagation.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The compilation result with class name and C# source.</returns>
    internal async Task<KernelCompilationResult> CompileKernelAsync(
        MethodInfo method,
        Backend backend,
        ICompilerManager? compilerManager = null,
        IRDumpSettings dump = default,
        string[]? launchParamTypeNames = null,
        string[]?[]? launchParamFieldAccessors = null,
        string? kernelName = null,
        int? indexDimOverride = null,
        CancellationToken ct = default)
    {
        // Build IR module (may write AfterFrontend / AfterGlobalOpt dumps)
        var module = CompileMethodToModule(method, backend.BackendType, dump);

        // Generate code and CompiledKernel wrapper (may write AfterBackendTransforms
        // dump)
        return await CompileForBackendAsync(
            backend,
            module,
            compilerManager,
            dump,
            launchParamTypeNames,
            launchParamFieldAccessors,
            kernelName,
            indexDimOverride,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Compiles multiple kernels across multiple backends, and generates
    /// per-backend registrar files.
    /// </summary>
    internal async Task<KernelCompilationBatch> CompileAllAsync(
        MethodInfo[] methods,
        Backend[] backends,
        ICompilerManager? compilerManager = null,
        bool parallel = false,
        IRDumpSettings dump = default,
        CancellationToken ct = default)
    {
        IReadOnlyList<KernelCompilationResult> results;

        if (parallel)
        {
            var tasks =
                from method in methods
                from backend in backends
                select CompileKernelAsync(
                    method, backend, compilerManager, dump, ct: ct);
            results = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        else
        {
            var list = new List<KernelCompilationResult>(
                methods.Length * backends.Length);
            foreach (var method in methods)
            {
                foreach (var backend in backends)
                {
                    list.Add(await CompileKernelAsync(
                            method, backend, compilerManager, dump, ct: ct)
                        .ConfigureAwait(false));
                }
            }
            results = list;
        }

        // Aggregate registrars
        var kernelsByBackend = new Dictionary<AcceleratorType, List<string>>();
        foreach (var result in results)
        {
            if (!kernelsByBackend.TryGetValue(result.AcceleratorType, out var classNames))
                kernelsByBackend[result.AcceleratorType] = classNames = [];
            classNames.Add(result.ClassName);
        }

        var registrars = new Dictionary<AcceleratorType, string>();
        foreach (var (accelType, classNames) in kernelsByBackend)
        {
            registrars[accelType] = KernelRegistrarGenerator.Generate(
                accelType,
                classNames);
        }

        return new KernelCompilationBatch(results, registrars);
    }

    /// <summary>
    /// Converts a .NET MethodInfo to an optimized IR Module, optionally writing
    /// normalized IR dumps at <see cref="IRDumpPoint.AfterFrontend"/> and
    /// <see cref="IRDumpPoint.AfterGlobalOpt"/>.
    /// </summary>
    internal IR.ModuleValues.Module CompileMethodToModule(
        MethodInfo method,
        BackendType backendType,
        IRDumpSettings dump = default)
    {
        var assemblyDir = Path.GetDirectoryName(method.DeclaringType?.Assembly.Location);
        var frontend = new ILFrontend(backendType, assemblyDir);
        frontend.LoadMethods([method]);

        var moduleBuilder = new ModuleBuilder(
            _properties,
            new Generation(),
            IR.Location.Unknown,
            _typeManager);
        frontend.GenerateCode(moduleBuilder, [method]);

        var entryPointHandle = moduleBuilder.GetMethod(method).Declaration.Handle;
        var module = moduleBuilder.Seal(entryPointHandle);

        // Dump: IL -> IR done, no passes run yet
        if (dump.HasPoint(IRDumpPoint.AfterFrontend) && dump.Directory is not null)
        {
            var dir = Path.Combine(dump.Directory, "frontend");
            Directory.CreateDirectory(dir);
            module.DumpToFile(
                Path.Combine(dir, $"{GetDumpFileName(method)}.ir"),
                IRDumpMode.Normalized,
                dump.Format,
                IRDumpPoint.AfterFrontend);
        }
        if (dump.PrintToConsole)
        {
            PrintIRToConsole(
                module,
                IRDumpPoint.AfterFrontend,
                GetDumpFileName(method),
                dump.Format);
        }

        // Apply optimization pipeline (LowerGCInit validates and lowers GCInit nodes)
        var finalModule = _optimizationTransformer.Apply(
            _properties, _typeManager, module);

        // Dump: backend-agnostic passes complete
        if (dump.HasPoint(IRDumpPoint.AfterGlobalOpt) && dump.Directory is not null)
        {
            var dir = Path.Combine(dump.Directory, "global");
            Directory.CreateDirectory(dir);
            finalModule.DumpToFile(
                Path.Combine(dir, $"{GetDumpFileName(method)}.ir"),
                IRDumpMode.Normalized,
                dump.Format,
                IRDumpPoint.AfterGlobalOpt);
        }
        if (dump.PrintToConsole)
        {
            PrintIRToConsole(
                finalModule,
                IRDumpPoint.AfterGlobalOpt,
                GetDumpFileName(method),
                dump.Format);
        }

        return finalModule;
    }

    /// <summary>
    /// Runs one backend against one module, optionally compiles to binary,
    /// and returns the CompiledKernel generation result.
    /// </summary>
    internal async Task<KernelCompilationResult> CompileForBackendAsync(
        Backend backend,
        IR.ModuleValues.Module module,
        ICompilerManager? compilerManager,
        IRDumpSettings dump,
        string[]? launchParamTypeNames,
        string[]?[]? launchParamFieldAccessors,
        string? kernelName,
        int? indexDimOverride,
        CancellationToken ct)
    {
        // Open optional IR dump writer for AfterBackendTransforms (file, console, or both)
        using TextWriter? dumpWriter = BuildBackendDumpWriter(dump, backend, module);

        // Generate source code for this backend (passes dumpWriter to Backend.GenerateCode)
        var compiled = backend.GenerateCode(
            _properties,
            _typeManager,
            module,
            dumpWriter,
            dump.Format);

        // Print GPU source to console
        if (dump.PrintToConsole)
        {
            var sep = new string('=', 58);
            await Console.Out.WriteLineAsync().ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"// {sep}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync(
                $"// GPU Source: {backend.BackendType} | {compiled.EntryPointName}")
                .ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"// {sep}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync(compiled.SourceCode).ConfigureAwait(false);
        }

        // Optionally compile source to binary
        byte[]? compiledBinary = null;
        if (compilerManager is not null)
        {
            var compilationResult = await backend
                .CompileSourceAsync(compiled, compilerManager, ct)
                .ConfigureAwait(false);
            if (compilationResult is { Success: true, Output: not null })
            {
                compiledBinary = Convert.FromBase64String(compilationResult.Output);
            }
            else if (compilationResult is { Success: false })
            {
                await Console.Error.WriteLineAsync(
                    $"Error: {backend.BackendType} binary compilation failed " +
                    $"(exit code {compilationResult.ExitCode}).")
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(compilationResult.StdErr))
                {
                    await Console.Error.WriteLineAsync(compilationResult.StdErr)
                        .ConfigureAwait(false);
                }
                if (!string.IsNullOrWhiteSpace(compilationResult.StdOut))
                {
                    await Console.Error.WriteLineAsync(compilationResult.StdOut)
                        .ConfigureAwait(false);
                }
                throw new InvalidOperationException(
                    $"{backend.BackendType} binary compilation failed " +
                    $"(exit code {compilationResult.ExitCode}).");
            }
        }

        // SIMD width is needed for the CPU launcher emitter. The kernel class
        // name MUST come from compiled.KernelClassName (the post-backend-
        // transform name) — recomputing it from module.EntryPoint here gives
        // a stale ID that no longer matches the inner class declared in the
        // generated source. We mirror what ProgramBuilder does in the
        // in-process test path: leave kernelClassName null and let the
        // CompiledKernelGenerator fall back to compiled.KernelClassName.
        int simdWidth = backend is CPUBackend cpuBackend
            ? cpuBackend.VectorWidth
            : 8;

        // Generate kernel class wrapper
        var kernelGuid = Guid.NewGuid();
        using var compiledKernelGen = new CompiledKernelGenerator(
            backend.CreateCompiledKernelEmitter(),
            backend.CreateLauncherEmitter(),
            module,
            compiled,
            kernelGuid,
            compiledBinary,
            kernelClassName: null,
            simdWidth: simdWidth,
            kernelName: kernelName,
            launchParamTypeNames: launchParamTypeNames,
            launchParamFieldAccessors: launchParamFieldAccessors,
            indexDimOverride: indexDimOverride);
        var genResult = compiledKernelGen.Generate();

        return new KernelCompilationResult(
            genResult.ClassName,
            genResult.SourceCode,
            genResult.AcceleratorType);
    }

    /// <summary>
    /// Returns a safe file-name stem derived from the kernel method's declaring
    /// type and name (e.g., <c>MyClass_VectorAdd</c>).
    /// </summary>
    private static string GetDumpFileName(MethodInfo method)
    {
        var raw = $"{method.DeclaringType?.Name ?? "Unknown"}_{method.Name}";
        foreach (var c in Path.GetInvalidFileNameChars())
            raw = raw.Replace(c, '_');
        return raw;
    }

    /// <summary>
    /// Prints a normalized IR dump to <see cref="Console.Out"/> with a banner header.
    /// </summary>
    private static void PrintIRToConsole(
        IR.ModuleValues.Module module,
        IRDumpPoint point,
        string label,
        IRPrinterFormat format = IRPrinterFormat.ILGPU)
    {
        var sep = new string('=', 58);
        Console.Out.WriteLine();
        Console.Out.WriteLine($"// {sep}");
        Console.Out.WriteLine($"// IR: {point} | {label}");
        Console.Out.WriteLine($"// {sep}");
        module.Dump(Console.Out, IRDumpMode.Normalized, format, point);
    }

    /// <summary>
    /// Builds a <see cref="TextWriter"/> for the
    /// <see cref="IRDumpPoint.AfterBackendTransforms"/> stage, routing output to a
    /// file, <see cref="Console.Out"/>, or both, depending on
    /// <paramref name="dump"/> settings. Returns <see langword="null"/> when neither
    /// destination is active.
    /// </summary>
    private static TextWriter? BuildBackendDumpWriter(
        IRDumpSettings dump,
        Backend backend,
        IR.ModuleValues.Module module)
    {
        bool toFile = dump.HasPoint(IRDumpPoint.AfterBackendTransforms)
                         && dump.Directory is not null;
        bool toConsole = dump.PrintToConsole;

        if (!toFile && !toConsole)
            return null;

        StreamWriter? fileWriter = null;
        if (toFile)
        {
            var kernelName = module.EntryPoint is { } ep
                ? GenerationContext.GetClassName(ep)
                : "Unknown";
            var dir = Path.Combine(
                dump.Directory!, "backend", backend.BackendType.ToString());
            Directory.CreateDirectory(dir);
            fileWriter = new StreamWriter(
                Path.Combine(dir, $"{kernelName}.ir"), append: false);
        }

        if (toFile && toConsole)
            return new MulticastWriter(fileWriter!, new NonClosingWriter(Console.Out));
        if (toFile)
            return fileWriter;
        return new NonClosingWriter(Console.Out);
    }

    /// <summary>
    /// Wraps a <see cref="TextWriter"/> without taking ownership;
    /// <see cref="Dispose"/> is a deliberate no-op so the inner writer is not closed.
    /// Used to pass <see cref="Console.Out"/> to APIs that may dispose their writer.
    /// </summary>
    private sealed class NonClosingWriter(TextWriter inner) : TextWriter
    {
        public override Encoding Encoding => inner.Encoding;
        public override void Write(char value) => inner.Write(value);
        public override void Write(ReadOnlySpan<char> buffer) => inner.Write(buffer);
        public override void WriteLine(string? value) => inner.WriteLine(value);
        public override void Flush() => inner.Flush();
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing); // intentionally do not close inner
        }
    }

    /// <summary>
    /// Fans out writes to multiple <see cref="TextWriter"/> instances simultaneously.
    /// <see cref="Dispose"/> disposes all inner writers.
    /// </summary>
    private sealed class MulticastWriter(params TextWriter[] writers) : TextWriter
    {
        public override Encoding Encoding => writers[0].Encoding;
        public override void Write(char value)
        { foreach (var w in writers) w.Write(value); }
        public override void Write(ReadOnlySpan<char> buffer)
        { foreach (var w in writers) w.Write(buffer); }
        public override void WriteLine(string? value)
        { foreach (var w in writers) w.WriteLine(value); }
        public override void Flush()
        { foreach (var w in writers) w.Flush(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var w in writers) w.Dispose();
            base.Dispose(disposing);
        }
    }
}

#pragma warning restore CA2000
