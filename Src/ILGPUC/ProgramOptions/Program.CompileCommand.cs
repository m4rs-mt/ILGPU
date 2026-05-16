// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.CompileCommand.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Compilers;
using ILGPUC.IR;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC;

sealed partial class Program
{
    /// <summary>
    /// Represents a compile command to compile IL kernels.
    /// </summary>
    sealed class CompileCommand
    {
        /// <summary>
        /// Holds all objects constructed once during compilation setup and shared
        /// across every method × backend pair in the compilation run.
        /// </summary>
        /// <param name="Properties">
        /// Compilation properties resolved from CLI options.
        /// </param>
        /// <param name="Methods">Kernel entry-point methods to compile.</param>
        /// <param name="Backends">Backend instances to target.</param>
        /// <param name="OutputPath">
        /// Directory where generated .cs files are written.
        /// </param>
        /// <param name="CompilerManager">
        /// Optional compiler manager for binary compilation.
        /// </param>
        /// <param name="DumpSettings">IR dump settings resolved from CLI options.</param>
        sealed record CompilationSetup(
            CompilationProperties Properties,
            MethodInfo[] Methods,
            Backend[] Backends,
            string OutputPath,
            ICompilerManager? CompilerManager,
            IRDumpSettings DumpSettings);

        /// <summary>
        /// Path to the .NET assembly containing kernel entry-point methods.
        /// </summary>
        readonly Option<string> _inputAssemblyOption = new("-i", "--input", "--assembly")
        {
            Description = "Input assembly to load",
        };

        /// <summary>One or more fully-qualified kernel method names to compile.</summary>
        readonly Option<string[]> _kernelInputsOption = new("-k", "--kernel")
        {
            Description = "Input kernels to compile",
            AllowMultipleArgumentsPerToken = true,
        };

        /// <summary>
        /// Directory where generated <c>.cs</c> files are written.
        /// Defaults to a temp subdirectory.
        /// </summary>
        readonly Option<string> _outputPathOption = new("-o", "--output-path")
        {
            Description = "Output path to use (falls back to a temporary directory)",
            DefaultValueFactory = static _ =>
                Directory.CreateTempSubdirectory("ILGPUC").FullName
        };

        /// <summary>Target backend type(s) (Cuda, ROCm, OpenCL, Metal, or CPU).</summary>
        readonly Option<BackendType[]> _backendTypeOption = new("-b", "--backend")
        {
            Description = "Backend type(s) to compile for (may be repeated)",
            DefaultValueFactory = static _ => new[] { BackendType.Cuda },
            AllowMultipleArgumentsPerToken = true,
        };

        /// <summary>
        /// When <see langword="true"/>, invokes a platform compiler (nvcc, hipcc, xcrun)
        /// to embed a binary artifact instead of emitting source.
        /// </summary>
        readonly Option<bool> _compileOption = new("--compile")
        {
            Description =
                "Compile generated source code using platform compilers " +
                "(nvcc, hipcc, xcrun) and embed binary instead of source",
            DefaultValueFactory = static _ => false,
        };

        /// <summary>
        /// Zero or more base URLs of remote <c>ILGPUC.CompilerService</c>
        /// instances. Each URL is probed for capabilities; the first URL to
        /// advertise a target wins for that target. May be combined with the
        /// <c>ILGPU_*_SERVICE_URL</c> environment variables — explicit URLs
        /// take precedence.
        /// </summary>
        readonly Option<string[]> _compilerServiceOption = new("--compiler-service")
        {
            Description =
                "Base URL of a remote ILGPUC.CompilerService instance "
                + "(repeatable; first to advertise a backend wins)",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = static _ => [],
        };

        /// <summary>
        /// Pipeline stages at which to write normalized IR dump files to disk.
        /// </summary>
        readonly Option<IRDumpPoint[]> _dumpIRAtOption = new("--dump-ir-at")
        {
            Description =
                "Pipeline stages at which to emit normalized IR dumps. " +
                "Values: AfterFrontend, AfterGlobalOpt, AfterBackendTransforms, All. " +
                "May be space-separated or repeated.",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = static _ => [],
        };

