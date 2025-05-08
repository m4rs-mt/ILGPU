// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using ILGPUC.Backends;
using ILGPUC.Backends.EntryPoints;
using ILGPUC.Backends.PTX;
using ILGPUC.Backends.PTX.API;
using ILGPUC.Frontend;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using ILGPUC.IR.Transformations;
using ILGPUC.IR.Types;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ILGPUC;

/// <summary>
/// Main ILGPUC program class.
/// </summary>
sealed partial class Program
{
    /// <summary>
    /// Main entry point of the ILGPUC command line compiler.
    /// </summary>
    /// <param name="args">The command line args passed to ILGPUC.</param>
    static Task<int> Main(string[] args)
    {
        // Build the root command
        var root = new RootCommand("ILGPUC command line compiler");
        using var compileCommandHandler = new CompileCommand(root);

        // Parse and invoke with cancellation support
        var parseResult = root.Parse(args);
        return parseResult.InvokeAsync();
    }

    #region Commands

    /// <summary>
    /// Represents a compile command to compile IL kernels.
    /// </summary>
    sealed class CompileCommand : DisposeBase
    {
        readonly Option<string> _inputAssemblyOption = new("-i", "--input", "--assembly")
        {
            Description = "Input assembly to load",
        };

        readonly Option<string[]> _kernelInputsOption = new("-k", "--kernel")
        {
            Description = "Input kernels to compile",
            AllowMultipleArgumentsPerToken = true,
        };

        readonly Option<string> _outputPathOption = new("-o", "--output-path")
        {
            Description = "Output path to use (falls back to a temporary directory)",
            DefaultValueFactory = static _ =>
                Directory.CreateTempSubdirectory("ILGPUC").FullName
        };

        readonly Option<BackendType> _backendTypeOption = new("-b", "--backend")
        {
            Description = "Backend type to use",
            DefaultValueFactory = static _ => BackendType.PTX,
        };

        readonly Option<bool> _humanReadableKernelsOption = new("-hr", "--human-readable")
        {
            Description = "True to enable output of human readable kernels",
            DefaultValueFactory = static _ => false,
        };

        readonly CompilationPropertiesOptions _compilationPropertiesOptions;
        readonly Dictionary<BackendType, BackendOptions> _backendOptions = new(1);

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
                _humanReadableKernelsOption
            };

            _compilationPropertiesOptions = new(command);
            _backendOptions.Add(BackendType.PTX, new PTXOptions(command));

            command.SetAction(Execute);

