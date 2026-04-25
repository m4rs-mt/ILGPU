// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using System.IO;
using System.Text;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// C# code generator with array-based vectorization support.
/// </summary>
/// <remarks>
/// Generates complete C# code using T[] arrays for vectorized operations.
/// All vectorized values are represented as T[] arrays, with math operations
/// delegated to CPUMathIntrinsics (backed by TensorPrimitives for SIMD execution).
/// </remarks>
sealed class CPUCodeGenerator(
    Module module,
    LanguageConfiguration languageConfiguration,
    int vectorWidth,
    bool emitDebugSymbols,
    bool poolBuffers = true)
{
    /// <summary>
    /// Gets the module being compiled.
    /// </summary>
    public Module Module => module;

    /// <summary>
    /// Gets the language configuration.
    /// </summary>
    public LanguageConfiguration Configuration => languageConfiguration;

    /// <summary>
    /// Gets whether to emit debug symbols.
    /// </summary>
    public bool EmitDebugSymbols => emitDebugSymbols;

    /// <summary>
    /// Returns the vector width (number of SIMD lanes).
    /// </summary>
    public int VectorWidth => vectorWidth;

    /// <summary>
    /// Generates C# code with array-based vectorization.
    /// </summary>
    public CodeGenerationResult GenerateCode()
    {
        var builder = new StringBuilder(16 * 1024);
        using var writer = new StringWriter(builder);
        var context = new GenerationContext(module, languageConfiguration, writer);

        // Generate header with includes
        GenerateHeader(context);

        // Generate type declarations
        GenerateTypeDeclarations(context);

        // Wrap all method implementations in a static class named after the kernel
        var className = module.EntryPoint is not null
            ? GenerationContext.GetClassName(module.EntryPoint)
            : "KernelClass";

        context.WriteLine($"public static class {className}");
        context.OpenScope();
        context.WriteLine($"public const int SIMDWidth = {vectorWidth};");
        context.WriteLine();

        // Generate vectorized method implementations
        // The emitter computes BufferPool during emission
        var methodEmitter = GenerateVectorizedMethodImplementations(context);

        context.CloseScope();

        return new CodeGenerationResult(
            writer.ToString(),
            GetEntryPointName(),
            BufferPool: methodEmitter?.BufferPool,
            KernelClassName: className,
            EntryPointParamTypeNames: GetEntryPointParamTypeNames(context),
            EntryPointIndexTypeName: GetEntryPointIndexTypeName(context));
    }

    /// <summary>
    /// Returns the post-backend-transform type name of the entry point's
    /// index parameter when it is a <c>KernelIndex</c>-shaped struct.
    /// <see langword="null"/> for scalar indices (<c>Index1D</c>) and for
    /// grouped launches without an index parameter — callers derive scalar
    /// index wiring from <see cref="LauncherEmissionContext.IndexDimensions"/>.
    /// </summary>
    private string? GetEntryPointIndexTypeName(GenerationContext context)
    {
        if (module.EntryPoint is null || module.EntryPoint.Parameters.Count == 0)
            return null;
        if (module.EntryPoint.Parameters[0].Type is not StructureType st
            || !CPUMethodEmitter.IsKernelIndexStructType(st))
        {
            return null;
        }
        var typeEmitter = new TypeEmitter(context);
        return typeEmitter.GetTypeName(st);
    }

    /// <summary>
    /// Collects the post-backend-transform type names for the entry point
    /// parameters. For auto-sized launches, skips parameter 0 (the implicit
    /// thread index). For grouped launches, includes all parameters.
    /// </summary>
    private string[]? GetEntryPointParamTypeNames(GenerationContext context)
    {
        if (module.EntryPoint is null)
            return null;

        var typeEmitter = new TypeEmitter(context);
        var parameters = module.EntryPoint.Parameters;

        // Detect whether param 0 is the thread index (Int32 for Index1D,
        // Index2D/3D struct, or KernelIndex for grouped launches). Pure
        // grouped kernels without an index parameter start marshaling from
        // parameter 0.
        bool hasIndexParam = parameters.Count > 0
            && (parameters[0].Type is PrimitiveType
                    { BasicValueType: BasicValueType.Int32 }
                || (parameters[0].Type is StructureType st
                    && (CPUMethodEmitter.IsIndexStructType(st)
                        || CPUMethodEmitter.IsKernelIndexStructType(st))));
        int startParam = hasIndexParam ? 1 : 0;

        var result = new string[parameters.Count - startParam];
        for (int i = startParam; i < parameters.Count; i++)
            result[i - startParam] = typeEmitter.GetTypeName(parameters[i].Type);

        return result;
    }

    /// <summary>
    /// Generates the file header (includes, pragmas, etc).
    /// </summary>
    private void GenerateHeader(GenerationContext context)
    {
        context.WriteLine("// ---------------------------------------------------");
        context.WriteLine("// Generated by ILGPU Compiler (C# Array Backend)");
        context.WriteLine("// ---------------------------------------------------");
        context.WriteLine();

        foreach (var include in languageConfiguration.GetHeaderIncludes())
            context.WriteLine(include);
        context.WriteLine();
    }

    /// <summary>
    /// Generates all type declarations.
    /// </summary>
    private static void GenerateTypeDeclarations(GenerationContext context)
    {
        var typeEmitter = new TypeEmitter(context);
        typeEmitter.EmitTypeDeclarations();
    }

    /// <summary>
    /// Gets the name of the entry point method.
    /// </summary>
    private string GetEntryPointName() =>
        module.EntryPoint != null ? "KernelEntryPoint" : "kernel";

    /// <summary>
    /// Generates vectorized method implementations.
    /// Returns the emitter so the caller can retrieve BufferPool metadata.
    /// </summary>
    private CPUMethodEmitter GenerateVectorizedMethodImplementations(
        GenerationContext context)
    {
        var methodEmitter = new CPUMethodEmitter(
            context,
            emitDebugSymbols,
            poolBuffers);
        methodEmitter.EmitMethodImplementations();
        return methodEmitter;
    }
}