        /// <summary>
        /// Directory for IR dump files.
        /// Defaults to <c>--output-path</c> when <c>--dump-ir-at</c> is set.
        /// </summary>
        readonly Option<string?> _dumpIRDirOption = new("--dump-ir-dir")
        {
            Description =
                "Directory to write normalized IR dump files. " +
                "Subdirs frontend/, global/, backend/<BackendType>/ are created " +
                "automatically. Defaults to --output-path when --dump-ir-at is set.",
        };

        /// <summary>
        /// When <see langword="true"/>, prints normalized IR at every pipeline stage
        /// and the generated GPU source to stdout.
        /// Independent of <c>--dump-ir-at</c> / <c>--dump-ir-dir</c>.
        /// </summary>
        readonly Option<bool> _printIROption = new("--print-ir")
        {
            Description =
                "Print normalized IR at every pipeline stage and the generated GPU " +
                "source to stdout (useful for interactive debugging; independent of " +
                "--dump -ir-at)",
            DefaultValueFactory = static _ => false,
        };

        /// <summary>
        /// IR printer format used for all dump output (file and console).
        /// </summary>
        readonly Option<IRPrinterFormat> _irFormatOption = new("--ir-format")
        {
            Description =
                "IR printer format for dump files and console output. " +
                "ILGPU: ILGPU-native format with block-prefixed names and ILGPU " +
                "opcodes (default). LLVM: LLVM-style format with sequential %%N names " +
                "and standard LLVM mnemonics.",
            DefaultValueFactory = static _ => IRPrinterFormat.ILGPU,
        };

        /// <summary>
        /// When <see langword="true"/>, compiles all method × backend pairs concurrently
        /// using <see cref="Task.WhenAll(System.Collections.Generic.IEnumerable{Task})"/>
        /// instead of sequentially.
        /// </summary>
        readonly Option<bool> _parallelOption = new("-p", "--parallel")
        {
            Description =
                "Compile all method × backend pairs concurrently " +
                "(faster for multi-kernel or multi-backend builds)",
            DefaultValueFactory = static _ => false,
        };

        /// <summary>
        /// Shared compilation-properties options registered on this command.
        /// </summary>
        readonly CompilationPropertiesOptions _compilationPropertiesOptions;

        /// <summary>
        /// Maps each <see cref="BackendType"/> to its backend-specific options object.
        /// </summary>
        readonly Dictionary<BackendType, BackendOptions> _backendOptions = new(2);

        /// <summary>
        /// Registers compile command options and handlers with the given command.
        /// </summary>
        /// <param name="rootCommand">The command to attach options to.</param>
        public CompileCommand(RootCommand rootCommand)
        {
            var command = new Command(
                "compile",
                description: "Compiles a kernel for a specific target architecture")
            {
                _inputAssemblyOption,
                _kernelInputsOption,
                _outputPathOption,
                _backendTypeOption,
                _compileOption,
                _compilerServiceOption,
                _dumpIRAtOption,
                _dumpIRDirOption,
                _printIROption,
                _irFormatOption,
                _parallelOption,
            };

            _compilationPropertiesOptions = new(command);
            _backendOptions.Add(BackendType.Cuda, new CudaOptions(command));
            _backendOptions.Add(BackendType.ROCm, new ROCmOptions(command));
            _backendOptions.Add(BackendType.OpenCL, new OpenCLOptions(command));
            _backendOptions.Add(BackendType.Metal, new MetalOptions(command));
            _backendOptions.Add(BackendType.CPU, new CPUOptions(command));

            command.SetAction(Execute);

            rootCommand.Add(command);
        }

