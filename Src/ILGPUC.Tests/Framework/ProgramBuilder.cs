// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ProgramBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using ILGPUC.Backends;
using ILGPUC.Compilers;
using ILGPUC.Frontend;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.Transformations;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Roslyn.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynSymbols = Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using Xunit.Abstractions;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Orchestrates the full ILGPUC pipeline from test source to executable:
/// Roslyn parse → launch site analysis → kernel compilation → call site rewriting
/// → final Roslyn emit → ready to run.
/// </summary>
sealed class ProgramBuilder : DisposeBase
{
    private static int s_intrinsicsInitialized;

    private readonly string _tempDir;
    private readonly BackendType _backend;
    private readonly ITestOutputHelper? _output;

    public ProgramBuilder(BackendType backend, ITestOutputHelper? output = null)
    {
        _backend = backend;
        _output = output;
        _tempDir = Path.Combine(
            Path.GetTempPath(), $"ilgpuc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        CopyDependencies();
        _output?.WriteLine(
            $"ILGPU assembly: {typeof(ILGPU.Index1D).Assembly.Location}");
        EnsureIntrinsicsInitialized();
    }

    /// <summary>
    /// Builds an executable from test program source files.
    /// </summary>
    /// <param name="programSourceFiles">
    /// Paths to .cs source files for the test program.
    /// </param>
    /// <param name="kernelMethodFullNames">
    /// Fully qualified kernel method names like "Kernels.IfTrueKernel".
    /// These are used as a fallback when launch site analysis finds no kernels.
    /// With the new stream.Launch() API, kernels are auto-detected.
    /// </param>
    /// <param name="props">Optional compilation properties.</param>
    /// <returns>Path to the executable DLL (run with <c>dotnet</c>).</returns>
    public string BuildExecutable(
        string[] programSourceFiles,
        string[] kernelMethodFullNames,
        CompilationProperties? props = null)
    {
        var properties = props ?? new CompilationProperties();

        // Read source files
        var sources = programSourceFiles
            .Select(File.ReadAllText)
            .ToArray();

        // Resolve UNKNOWN placeholders: test programs with generic kernels
        // use UNKNOWN as a placeholder for the kernel method name. Replace
        // with the actual method name so Roslyn can resolve the symbol and
        // infer generic type arguments from the call-site argument types.
        if (kernelMethodFullNames.Length > 0)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i].Contains("UNKNOWN("))
                    sources[i] = sources[i].Replace(
                        "UNKNOWN", kernelMethodFullNames[0]);
            }
        }

        // For GPU backends, override the device preference so the test
        // program creates the matching accelerator type instead of CPU.
        if (_backend != BackendType.CPU)
        {
            for (int i = 0; i < sources.Length; i++)
                sources[i] = sources[i].Replace(
                    "preferCPU: true", "preferCPU: false");
        }

        // Step 1: Create a CSharpCompilation (no emit yet)
        var compilation = RoslynCompiler.CreateCompilation(
            sources, "program");

        // Step 2: Analyze launch sites and extract kernel descriptors
        var analysis = CompilationRewriter.Analyze(compilation);
        _output?.WriteLine(
            $"Found {analysis.AllKernels.Count} launch site(s), " +
            $"{analysis.UniqueKernels.Count} unique kernel(s)");

        if (analysis.UniqueKernels.Count == 0)
        {
            // Fallback: no stream.Launch() calls found — use legacy path
            return BuildExecutableLegacy(sources, kernelMethodFullNames, properties);
        }

        // Step 3: Compile each unique kernel via ILGPUC pipeline
        // For inline lambda kernels, synthesize a static method so we can
        // resolve them via reflection just like named kernels.
        var inlineSources = new List<string>();
        foreach (var kernel in analysis.UniqueKernels)
        {
            if (kernel.KernelMethod is null && kernel.KernelBody is not null)
            {
                var synth = SynthesizeInlineKernel(kernel);
                inlineSources.Add(synth);
                _output?.WriteLine(
                    $"Synthesized inline kernel: {kernel.KernelName}");
            }
        }

        // Emit a stub compilation so we can load the assembly and resolve
        // kernel MethodInfos. Include synthesized inline kernel sources so
        // they can be resolved via reflection.
        var stubRewriter = new CompilationRewriter();  // uses stubs
        var stubResult = stubRewriter.Rewrite(compilation, analysis);

        // Add synthesized inline kernel sources to the stub compilation
        var stubCompilation = stubResult.Compilation;
        if (inlineSources.Count > 0)
        {
            var parseOptions = CSharpParseOptions.Default
                .WithLanguageVersion(LanguageVersion.CSharp13);
            var inlineTrees = inlineSources.Select((src, i) =>
                CSharpSyntaxTree.ParseText(
                    src, parseOptions, $"inline{i}.cs")).ToArray();
            stubCompilation = stubCompilation.AddSyntaxTrees(inlineTrees);
        }

        var stubDll = Path.Combine(_tempDir, "stub.dll");
        RoslynCompiler.Emit(stubCompilation, stubDll);

        var alc = new AssemblyLoadContext(
            $"ProgramBuilder_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            alc.Resolving += (ctx, name) =>
            {
                if (name.Name == "ILGPU")
                    return typeof(ILGPU.Index1D).Assembly;
                return null;
            };

            var assembly = alc.LoadFromAssemblyPath(stubDll);
            var typeManager = new TypeInformationManager();
            var compiledSources = new Dictionary<string, string>();
            var classNamesByAccel =
                new Dictionary<AcceleratorType, List<string>>();

            foreach (var kernel in analysis.UniqueKernels)
            {
                var method = ResolveKernelMethod(assembly, kernel);
                if (method is null)
                {
                    // For inline kernels, try the synthesized holder class
                    method = ResolveInlineKernelMethod(assembly, kernel);
                }
                if (method is null)
                {
                    _output?.WriteLine(
                        $"Could not resolve kernel '{kernel.KernelName}', using stub");
                    continue;
                }

                // Extract user-facing C# type names from the Roslyn analysis
                var paramTypeNames = kernel.Parameters
                    .Select(p => LauncherStubGenerator.FormatTypeNamePublic(p.Type))
                    .ToArray();

                // Extract user property names for struct params (CPU marshaling).
                // Use LauncherStubGenerator.ExtractFieldAccessors so nested
                // struct fields are flattened to leaf accessors — required for
                // ArrayView2D/3D, whose Extent/Stride substructs the IR
                // decomposes into multiple primitive slots. The production
                // ilgpuc-build path uses the same helper from MainKernelProvider.
                // Coalesce null (non-struct param) to an empty array so the
                // jagged array type matches GenerateWrapper's non-nullable
                // inner-element contract.
                var fieldAccessors = kernel.Parameters
                    .Select(p =>
                        LauncherStubGenerator.ExtractFieldAccessors(p.Type) ?? [])
                    .ToArray();

                var result = GenerateWrapper(
                    method, properties, typeManager, kernel.KernelName,
                    paramTypeNames, fieldAccessors);
                compiledSources[kernel.KernelName] = result.SourceCode;
                _output?.WriteLine(
                    $"Compiled kernel: {kernel.KernelName} → {result.ClassName}");

                // Track class names per accelerator type for registrar
                if (!classNamesByAccel.TryGetValue(
                    result.AcceleratorType, out var classList))
                    classNamesByAccel[result.AcceleratorType] = classList = [];
                classList.Add(result.ClassName);
            }

            // Step 4: Rewrite with real compiled kernels
            // Use the ORIGINAL compilation and analysis — syntax node
            // references must match for CallSiteRewriter to find them.
            var provider = new PrecompiledKernelProvider(compiledSources);
            var rewriter = new CompilationRewriter(provider);
            var rewriteResult = rewriter.Rewrite(compilation, analysis);

            // Step 4b: Generate kernel registrar(s) — [ModuleInitializer]
            // that registers compiled kernels with the ILGPU context so
            // LoadKernels() can find them during accelerator init.
            var finalCompilation = rewriteResult.Compilation;
            foreach (var (accelType, classNames) in classNamesByAccel)
            {
                var registrarSrc = KernelRegistrarGenerator.Generate(
                    accelType, classNames);
                _output?.WriteLine(
                    $"Generated {accelType} registrar for: " +
                    string.Join(", ", classNames));
                var tree = CSharpSyntaxTree.ParseText(registrarSrc);
                finalCompilation = finalCompilation.AddSyntaxTrees(tree);
            }

            // Step 5: Emit final executable
            var exePath = Path.Combine(_tempDir, "program.dll");
            RoslynCompiler.Emit(finalCompilation, exePath);
            _output?.WriteLine($"Compiled executable: {exePath}");

            // Step 6: Generate runtimeconfig.json
            GenerateRuntimeConfig(exePath);

            return exePath;
        }
        finally
        {
            alc.Unload();
        }
    }

    /// <summary>
    /// Legacy path for programs that don't use stream.Launch() API.
    /// </summary>
    private string BuildExecutableLegacy(
        string[] sources,
        string[] kernelMethodFullNames,
        CompilationProperties properties)
    {
        var inputDll = Path.Combine(_tempDir, "input.dll");
        RoslynCompiler.CompileToLibrary(sources, inputDll);

        var alc = new AssemblyLoadContext(
            $"ProgramBuilder_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            alc.Resolving += (ctx, name) =>
            {
                if (name.Name == "ILGPU")
                    return typeof(ILGPU.Index1D).Assembly;
                return null;
            };

            var assembly = alc.LoadFromAssemblyPath(inputDll);
            var methods = ResolveKernelMethods(assembly, kernelMethodFullNames);

            var wrapperSources = new List<string>();
            var classNamesByAccel = new Dictionary<AcceleratorType, List<string>>();
            var typeManager = new TypeInformationManager();

            foreach (var method in methods)
            {
                var result = GenerateWrapper(method, properties, typeManager);
                wrapperSources.Add(result.SourceCode);

                if (!classNamesByAccel.TryGetValue(
                    result.AcceleratorType, out var list))
                    classNamesByAccel[result.AcceleratorType] = list = [];
                list.Add(result.ClassName);
            }

            var registrarSources = new List<string>();
            foreach (var (accelType, classNames) in classNamesByAccel)
            {
                registrarSources.Add(
                    KernelRegistrarGenerator.Generate(accelType, classNames));
            }

            var allSources = sources
                .Concat(wrapperSources)
                .Concat(registrarSources)
                .ToArray();

            var exePath = Path.Combine(_tempDir, "program.dll");
            RoslynCompiler.CompileToExecutable(allSources, exePath);
            GenerateRuntimeConfig(exePath);
            return exePath;
        }
        finally
        {
            alc.Unload();
        }
    }

    /// <summary>
    /// Resolves a kernel method from a KernelDescriptor.
    /// The descriptor has the kernel method's IMethodSymbol — we match by
    /// type name + method name in the loaded assembly.
    /// </summary>
    private static MethodInfo? ResolveKernelMethod(
        Assembly assembly, KernelDescriptor kernel)
    {
        if (kernel.KernelMethod is null)
            return null;

        var containingType = kernel.KernelMethod.ContainingType;
        var typeName = containingType.Name;
        var methodName = kernel.KernelMethod.Name;

        var type = assembly.GetTypes()
            .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
        if (type is null)
            return null;

        var method = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
            return null;

        // For generic methods, create the concrete instantiation using
        // the type arguments from the Roslyn symbol (e.g., AddKernel<T>
        // → AddKernel<int> based on call-site inference).
        if (method.IsGenericMethodDefinition
            && kernel.KernelMethod is IMethodSymbol { IsGenericMethod: true } sym)
        {
            var typeArgs = sym.TypeArguments
                .Select(ta => ResolveTypeFromSymbol(ta, assembly))
                .ToArray();
            method = method.MakeGenericMethod(typeArgs);
        }

        return method;
    }

    /// <summary>
    /// Resolves a CLR <see cref="Type"/> from a Roslyn type symbol,
    /// handling C# keyword aliases (int, long, float, etc.) and falling back
    /// to a name lookup against the test program's loaded assembly for
    /// user-defined struct/closure types.
    /// </summary>
    private static Type ResolveTypeFromSymbol(
        RoslynSymbols.ITypeSymbol typeSymbol,
        Assembly assembly)
    {
        // Handle special types (C# keywords) directly
        if (typeSymbol.SpecialType != RoslynSymbols.SpecialType.None)
        {
            return typeSymbol.SpecialType switch
            {
                RoslynSymbols.SpecialType.System_Boolean => typeof(bool),
                RoslynSymbols.SpecialType.System_Byte => typeof(byte),
                RoslynSymbols.SpecialType.System_SByte => typeof(sbyte),
                RoslynSymbols.SpecialType.System_Int16 => typeof(short),
                RoslynSymbols.SpecialType.System_UInt16 => typeof(ushort),
                RoslynSymbols.SpecialType.System_Int32 => typeof(int),
                RoslynSymbols.SpecialType.System_UInt32 => typeof(uint),
                RoslynSymbols.SpecialType.System_Int64 => typeof(long),
                RoslynSymbols.SpecialType.System_UInt64 => typeof(ulong),
                RoslynSymbols.SpecialType.System_Single => typeof(float),
                RoslynSymbols.SpecialType.System_Double => typeof(double),
                _ => typeof(int),
            };
        }

        var clrName = typeSymbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");

        // Try the test program's assembly first (custom struct closures
        // like AddOffsetClosure), then any loaded assembly via Type.GetType.
        var simpleName = typeSymbol.Name;
        return assembly.GetTypes()
                .FirstOrDefault(t => t.FullName == clrName || t.Name == simpleName)
            ?? Type.GetType(clrName)
            ?? typeof(int);
    }

    private CompiledKernelGenerationResult GenerateWrapper(
        MethodInfo method,
        CompilationProperties properties,
        TypeInformationManager typeManager,
        string? kernelName = null,
        string[]? launchParamTypeNames = null,
        string[][]? launchParamFieldAccessors = null)
    {
        // Frontend: MethodInfo → Module
        var frontend = new ILFrontend(_backend, _tempDir);
        frontend.LoadMethods([method]);

        var moduleBuilder = new ModuleBuilder(
            properties,
            new Generation(),
            ILGPUC.IR.Location.Unknown,
            typeManager);
        frontend.GenerateCode(moduleBuilder, [method]);

        var entryPointHandle = moduleBuilder.GetMethod(method).Declaration.Handle;
        var module = moduleBuilder.Seal(entryPointHandle);

        // Optimize (pre-backend-transform module)
        var optTransformer = properties.OptimizationLevel.CreateTransformer();
        var optimizedModule = optTransformer.Apply(properties, typeManager, module);

        // Backend codegen (applies backend transforms internally)
        var backend = CompilationHelper.CreateBackend(_backend);
        var compiled = backend.GenerateCode(properties, typeManager, optimizedModule);

        // Debug: dump generated GPU source
        {
            var dd = "/tmp/ilgpuc_debug";
            Directory.CreateDirectory(dd);
            File.WriteAllText(
                Path.Combine(dd, $"{kernelName ?? "kernel"}.metal"),
                compiled.SourceCode);
        }

        // Native compilation: produce binary if possible (Metal→metallib,
        // CUDA→cubin, etc.). Without binary, the generated class embeds
        // source and the GPU must compile at runtime, which may fail.
        byte[]? compiledBinary = null;
        if (backend.CreateCompiledKernelEmitter().EmbedMode == KernelEmbedMode.Binary)
        {
            try
            {
                // GenerateWrapper is synchronous; match the existing
                // sync-over-async pattern used for CompileSourceAsync
                // below. The probe runs at most once per backend per
                // process so the blocking cost is bounded.
                var target = Availability.MapToTarget(_backend)
                    ?? throw new InvalidOperationException(
                        $"Backend '{_backend}' has no native compilation target.");
                var compilerManager = CompilerManagerFactory
                    .ResolveAsync(target)
                    .GetAwaiter().GetResult();
                var nativeResult = backend
                    .CompileSourceAsync(compiled, compilerManager)
                    .GetAwaiter().GetResult();
                if (nativeResult is { Success: true, Output: not null })
                {
                    compiledBinary = Convert.FromBase64String(nativeResult.Output);
                    _output?.WriteLine(
                        $"Native binary: {compiledBinary.Length} bytes");
                }
                else
                {
                    _output?.WriteLine(
                        $"Native compilation failed: " +
                        $"success={nativeResult?.Success} " +
                        $"stderr={nativeResult?.StdErr}");
                }
            }
            catch (Exception ex)
            {
                _output?.WriteLine(
                    $"Native compilation error: {ex.Message}");
            }
        }

        // Generate CompiledKernel wrapper
        using var kernelGen = new CompiledKernelGenerator(
            backend.CreateCompiledKernelEmitter(),
            backend.CreateLauncherEmitter(),
            optimizedModule,  // pre-backend-transform (has ViewType)
            compiled,         // post-backend-transform code
            Guid.NewGuid(),
            compiledBinary: compiledBinary,
            kernelName: kernelName,
            launchParamTypeNames: launchParamTypeNames,
            launchParamFieldAccessors: launchParamFieldAccessors);

        var genResult = kernelGen.Generate();

        // Debug: dump generated wrapper source
        var debugDir = "/tmp/ilgpuc_debug";
        Directory.CreateDirectory(debugDir);
        File.WriteAllText(
            Path.Combine(debugDir, $"{genResult.ClassName}.cs"),
            genResult.SourceCode);

        return genResult;
    }

    private static MethodInfo[] ResolveKernelMethods(
        Assembly assembly, string[] fullNames)
    {
        return fullNames.Select(name =>
        {
            var lastDot = name.LastIndexOf('.');
            if (lastDot < 0)
                throw new ArgumentException(
                    $"Invalid kernel name '{name}'. Expected 'TypeName.MethodName'.");

            var typeName = name[..lastDot];
            var methodName = name[(lastDot + 1)..];

            var type = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName)
                ?? throw new ArgumentException(
                    $"Type '{typeName}' not found in assembly '{assembly.Location}'");

            return type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new ArgumentException(
                    $"Method '{methodName}' not found on type '{typeName}'");
        }).ToArray();
    }

    private static void GenerateRuntimeConfig(string exePath)
    {
        var major = Environment.Version.Major;
        var configPath = Path.ChangeExtension(exePath, ".runtimeconfig.json");
        File.WriteAllText(configPath, $$"""
            {
              "runtimeOptions": {
                "tfm": "net{{major}}.0",
                "framework": {
                  "name": "Microsoft.NETCore.App",
                  "version": "{{major}}.0.0"
                },
                "rollForward": "Major"
              }
            }
            """);
    }

    /// <summary>
    /// Copy ILGPU.dll (and potentially other dependencies) to the temp dir
    /// so Mono.Cecil (ILFrontend) can resolve assembly references.
    /// </summary>
    private void CopyDependencies()
    {
        var ilgpuPath = typeof(ILGPU.Index1D).Assembly.Location;
        if (!string.IsNullOrEmpty(ilgpuPath))
        {
            File.Copy(
                ilgpuPath,
                Path.Combine(_tempDir, "ILGPU.dll"),
                overwrite: true);

            // Copy ILGPU's dependencies (e.g., System.Numerics.Tensors)
            // so the subprocess can resolve them at runtime
            var ilgpuDir = Path.GetDirectoryName(ilgpuPath);
            if (ilgpuDir != null)
            {
                foreach (var dep in new[]
                {
                    "System.Numerics.Tensors.dll",
                })
                {
                    var depPath = Path.Combine(ilgpuDir, dep);
                    if (File.Exists(depPath))
                        File.Copy(
                            depPath,
                            Path.Combine(_tempDir, dep),
                            overwrite: true);
                }
            }
        }
    }

    private static void EnsureIntrinsicsInitialized()
    {
        if (Interlocked.CompareExchange(ref s_intrinsicsInitialized, 1, 0) == 0)
            Intrinsics.Init();
    }

    /// <summary>
    /// Synthesizes a C# source file containing a static method from an inline
    /// lambda kernel body. The method receives the index parameter first,
    /// followed by all captured variables as explicit parameters.
    /// </summary>
    private static string SynthesizeInlineKernel(KernelDescriptor kernel)
    {
        // Extract index parameter name from the lambda syntax
        var indexParamName = kernel.LaunchInfo.KernelArgument switch
        {
            SimpleLambdaExpressionSyntax simple =>
                simple.Parameter.Identifier.Text,
            ParenthesizedLambdaExpressionSyntax paren =>
                paren.ParameterList.Parameters[0].Identifier.Text,
            _ => "index",
        };

        // Determine the index type from the launch variant
        var indexType = kernel.Variant switch
        {
            LaunchVariant.Auto1D => "Index1D",
            LaunchVariant.AutoLong1D => "LongIndex1D",
            LaunchVariant.Grouped => "KernelIndex",
            LaunchVariant.Auto2D or LaunchVariant.Auto2DStride => "Index2D",
            LaunchVariant.AutoLong2D or LaunchVariant.AutoLong2DStride
                => "LongIndex2D",
            LaunchVariant.Auto3D or LaunchVariant.Auto3DStride => "Index3D",
            LaunchVariant.AutoLong3D or LaunchVariant.AutoLong3DStride
                => "LongIndex3D",
            _ => "Index1D",
        };

        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using ILGPU;");
        sb.AppendLine("using ILGPU.Runtime;");
        sb.AppendLine();
        sb.AppendLine(
            $"static class {kernel.KernelName}_Holder");
        sb.AppendLine("{");

        // Method signature: index param + captured params
        sb.Append(
            $"    public static void {kernel.KernelName}(");
        sb.Append($"{indexType} {indexParamName}");

        foreach (var param in kernel.Parameters)
        {
            var typeName =
                LauncherStubGenerator.FormatTypeNamePublic(param.Type);
            sb.Append($", {typeName} {param.Name}");
        }
        sb.AppendLine(")");

        // Rewrite body: for buffer-to-view captures, strip ".View" from
        // member access since the parameter is already the view type.
        var bodyText = kernel.KernelBody!.ToFullString();
        foreach (var param in kernel.Parameters)
        {
            var sourceText = param.SourceExpression.ToString();
            if (sourceText != param.Name)
            {
                // e.g., "buffer.View" → "buffer"
                bodyText = bodyText.Replace(sourceText, param.Name);
            }
        }

        if (kernel.KernelBody is BlockSyntax)
        {
            sb.AppendLine($"    {bodyText}");
        }
        else
        {
            // Expression body — wrap in a statement block
            sb.AppendLine("    {");
            sb.AppendLine($"        {bodyText};");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Resolves a synthesized inline kernel method from its holder class.
    /// </summary>
    private static MethodInfo? ResolveInlineKernelMethod(
        Assembly assembly, KernelDescriptor kernel)
    {
        var holderTypeName = $"{kernel.KernelName}_Holder";
        var type = assembly.GetTypes()
            .FirstOrDefault(t => t.Name == holderTypeName);
        if (type is null)
            return null;

        return type.GetMethod(
            kernel.KernelName,
            BindingFlags.Public | BindingFlags.Static);
    }

    protected override void Dispose(bool disposing)
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }

        base.Dispose(disposing);
    }
}
