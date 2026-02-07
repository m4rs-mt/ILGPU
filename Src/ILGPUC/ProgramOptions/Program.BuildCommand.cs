// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.BuildCommand.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Compilers;
using ILGPUC.IR;
using ILGPUC.Roslyn.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
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
    /// Represents the build command that compiles C# source files through the
    /// full Roslyn -> ILGPUC pipeline.
    /// </summary>
    sealed class BuildCommand
    {
        /// <summary>C# source files to compile.</summary>
        readonly Option<string[]> _compileItemsOption = new("--compile-items")
        {
            Description = "C# source files to compile",
            AllowMultipleArgumentsPerToken = true,
        };

        /// <summary>Assembly references to include in the Roslyn compilation.</summary>
        readonly Option<string[]> _referencesOption = new("--references")
        {
            Description = "Assembly references",
            AllowMultipleArgumentsPerToken = true,
        };

        /// <summary>Output directory for rewritten and generated files.</summary>
        readonly Option<string> _outputDirOption = new("--output-dir")
        {
            Description = "Output directory for rewritten and generated files",
        };

        /// <summary>Project root directory for relative path computation.</summary>
        readonly Option<string?> _projectDirOption = new("--project-dir")
        {
            Description = "Project root for relative path computation",
        };

        /// <summary>Target backend type(s) (Cuda, ROCm, OpenCL, Metal, or CPU).</summary>
        readonly Option<BackendType[]> _backendTypeOption = new("-b", "--backend")
        {
            Description = "Backend type(s) to compile for (may be repeated)",
            DefaultValueFactory = static _ => new[] { BackendType.CPU },
            AllowMultipleArgumentsPerToken = true,
        };

        /// <summary>
        /// When <see langword="true"/>, invokes platform compilers to embed binary
        /// artifacts instead of emitting source.
        /// </summary>
        readonly Option<bool> _compileOption = new("--compile")
        {
            Description =
                "Compile generated source code using platform compilers " +
                "and embed binary instead of source",
            DefaultValueFactory = static _ => false,
        };

        /// <summary>
        /// Optional base URL of a remote <c>ILGPUC.CompilerService</c> for remote
        /// compilation.
        /// </summary>
        readonly Option<Uri?> _compilerServiceOption = new("--compiler-service")
        {
            Description =
                "Base URL of a remote ILGPUC.CompilerService instance",
        };

        /// <summary>
        /// When <see langword="true"/>, compiles all unique kernels concurrently
        /// using <see cref="Task.WhenAll(System.Collections.Generic.IEnumerable{Task})"/>
        /// instead of sequentially.
        /// </summary>
        readonly Option<bool> _parallelOption = new("-p", "--parallel")
        {
            Description =
                "Compile all unique kernels concurrently " +
                "(faster for multi-kernel or multi-backend builds)",
            DefaultValueFactory = static _ => false,
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
        /// Defaults to <c>--output-dir</c> when <c>--dump-ir-at</c> is set.
        /// </summary>
        readonly Option<string?> _dumpIRDirOption = new("--dump-ir-dir")
        {
            Description =
                "Directory to write normalized IR dump files. " +
                "Subdirs frontend/, global/, backend/<BackendType>/ are created " +
                "automatically. Defaults to --output-dir when --dump-ir-at is set.",
        };

        /// <summary>
        /// When <see langword="true"/>, prints normalized IR at every pipeline stage
        /// and the generated GPU source to stdout.
        /// Independent of <c>--dump-ir-at</c> / <c>--dump-ir-dir</c>.
        /// </summary>
        readonly Option<bool> _printIROption = new("--print-ir")
        {
            Description =
                "Print normalized IR at every pipeline stage and the generated GPU "
                + "source to stdout (useful for interactive debugging; independent of "
                + "--dump-ir-at)",
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
        /// Shared compilation-properties options registered on this command.
        /// </summary>
        readonly CompilationPropertiesOptions _compilationPropertiesOptions;

        /// <summary>
        /// Maps each <see cref="BackendType"/> to its backend-specific options object.
        /// </summary>
        readonly Dictionary<BackendType, BackendOptions> _backendOptions = new(2);

        /// <summary>
        /// Registers build command options and handlers with the given root command.
        /// </summary>
        /// <param name="rootCommand">
        /// The root command to attach the build sub-command to.
        /// </param>
        public BuildCommand(RootCommand rootCommand)
        {
            var command = new Command(
                "build",
                description:
                    "Compiles C# source files: finds kernel launch sites, " +
                    "compiles kernels via ILGPUC, and writes rewritten sources + " +
                    "compiled kernel classes to disk")
            {
                _compileItemsOption,
                _referencesOption,
                _outputDirOption,
                _projectDirOption,
                _backendTypeOption,
                _compileOption,
                _compilerServiceOption,
                _parallelOption,
                _dumpIRAtOption,
                _dumpIRDirOption,
                _printIROption,
                _irFormatOption,
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
        /// Parses source files, analyzes kernel launch sites, compiles kernels via
        /// ILGPUC, and writes rewritten sources and registrar files to disk.
        /// </summary>
        /// <param name="parseResult">The command-line parse result.</param>
        /// <param name="ct">Cancellation token.</param>
        async Task Execute(ParseResult parseResult, CancellationToken ct)
        {
            var compileItems = parseResult.GetValue(_compileItemsOption) ?? [];
            var references = parseResult.GetValue(_referencesOption) ?? [];
            var outputDir = parseResult.GetRequiredValue(_outputDirOption);
            var projectDir = parseResult.GetValue(_projectDirOption);

            if (compileItems.Length == 0)
            {
                await Console.Error.WriteLineAsync(
                    "Error: --compile-items requires at least one source file.");
                return;
            }

            // Parse source files
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var source in compileItems)
            {
                if (!File.Exists(source))
                {
                    await Console.Error.WriteLineAsync(
                        $"Warning: Source file not found: {source}");
                    continue;
                }

                var text = SourceText.From(
                    await File.ReadAllTextAsync(source, cancellationToken: ct),
                    System.Text.Encoding.UTF8);
                var tree = CSharpSyntaxTree.ParseText(
                    text,
                    CSharpParseOptions.Default
                        .WithLanguageVersion(LanguageVersion.Latest),
                    path: Path.GetFullPath(source),
                    cancellationToken: ct);
                syntaxTrees.Add(tree);
            }

            if (syntaxTrees.Count == 0)
            {
                await Console.Error.WriteLineAsync(
                    "Error: No valid source files found.");
                return;
            }

            // Collect references
            var refs = new List<MetadataReference>();
            foreach (var refPath in references)
            {
                if (File.Exists(refPath))
                    refs.Add(MetadataReference.CreateFromFile(refPath));
            }

            // If no explicit references, use trusted platform assemblies
            if (refs.Count == 0)
            {
                var trustedAssemblies =
                    AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
                if (trustedAssemblies != null)
                {
                    refs.AddRange(
                        trustedAssemblies
                            .Split(Path.PathSeparator)
                            .Where(p => File.Exists(p) && IsTrustedPlatformAssembly(p))
                            .Select(p => MetadataReference.CreateFromFile(p)));
                }
            }

            var compilation =
                CSharpCompilation.Create(
                    "ILGPUKernelCompilation",
                    syntaxTrees: syntaxTrees,
                    references: refs,
                    options: new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        allowUnsafe: true));

            // Resolve compilation properties and backends
            var properties =
                _compilationPropertiesOptions.GetProperties(parseResult);
            var backendTypes = parseResult.GetRequiredValue(_backendTypeOption);
            var backends = backendTypes
                .SelectMany(bt => _backendOptions[bt]
                    .CreateBackends(properties, parseResult))
                .ToArray();

            // Create compiler manager if --compile is set
            ICompilerManager? compilerManager = null;
            if (parseResult.GetValue(_compileOption))
            {
                var serviceUrl = parseResult.GetValue(_compilerServiceOption);
                compilerManager = serviceUrl is not null
                    ? new RemoteCompilerManager(serviceUrl)
                    : new CompilerManager();
            }

            // Analyze the compilation
            var compiler = new KernelCompiler(properties);
            var analysis = CompilationRewriter.Analyze(compilation);

            if (analysis.AllKernels.Count == 0)
            {
                // No launch sites — write empty manifest
                Directory.CreateDirectory(outputDir);
                await File.WriteAllTextAsync(
                    Path.Combine(outputDir, "manifest.txt"), "",
                    cancellationToken: ct);
                await Console.Out.WriteLineAsync(
                    "ILGPU build: No kernel launch sites found.");
                return;
            }

            Console.WriteLine(
                $"ILGPU build: Found {analysis.UniqueKernels.Count} " +
                $"unique kernel(s), compiling for {backends.Length} backend(s)...");

            // Resolve IR dump settings
            IRDumpPoint dumpPoints = IRDumpPoint.None;
            foreach (var p in parseResult.GetValue(_dumpIRAtOption) ?? [])
                dumpPoints |= p;
            var dumpDir = parseResult.GetValue(_dumpIRDirOption);
            if (dumpPoints != IRDumpPoint.None && dumpDir is null)
                dumpDir = outputDir;
            var printIR = parseResult.GetValue(_printIROption);
            var irFormat = parseResult.GetValue(_irFormatOption);
            var dump = new IRDumpSettings(dumpPoints, dumpDir, printIR, irFormat);

            // Two-phase compile: stubs -> temp assembly -> resolve -> real kernels
            var parallel = parseResult.GetValue(_parallelOption);
            var provider =
                await MainKernelProvider
                    .CreateAsync(
                        compilation,
                        analysis,
                        backends,
                        compiler,
                        compilerManager,
                        parallel,
                        dump,
                        ct)
                    .ConfigureAwait(false);

            // Rewrite to disk with real compiled kernel sources
            var realRewriter = new CompilationRewriter(provider);
            int rewrittenCount =
                realRewriter.RewriteToDisk(compilation, outputDir, projectDir);

            // Also write ILGPUC registrar files into generated/
            var generatedDir = Path.Combine(outputDir, "generated");
            Directory.CreateDirectory(generatedDir);

            // Compile all methods to get registrar info
            var methods = new List<MethodInfo>();
            foreach (var kernel in analysis.UniqueKernels)
            {
                if (kernel.KernelMethod is not null)
                {
                    // We need to resolve from the temp assembly again — but
                    // the provider already compiled them. For registrars, we
                    // can generate from the batch results.
                }
            }

            // Generate registrars from the compiled kernel class names.
            // Skip dispatch stubs — they are pure static classes, not
            // CompiledKernel subclasses.
            var dispatchNames = new HashSet<string>(
                analysis.DispatchKernels.Select(d => d.KernelName));
            var kernelsByAccelType =
                new Dictionary<AcceleratorType, List<string>>();
            foreach (var kernel in analysis.UniqueKernels)
            {
                if (dispatchNames.Contains(kernel.KernelName))
                    continue;

                foreach (var backend in backends)
                {
                    if (!kernelsByAccelType.TryGetValue(
                        backend.AcceleratorType, out var names))
                    {
                        kernelsByAccelType[backend.AcceleratorType] =
                            names = [];
                    }
                    names.Add($"{kernel.KernelName}_CompiledKernel");
                }
            }

            foreach (var (accelType, classNames) in kernelsByAccelType)
            {
                var registrarSource =
                    Backends.KernelRegistrarGenerator.Generate(
                        accelType, classNames);
                await File.WriteAllTextAsync(
                    Path.Combine(
                        generatedDir,
                        $"{accelType}KernelRegistrar.cs"),
                    registrarSource,
                    cancellationToken: ct);
            }

            Console.WriteLine(
                $"ILGPU build: {rewrittenCount} file(s) rewritten, " +
                $"output -> {outputDir}");
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="path"/> names a trusted
        /// platform assembly that should be included as a Roslyn metadata reference.
        /// </summary>
        /// <param name="path">Absolute path to the assembly file.</param>
        private static bool IsTrustedPlatformAssembly(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("ILGPU", StringComparison.OrdinalIgnoreCase)
                || fileName is "mscorlib.dll" or "netstandard.dll";
        }
    }
}
