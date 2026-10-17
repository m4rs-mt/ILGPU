// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
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
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Roslyn.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
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

        /// <summary>
        /// The output type of the project (for example Library, Exe or WinExe).
        /// Executables are not referenced by other assemblies, so their public
        /// methods are treated like internal ones.
        /// </summary>
        readonly Option<string?> _outputTypeOption = new("--output-type")
        {
            Description =
                "Output type of the project: Exe and WinExe are executables, " +
                "anything else is a library (default: Library)",
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
        /// Preprocessor symbols of the project, so that the analyzed code matches
        /// the code the real build compiles.
        /// </summary>
        readonly Option<string?> _defineConstantsOption = new("--define-constants")
        {
            Description =
                "Preprocessor symbols of the project, separated by ';' or ','",
        };

        /// <summary>
        /// The C# language version of the project.
        /// </summary>
        readonly Option<string?> _langVersionOption = new("--lang-version")
        {
            Description = "C# language version of the project (default: latest)",
        };

        /// <summary>
        /// The name of the assembly being built, which identifies its dispatch code
        /// for the builds of other assemblies.
        /// </summary>
        readonly Option<string?> _assemblyNameOption = new("--assembly-name")
        {
            Description =
                "Name of the assembly being built; enables launching generic kernels " +
                "across assemblies",
        };

        /// <summary>
        /// Analyzer assemblies of the project, whose source generators run before
        /// the analysis.
        /// </summary>
        readonly Option<string[]> _analyzersOption = new("--analyzers")
        {
            Description = "Analyzer assemblies whose source generators run first",
            AllowMultipleArgumentsPerToken = true,
        };

        /// <summary>Additional files of the project, for source generators.</summary>
        readonly Option<string[]> _additionalFilesOption = new("--additional-files")
        {
            Description = "Additional files passed to source generators",
            AllowMultipleArgumentsPerToken = true,
        };

        /// <summary>
        /// The .editorconfig and .globalconfig files of the project, for source
        /// generators.
        /// </summary>
        readonly Option<string[]> _analyzerConfigFilesOption =
            new("--analyzer-config-files")
            {
                Description = "Analyzer config files passed to source generators",
                AllowMultipleArgumentsPerToken = true,
            };

        /// <summary>
        /// Warns for externally visible methods whose type arguments reach a kernel
        /// launch.
        /// </summary>
        readonly Option<bool> _warnOpenPublicEntryPointsOption =
            new("--warn-open-public-entry-points")
            {
                Description =
                    "Warn for externally visible methods whose type arguments reach " +
                    "a kernel launch, since other assemblies may launch kernels " +
                    "with type arguments that are not compiled",
            };

        /// <summary>
        /// The maximum nesting depth of a type argument when finding the
        /// instantiations of kernels launched from generic code.
        /// </summary>
        readonly Option<int> _maxGenericTypeDepthOption =
            new("--max-generic-type-depth")
            {
                Description =
                    "Maximum nesting depth of a type argument when finding the " +
                    "instantiations of kernels launched from generic code",
                DefaultValueFactory = static _ =>
                    GenericInstantiationLimits.DefaultMaxTypeDepth,
            };

        /// <summary>
        /// The maximum number of instantiations of one generic definition when
        /// finding the instantiations of kernels launched from generic code.
        /// </summary>
        readonly Option<int> _maxGenericInstantiationsPerDefinitionOption =
            new("--max-generic-instantiations-per-definition")
            {
                Description =
                    "Maximum number of instantiations of one generic method or type " +
                    "when finding the instantiations of kernels launched from " +
                    "generic code",
                DefaultValueFactory = static _ =>
                    GenericInstantiationLimits.DefaultMaxInstantiationsPerDefinition,
            };

        /// <summary>
        /// The maximum number of generic instantiations in total when finding the
        /// instantiations of kernels launched from generic code.
        /// </summary>
        readonly Option<int> _maxGenericInstantiationsOption =
            new("--max-generic-instantiations")
            {
                Description =
                    "Maximum number of generic instantiations in total when finding " +
                    "the instantiations of kernels launched from generic code",
                DefaultValueFactory = static _ =>
                    GenericInstantiationLimits.DefaultMaxInstantiations,
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
                _outputTypeOption,
                _backendTypeOption,
                _compileOption,
                _compilerServiceOption,
                _parallelOption,
                _dumpIRAtOption,
                _dumpIRDirOption,
                _printIROption,
                _irFormatOption,
                _defineConstantsOption,
                _langVersionOption,
                _maxGenericTypeDepthOption,
                _maxGenericInstantiationsPerDefinitionOption,
                _maxGenericInstantiationsOption,
                _analyzersOption,
                _additionalFilesOption,
                _analyzerConfigFilesOption,
                _warnOpenPublicEntryPointsOption,
                _assemblyNameOption,
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
        /// <returns>
        /// The process exit code: non-zero if a launch site has errors.
        /// </returns>
        async Task<int> Execute(ParseResult parseResult, CancellationToken ct)
        {
            var compileItems = parseResult.GetValue(_compileItemsOption) ?? [];
            var references = parseResult.GetValue(_referencesOption) ?? [];
            var outputDir = parseResult.GetRequiredValue(_outputDirOption);
            var projectDir = parseResult.GetValue(_projectDirOption);

            if (compileItems.Length == 0)
            {
                await Console.Error.WriteLineAsync(
                    "Error: --compile-items requires at least one source file.");
                return 0;
            }

            // Parse source files with the project's preprocessor symbols and
            // language version, so that #if blocks select the same code
            var languageVersion = LanguageVersion.Latest;
            if (parseResult.GetValue(_langVersionOption) is { Length: > 0 } version &&
                LanguageVersionFacts.TryParse(version, out var parsedVersion))
            {
                languageVersion = parsedVersion;
            }
            var preprocessorSymbols = (parseResult.GetValue(_defineConstantsOption) ?? "")
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);
            var parseOptions = CSharpParseOptions.Default
                .WithLanguageVersion(languageVersion)
                .WithPreprocessorSymbols(preprocessorSymbols);
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
                    parseOptions,
                    path: Path.GetFullPath(source),
                    cancellationToken: ct);
                syntaxTrees.Add(tree);
            }

            if (syntaxTrees.Count == 0)
            {
                await Console.Error.WriteLineAsync(
                    "Error: No valid source files found.");
                return 0;
            }

            // Collect references, and load referenced assemblies from their paths
            // when compiled kernels or their types refer to them
            var refs = new List<MetadataReference>();
            var referencePaths = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var refPath in references)
            {
                if (!File.Exists(refPath))
                    continue;
                refs.Add(MetadataReference.CreateFromFile(refPath));
                referencePaths.TryAdd(Path.GetFileNameWithoutExtension(refPath), refPath);
            }
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving +=
                (context, name) => name.Name is { } simpleName &&
                    referencePaths.TryGetValue(simpleName, out var path)
                    ? context.LoadFromAssemblyPath(Path.GetFullPath(path))
                    : null;

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

            // Run the project's source generators, so that generated types
            // resolve and calls through generated code are followed
            var generatorResult = SourceGeneratorRunner.Run(
                compilation,
                parseOptions,
                parseResult.GetValue(_analyzersOption) ?? [],
                parseResult.GetValue(_additionalFilesOption) ?? [],
                parseResult.GetValue(_analyzerConfigFilesOption) ?? [],
                ct);
            compilation = generatorResult.Compilation;
            foreach (var diagnostic in generatorResult.Diagnostics)
                await Console.Out.WriteLineAsync(diagnostic.ToString());

            // Resolve compilation properties and backends
            var properties =
                _compilationPropertiesOptions.GetProperties(parseResult);
            var backendTypes = parseResult.GetRequiredValue(_backendTypeOption);
            var backends = backendTypes
                .SelectMany(bt => _backendOptions[bt]
                    .CreateBackends(properties, parseResult))
                .ToArray();

            // Create compiler manager if --compile is set. Walks the full
            // resolution chain (explicit --compiler-service URIs → per-target
            // env vars → global env var → Docker default ports → local
            // toolchain). The local toolchain is included as a last-resort
            // fallback so single-machine builds keep working.
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
                    ct);
            }

            // Analyze the compilation
            var compiler = new KernelCompiler(properties);
            var limits = new GenericInstantiationLimits(
                parseResult.GetValue(_maxGenericTypeDepthOption),
                parseResult.GetValue(_maxGenericInstantiationsPerDefinitionOption),
                parseResult.GetValue(_maxGenericInstantiationsOption));
            var analysis = CompilationRewriter.Analyze(
                compilation,
                new AnalysisOptions(
                    limits,
                    parseResult.GetValue(_warnOpenPublicEntryPointsOption),
                    generatorResult.GeneratedTrees,
                    parseResult.GetValue(_assemblyNameOption),
                    IsLibrary(parseResult.GetValue(_outputTypeOption))));

            // Report diagnostics in the canonical format, which MSBuild's Exec
            // task turns into build warnings and errors
            foreach (var diagnostic in analysis.Diagnostics)
                await Console.Out.WriteLineAsync(diagnostic.ToString());
            if (analysis.HasErrors)
                return 1;

            if (analysis.AllKernels.Count == 0 &&
                analysis.UniqueKernels.Count == 0 &&
                analysis.Summaries is not { Count: > 0 })
            {
                // No launch sites — write empty manifest
                Directory.CreateDirectory(outputDir);
                await File.WriteAllTextAsync(
                    Path.Combine(outputDir, "manifest.txt"), "",
                    cancellationToken: ct);
                await Console.Out.WriteLineAsync(
                    "ILGPU build: No kernel launch sites found.");
                return 0;
            }

            Console.WriteLine(
                $"ILGPU build: Found {analysis.UniqueKernels.Count} " +
                $"unique kernel(s), compiling for {backends.Length} backend(s)...");
            foreach (var stub in analysis.DispatchStubs)
            {
                Console.WriteLine(
                    $"ILGPU build: Kernel '{stub.KernelName}' is " +
                    $"launched from generic code with " +
                    $"{stub.Specializations.Length} instantiation(s).");
            }

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
            MainKernelProvider provider;
            try
            {
                provider =
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
            }
            catch (StubCompilationException e)
            {
                foreach (var diagnostic in e.ToBuildDiagnostics())
                    await Console.Out.WriteLineAsync(diagnostic.ToString());
                return 1;
            }
            catch (KernelDiagnosticException exception)
            {
                // Errors in kernel code are reported like invalid launch sites
                await Console.Out.WriteLineAsync(exception.Diagnostic.ToString());
                return 1;
            }
            foreach (var diagnostic in provider.Diagnostics)
                await Console.Out.WriteLineAsync(diagnostic.ToString());

            // Leave out kernel instantiations that throw for their type arguments
            analysis = analysis.WithoutKernels(provider.SkippedKernels);

            // Rewrite to disk with real compiled kernel sources
            var realRewriter = new CompilationRewriter(provider);
            int rewrittenCount =
                realRewriter.RewriteToDisk(
                    compilation,
                    analysis,
                    outputDir,
                    projectDir);

            // Also write ILGPUC registrar files into generated/
            var generatedDir = Path.Combine(outputDir, "generated");
            Directory.CreateDirectory(generatedDir);

            // Generate registrars from the compiled kernel class names.
            // Dispatch stubs are not part of UniqueKernels: they are pure
            // static classes, not CompiledKernel subclasses.
            var kernelsByAccelType =
                new Dictionary<AcceleratorType, List<string>>();
            foreach (var kernel in analysis.UniqueKernels)
            {
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

            return 0;
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

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="outputType"/> is the
        /// output type of a library: anything but an executable (Exe or WinExe),
        /// including no output type at all.
        /// </summary>
        /// <param name="outputType">The output type of the project, or null.</param>
        private static bool IsLibrary(string? outputType) =>
            !string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase);
    }
}