            rootCommand.Add(command);
        }

        /// <summary>
        /// Compiles a set of kernels into binary form.
        /// </summary>
        /// <param name="parseResult">The command-line parse result.</param>
        void Execute(ParseResult parseResult)
        {
            // Get backend options
            var backendType = parseResult.GetRequiredValue(_backendTypeOption);
            var backendOptions = _backendOptions[backendType];

            // Get compilation properties
            var properties = _compilationPropertiesOptions.GetProperties(parseResult);

            // Find kernel entry points
            var input = parseResult.GetRequiredValue(_inputAssemblyOption);
            var sourceAssembly = Assembly.LoadFile(input);

            var kernels = parseResult.GetRequiredValue(_kernelInputsOption);
            var methods = kernels
                .Select(kernel => ResolveMethod(sourceAssembly, kernel))
                .ToArray();

            // Initialize intrinsics
            Intrinsics.Init();

            // Load all methods into one frontend instance
            var frontend = new ILFrontend(backendType, Path.GetDirectoryName(input));
            foreach (var method in methods)
                frontend.LoadMethods([method]);

            // Initialize optimization pipeline
            var optimizationTransformer = Optimizer.CreateTransformer(
                properties.OptimizationLevel);

            // Initiate shared type context
            using var typeContext = new IRTypeContext(properties);

            // Iterate over all backend configurations
            foreach (var method in methods)
            {
                // Get new IR context
                using var context = new IRContext(
                    properties,
                    Verifier.Empty,
                    typeContext);

                // Generate code
                frontend.GenerateCode(context, method);

                // Apply generic optimization pipeline
                context.Transform(optimizationTransformer);

                // Get entry point
                var entryPoint = new EntryPoint(method, isGrouped: true);

                // Iterate over all backend configurations
                // TODO: register backend configurations properly instead of overwriting
                // kernels serialized to disk
                var backendConfigurations = backendOptions.CreateBackends(parseResult);
                foreach (var backend in backendConfigurations)
                {
                    // Use backend to compile kernel
                    var compiled = backend.Compile(frontend, entryPoint, context);

                    // Serialize kernel
                    SerializeKernel(parseResult, compiled, backendOptions);
                }
            }
        }

        /// <summary>
        /// Serializes a compiled kernel.
        /// </summary>
        /// <param name="parseResult">The command-line parse result.</param>
        /// <param name="kernelData">Compiled kernel data.</param>
        /// <param name="backendOptions">Backend options to use.</param>
        void SerializeKernel(
            ParseResult parseResult,
            CompiledKernelData kernelData,
            BackendOptions backendOptions)
        {
            var outputPath = parseResult.GetValue(_outputPathOption) ??
                Directory.GetCurrentDirectory();
            var humanReadableKernels = parseResult.GetValue(_humanReadableKernelsOption);

            // Get kernel name and binary file name
            var kernelName = CompiledKernelData.GetKernelName(kernelData.Guid);
            var fileName = Path.Combine(outputPath, $"{kernelName}.ilgpuk");

            // Output kernel in binary form
            using var stream = new FileStream(
                fileName,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite);
            stream.SetLength(0L);
            using var writer = new BinaryWriter(stream);
            kernelData.Write(writer);

            // TODO: write kernel configurations to map backend properties to IDs
            // TODO: write resource table information for further processing

            // Write human readable kernels (if requested)
            if (humanReadableKernels)
            {
                backendOptions.WriteHumanReadableKernel(
                    outputPath,
                    kernelName,
                    kernelData);
            }
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var options in _backendOptions.Values)
                options.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Registers compilation properties command-line switches.
    /// </summary>
    sealed class CompilationPropertiesOptions
    {
        readonly Option<DebugSymbolsMode> _debugSymbolsOption = new("--debug-symbols")
        {
            Description = "Debug symbols mode to use",
            DefaultValueFactory = static _ => DebugSymbolsMode.None
        };

        readonly Option<OptimizationLevel> _optLevelOption = new(
            "-opt",
            "--optimization-level")
        {
            Description = "Optimization level to use (default O1)",
            DefaultValueFactory = static _ => OptimizationLevel.O1
        };

        readonly Option<bool> _assertionsOption = new("-assert", "--assertions")
        {
            Description = "Enables or disables assertions",
            DefaultValueFactory = static _ => false
        };

        readonly Option<bool> _ioOperationsOption = new("-io", "--io-operations")
        {
            Description = "Enables or disables assertions",
            DefaultValueFactory = static _ => false
        };

        readonly Option<InliningMode> _inliningModeOption = new("--inline")
        {
            Description = "Specifies inlining mode to use",
            DefaultValueFactory = static _ => InliningMode.Aggressive
        };

        readonly Option<MathMode> _mathModeOption = new("--math")
        {
            Description = "Specifies math operations mode to use",
            DefaultValueFactory = static _ => MathMode.Default
        };

        readonly Option<bool> _mathFlushToZeroOption = new("--ftz")
        {
            Description = "Forces math operations to use flush-to-zero for performance",
            DefaultValueFactory = static _ => false
        };

        readonly Option<StaticFieldMode> _staticFieldModeOption = new("--static-fields")
        {
            Description = "Specifies static field mode to use",
            DefaultValueFactory = static _ => StaticFieldMode.Default
        };

        readonly Option<ArrayMode> _arrayModeOption = new("--arrays")
        {
            Description = "Specifies array mode to use",
            DefaultValueFactory = static _ => ArrayMode.Default
        };

        readonly Option<bool> _debugOption = new("-d", "--debug")
        {
            Description = "Enables all debug-related settings (like assertions and IO)",
            DefaultValueFactory = static _ => false
        };

        readonly Option<bool> _releaseOption = new("-r", "--release")
        {
            Description = "Enables all release-related settings",
            DefaultValueFactory = static _ => false
        };

        /// <summary>
        /// Registers all options to control compilation properties with the command.
        /// </summary>
        /// <param name="command">The command to attach options to.</param>
        public CompilationPropertiesOptions(Command command)
        {
            command.Add(_debugSymbolsOption);
            command.Add(_optLevelOption);
            command.Add(_assertionsOption);
            command.Add(_ioOperationsOption);

            command.Add(_inliningModeOption);
            command.Add(_mathModeOption);
            command.Add(_mathFlushToZeroOption);
            command.Add(_staticFieldModeOption);
            command.Add(_arrayModeOption);

            command.Add(_debugOption);
            command.Add(_releaseOption);
        }

        /// <summary>
        /// Gets compilation properties from the given parse result.
        /// </summary>
        /// <param name="result">The parse result as input.</param>
        /// <returns>The determined compilation properties.</returns>
        public CompilationProperties GetProperties(ParseResult result)
        {
            var properties = new CompilationProperties
            {
                DebugSymbolsMode = result.GetValue(_debugSymbolsOption),
                OptimizationLevel = result.GetValue(_optLevelOption),
                EnableAssertions = result.GetValue(_assertionsOption),
                EnableIOOperations = result.GetValue(_ioOperationsOption),

                InliningMode = result.GetValue(_inliningModeOption),
                MathMode = result.GetValue(_mathModeOption),
                EnableMathFlushToZero = result.GetValue(_mathFlushToZeroOption),
                StaticFieldMode = result.GetValue(_staticFieldModeOption),
                ArrayMode = result.GetValue(_arrayModeOption),
            };

            if (result.GetValue(_debugOption))
            {
                properties = properties with
                {
                    DebugSymbolsMode = DebugSymbolsMode.Default,
                    OptimizationLevel = OptimizationLevel.O0,
                    EnableAssertions = true,
                    EnableIOOperations = true,
                };
            }
            if (result.GetValue(_releaseOption))
            {
                properties = properties with
                {
                    DebugSymbolsMode = DebugSymbolsMode.None,
                    OptimizationLevel = OptimizationLevel.O2,
                    EnableAssertions = false,
                    EnableIOOperations = false,
                };
            }

            return properties;
        }
    }

    #endregion

    #region Backend-specific options

    abstract class BackendOptions(BackendType backendType) : DisposeBase
    {
        public BackendType BackendType { get; } = backendType;

        public abstract IEnumerable<PTXBackend> CreateBackends(ParseResult parseResult);

        public abstract void WriteHumanReadableKernel(
            string outputPath,
            string kernelName,
            CompiledKernelData kernelData);
    }

    /// <summary>
    /// Represents options for the PTX backend
    /// </summary>
    sealed class PTXOptions : BackendOptions
    {
        readonly Option<PTXBackendMode> _modeOption = new("--ptx-mode")
        {
            Description = "PTX backend mode to specify",
            DefaultValueFactory = _ => PTXBackendMode.Default
        };

        readonly Option<string[]> _cudaArchitecturesOption = new("--cuda-arch")
        {
            Description = "Cuda architecture to use",
            DefaultValueFactory = _ => ["SM_80"],
            AllowMultipleArgumentsPerToken = true
        };

        readonly Option<string[]> _cudaInstructionSetsOption = new("--cuda-isa")
        {
            Description = "Cuda instruction set to use",
            DefaultValueFactory = _ => ["8.0"],
            AllowMultipleArgumentsPerToken = true
        };

        readonly Option<bool> _enableLibDeviceOption = new("--libdevice", "--libDevice")
        {
            Description = "True to enable use of fast math functions via libDevice",
            DefaultValueFactory = _ => false
        };

        NvvmAPI? _nvvmAPI;

        /// <summary>
        /// Registers all options to control PTX compilation properties with the command.
        /// </summary>
        /// <param name="command">The command to attach options to.</param>
        public PTXOptions(Command command) : base(BackendType.PTX)
        {
            command.Add(_modeOption);
            command.Add(_cudaArchitecturesOption);
            command.Add(_cudaInstructionSetsOption);
            command.Add(_enableLibDeviceOption);
        }

        public override IEnumerable<PTXBackend> CreateBackends(ParseResult parseResult)
        {
            var backendMode = parseResult.GetRequiredValue(_modeOption);
            var architectures = parseResult
                .GetRequiredValue(_cudaArchitecturesOption)
                .Select(architecture =>
                {
                    if (!CudaArchitecture.TryParse(architecture, out var arch))
                        arch = CudaArchitecture.SM_80;
                    return arch;
                })
                .ToArray();
            var instructionSets = parseResult
                .GetRequiredValue(_cudaInstructionSetsOption)
                .Select(instructionSet =>
                {
                    if (!CudaInstructionSet.TryParse(instructionSet, out var isa))
                        isa = CudaInstructionSet.ISA_80;
                    return isa;
                })
                .ToArray();
            bool enableLibDevice = parseResult.GetRequiredValue(_enableLibDeviceOption);
            _nvvmAPI = enableLibDevice
                ? new LibDevice().CreateNvvmAPI()
                : null;

            foreach (var arch in architectures)
                foreach (var isa in instructionSets)
                    yield return new PTXBackend(backendMode, arch, isa, _nvvmAPI);
        }

        public override void WriteHumanReadableKernel(
            string outputPath,
            string kernelName,
            CompiledKernelData kernelData)
        {
            var fileName = Path.Combine(outputPath, $"{kernelName}.ptx");
            File.WriteAllText(fileName, kernelData.GetSourceAsString());
        }

        /// <summary>
        /// Frees internal NVVM API instances (if required).
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            _nvvmAPI?.Dispose();
            base.Dispose(disposing);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Resolves generic methods via packed command line arguments.
    /// </summary>
    /// <param name="assembly">The source assembly.</param>
    /// <param name="fullyQualifiedMethodName">The fully qualified method name.</param>
    /// <returns>The resolved method info object for processing.</returns>
    public static MethodInfo ResolveMethod(
        Assembly assembly,
        string fullyQualifiedMethodName)
    {
        // Step 1: Normalize input names
        string normalizedMethodName = KernelNameNormalizationRegex()
            .Replace(fullyQualifiedMethodName, match =>
                match.Value switch
                {
                    "__LT__" => "<",
                    "__GT__" => ">",
                    _ => match.Value
                });

        // Step 1: Split into type and method
        int lastDot = normalizedMethodName.LastIndexOf('.');
        if (lastDot == -1)
            throw new ArgumentException("Invalid method name");

        string typeName = normalizedMethodName[..lastDot];
        string methodName = normalizedMethodName[(lastDot + 1)..];

        // Step 2: Get the Type
        var type = assembly.GetType(typeName, throwOnError: true).ThrowIfNull();

        // Step 3: Match method name (possibly generic)
        // Note: MethodInfo.Name does NOT include arity like `1
        var genericSplits = methodName.Split('`', 2);
        string baseMethodName = genericSplits[0];

        var candidates = type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance)
            .Where(m => m.Name == baseMethodName);

        if (genericSplits.Length > 1 &&
            int.TryParse(genericSplits[1], out int genericArity))
        {
            candidates = candidates.Where(
                m => m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == genericArity);
        }

        return candidates.First();
    }

    [GeneratedRegex("__LT__|__GT__")]
    private static partial Regex KernelNameNormalizationRegex();

    #endregion
}
