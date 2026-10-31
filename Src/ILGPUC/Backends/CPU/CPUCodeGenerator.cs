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
using ILGPUC.IR.BasicBlockValues;
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
    bool poolBuffers,
    EntryPointLaunch launch)
{
    /// <summary>
    /// Generates C# code with array-based vectorization.
    /// </summary>
    public CodeGenerationResult GenerateCode()
    {
        var builder = new StringBuilder(16 * 1024);
        using var writer = new StringWriter(builder);
        var context = new GenerationContext(
            module, languageConfiguration, writer, launch);

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

        // The launcher runs one group per call and publishes the launch
        // geometry, which helper methods read as well
        foreach (var name in new[]
        {
            CPUIntrinsicEmitter.GridIndexName,
            CPUIntrinsicEmitter.GridDimensionName,
            CPUIntrinsicEmitter.GroupDimensionName,
            CPUIntrinsicEmitter.GroupOffsetName,
        })
        {
            context.WriteLine($"[System.ThreadStatic] public static int {name};");
        }

        // Barriers and shared memory span the threads of one call only
        context.WriteLine(
            $"public const bool {SynchronizesGroupsName} = " +
            $"{(SynchronizesGroups() ? "true" : "false")};");
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
    /// The name of the constant telling the launcher whether the kernel
    /// synchronizes the threads of its group.
    /// </summary>
    internal const string SynchronizesGroupsName = "SynchronizesGroups";

    /// <summary>
    /// Returns <see langword="true"/> if the kernel synchronizes the threads of
    /// a group: through group barriers, group broadcasts or shared memory.
    /// </summary>
    private bool SynchronizesGroups()
    {
        foreach (var global in module.Globals)
        {
            if (global.AddressSpace == MemoryAddressSpace.Shared)
                return true;
        }
        bool synchronizes = false;
        foreach (var method in module.Methods)
        {
            method.Blocks.ForEachValue<BarrierOperation>(barrier =>
                synchronizes |= barrier switch
                {
                    Barrier b => b.Kind == BarrierKind.GroupLevel,
                    PredicateBarrier p =>
                        p.Kind == PredicateBarrierKind.GroupLevel,
                    _ => false,
                });
            method.Blocks.ForEachValue<Broadcast>(broadcast =>
                synchronizes |= broadcast.Kind == BroadcastKind.GroupLevel);
        }
        return synchronizes;
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
        if (module.EntryPoint is null
            || module.EntryPoint.Parameters.Count == 0
            || !context.Launch.HasKernelIndex)
        {
            return null;
        }
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
    /// parameters. Skips parameter 0 if it is the thread index: for
    /// auto-sized launches and grouped launches with a <c>KernelIndex</c>.
    /// Grouped launches without an index parameter include all parameters.
    /// </summary>
    private string[]? GetEntryPointParamTypeNames(GenerationContext context)
    {
        if (module.EntryPoint is null)
            return null;

        var typeEmitter = new TypeEmitter(context);
        var parameters = module.EntryPoint.Parameters;

        // The launch tells whether param 0 is the thread index, as in the
        // compiled kernel generator: the first parameter of a grouped kernel
        // without an index parameter may have the type of an index
        int startParam = context.Launch.HasIndexParameter && parameters.Count > 0
            ? 1
            : 0;

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