        /// <summary>
        /// Compiles a set of kernels into binary form.
        /// </summary>
        /// <param name="parseResult">The command-line parse result.</param>
        /// <param name="ct">Cancellation token.</param>
        async Task Execute(ParseResult parseResult, CancellationToken ct)
        {
            // Resolve compiler manager up-front since the factory is async.
            ICompilerManager? compilerManager = null;
            if (parseResult.GetValue(_compileOption))
            {
                var rawUrls = parseResult.GetValue(_compilerServiceOption)
                    ?? [];
                var serviceUrls = rawUrls
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => new Uri(s))
                    .ToArray();
                compilerManager = await CompilerManagerFactory.CreateAsync(
                    serviceUrls,
                    allowLocal: true,
                    ct).ConfigureAwait(false);
            }
            var setup = BuildSetup(parseResult, compilerManager);

            var parallel = parseResult.GetValue(_parallelOption);
            var compiler = new KernelCompiler(setup.Properties);
            var batch = await compiler.CompileAllAsync(
                setup.Methods,
                setup.Backends,
                setup.CompilerManager,
                parallel,
                setup.DumpSettings,
                ct).ConfigureAwait(false);

            // Write kernel files
            foreach (var result in batch.Kernels)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(setup.OutputPath, $"{result.ClassName}.cs"),
                    result.SourceCode,
                    cancellationToken: ct);
            }

            // Write registrar files
            foreach (var (accelType, registrarSource) in batch.Registrars)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(setup.OutputPath, $"{accelType}KernelRegistrar.cs"),
                    registrarSource,
                    cancellationToken: ct);
            }
        }

        /// <summary>
        /// Reads all command-line options and constructs the objects required for the
        /// compilation run, returning them as a single <see cref="CompilationSetup"/>.
        /// </summary>
        /// <param name="parseResult">The command-line parse result.</param>
        /// <param name="compilerManager">
        /// Pre-resolved compiler manager (constructed asynchronously by the
        /// caller), or <see langword="null"/> when <c>--compile</c> is not set.
        /// </param>
        /// <returns>A fully initialized <see cref="CompilationSetup"/>.</returns>
        CompilationSetup BuildSetup(
            ParseResult parseResult,
            ICompilerManager? compilerManager)
        {
            // Resolve backend types and compilation properties
            var backendTypes = parseResult.GetRequiredValue(_backendTypeOption);
            var properties = _compilationPropertiesOptions.GetProperties(parseResult);

            // Load assembly and resolve kernel entry points
            var input = parseResult.GetRequiredValue(_inputAssemblyOption);
            var sourceAssembly = Assembly.LoadFile(input);
            var kernels = parseResult.GetRequiredValue(_kernelInputsOption);
            var methods = kernels
                .Select(kernel => ResolveMethod(sourceAssembly, kernel))
                .ToArray();

            // Resolve output path
            var outputPath = parseResult.GetValue(_outputPathOption) ??
                Directory.GetCurrentDirectory();

            // Materialize backends for all requested backend types
            var backends = backendTypes
                .SelectMany(bt => _backendOptions[bt]
                    .CreateBackends(properties, parseResult))
                .ToArray();

            // Resolve IR dump settings
            IRDumpPoint dumpPoints = IRDumpPoint.None;
            foreach (var p in parseResult.GetValue(_dumpIRAtOption) ?? [])
                dumpPoints |= p;
            var dumpDir = parseResult.GetValue(_dumpIRDirOption);
            if (dumpPoints != IRDumpPoint.None && dumpDir is null)
                dumpDir = outputPath;
            var printIR = parseResult.GetValue(_printIROption);
            var irFormat = parseResult.GetValue(_irFormatOption);
            var dumpSettings = new IRDumpSettings(dumpPoints, dumpDir, printIR, irFormat);

            return new CompilationSetup(
                properties,
                methods,
                backends,
                outputPath,
                compilerManager,
                dumpSettings);
        }
    }
}
