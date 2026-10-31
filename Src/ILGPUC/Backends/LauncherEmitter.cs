// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LauncherEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends.CPU;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;

namespace ILGPUC.Backends;

/// <summary>
/// Classifies a kernel parameter for marshaling purposes.
/// </summary>
enum ParameterKind
{
    /// <summary>
    /// A primitive type (int, float, etc.) — pass through unchanged.
    /// </summary>
    Primitive,

    /// <summary>
    /// An ArrayView&lt;T&gt; — convert to ViewImplementation&lt;T&gt;.
    /// </summary>
    View,

    /// <summary>
    /// A struct containing one or more view fields (possibly nested).
    /// </summary>
    StructWithViews,

    /// <summary>
    /// A struct with no view fields — pass through unchanged.
    /// </summary>
    StructPlain,

    /// <summary>
    /// A pointer type — pass as IntPtr.
    /// </summary>
    Pointer,
}

/// <summary>
/// Holds analyzed information about a single kernel parameter for marshaling.
/// </summary>
sealed class ParameterInfo
{
    /// <summary>
    /// The parameter index in the original method signature.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// The generated parameter name (e.g., "param_data").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The IR type of the parameter.
    /// </summary>
    public required TypeValue Type { get; init; }

    /// <summary>
    /// The marshaling classification of this parameter.
    /// </summary>
    public required ParameterKind Kind { get; init; }

    /// <summary>
    /// The C# type name used in the Launch method signature
    /// (e.g., "ArrayView&lt;float&gt;", "int").
    /// </summary>
    public required string CSharpTypeName { get; init; }

    /// <summary>
    /// The marshaled type name used in the KernelArgs struct
    /// (e.g., "ViewImplementation&lt;float&gt;", "int").
    /// </summary>
    public required string MarshaledTypeName { get; init; }

    /// <summary>
    /// The post-backend-transform type name used in the KernelEntryPoint
    /// signature (e.g., "CPURuntimeView&lt;int&gt;", "struct_4270").
    /// Set by the CPU backend. Null for non-CPU backends.
    /// </summary>
    public string? EntryPointTypeName { get; init; }

    /// <summary>
    /// The actual type name used in the generated Launch method signature.
    /// Matches the user-facing type (e.g., "TestStruct", "ArrayView1D&lt;uint,
    /// Stride1D.Dense&gt;") when Roslyn-extracted names are available.
    /// </summary>
    public string? LaunchTypeName { get; set; }

    /// <summary>
    /// User-facing property/field names for struct parameters, in IR field
    /// order. Used by the CPU launcher to generate field-by-field marshaling
    /// from user types to IR struct types. Null for non-struct params.
    /// </summary>
    public string[]? UserFieldAccessors { get; init; }

    /// <summary>
    /// Returns the expression of the basic <c>ArrayView&lt;T&gt;</c> of a view
    /// parameter: the parameter itself if it is launched as an
    /// <c>ArrayView&lt;T&gt;</c>, and its <c>BaseView</c> for the
    /// <c>ArrayView1D</c> family of views.
    /// </summary>
    public string BaseViewExpression =>
        (LaunchTypeName ?? CSharpTypeName).StartsWith(
            "ArrayView<",
            System.StringComparison.Ordinal)
            ? Name
            : $"{Name}.BaseView";
}

/// <summary>
/// Provides shared helpers from the kernel generator to
/// platform-specific launcher emitters.
/// </summary>
sealed class LauncherEmissionContext
{
    /// <summary>
    /// Writes an indented line of source code to the output.
    /// </summary>
    public required Action<string> WriteLine { get; init; }

    /// <summary>
    /// Opens a new brace scope (<c>{</c>) and increments indentation.
    /// </summary>
    public required Action OpenScope { get; init; }

    /// <summary>
    /// Closes the current brace scope (<c>}</c>) and decrements indentation.
    /// </summary>
    public required Action CloseScope { get; init; }

    /// <summary>
    /// Increments the current indentation level by one.
    /// </summary>
    public required Action IncrementIndent { get; init; }

    /// <summary>
    /// Decrements the current indentation level by one.
    /// </summary>
    public required Action DecrementIndent { get; init; }

    /// <summary>
    /// Emits the complete KernelArgs marshaling block, converting all
    /// parameters (views, structs, primitives) into their marshaled forms.
    /// </summary>
    public required Action<List<ParameterInfo>> EmitArgsMarshalingBlock { get; init; }

    /// <summary>
    /// Recursively emits marshaling code for a struct type, converting view
    /// fields to <c>ViewImplementation</c> instances. The optional fourth
    /// argument carries user-side accessor names for the source struct so
    /// the wrapper can read fields like <c>ArrayView1D.BaseView</c> instead
    /// of the IR-internal <c>Field0</c>/<c>Field1</c>/... names that don't
    /// exist on user-facing types.
    /// </summary>
    public required Action<StructureType, string, string, string[]?>
        EmitStructMarshalingCode
    {
        get; init;
    }

    /// <summary>
    /// Returns the C# element type name for a view type (e.g., "float").
    /// </summary>
    public required Func<ViewType, string> GetElementTypeName { get; init; }

    /// <summary>
    /// Returns the C# primitive type name for an IR type (e.g., "int", "double").
    /// </summary>
    public required Func<TypeValue, string> GetPrimitiveTypeName { get; init; }

    /// <summary>
    /// Returns the marshaled struct name for a structure type (e.g., "Marshaled_42").
    /// </summary>
    public required Func<StructureType, string> GetMarshaledStructName { get; init; }

    /// <summary>
    /// The kernel class name, used by the CPU backend for direct method dispatch.
    /// </summary>
    public string? KernelClassName { get; init; }

    /// <summary>
    /// The kernel name as a C# string literal, which the generated launcher
    /// reports when the kernel cannot be launched on a stream.
    /// </summary>
    public string KernelNameLiteral { get; init; } = "\"\"";

    /// <summary>
    /// The accelerator type the kernel is compiled for as a fully qualified C#
    /// expression.
    /// </summary>
    public string AcceleratorTypeExpression { get; init; } =
        "ILGPU.Runtime.AcceleratorType.None";

    /// <summary>
    /// The SIMD vectorization width, used by the CPU backend.
    /// </summary>
    public int SimdWidth { get; init; }

    /// <summary>
    /// Buffer pool metadata for pre-allocated vector buffers (CPU backend).
    /// When set, the launcher emits pool allocations before the kernel loop
    /// and passes them as extra parameters.
    /// </summary>
    public BufferPoolMetadata? BufferPool { get; init; }

    /// <summary>
    /// The effective kernel embedding mode for the current generation.
    /// Used by platform-specific emitters to conditionally emit constructor args.
    /// </summary>
    public KernelEmbedMode ActiveEmbedMode { get; init; }

    /// <summary>
    /// The math mode the kernel was compiled with. Used by emitters whose
    /// runtime compiles embedded source and needs the matching math options.
    /// </summary>
    public MathMode MathMode { get; init; }

    /// <summary>
    /// True if the kernel keeps its thread storage away from address 0, which
    /// the Metal runtime checks when it loads the kernel.
    /// </summary>
    public bool NullSafeLocals { get; init; } = true;

    /// <summary>
    /// Number of index dimensions (1 = scalar, 2 = Index2D, 3 = Index3D).
    /// Used by the CPU backend to decompose linear indices into multi-dim
    /// struct fields. Set by CompiledKernelGenerator from the entry point's
    /// first parameter type.
    /// </summary>
    public int IndexDimensions { get; set; } = 1;

    /// <summary>
    /// The post-backend-transform name of the <c>KernelIndex</c> struct type
    /// when the entry point's first parameter is <c>KernelIndex</c>
    /// (grouped launch with GridIndex/GroupIndex access). <see langword="null"/>
    /// otherwise. The CPU launcher uses this to construct a per-thread
    /// <c>KernelIndex</c> struct before calling KernelEntryPoint.
    /// </summary>
    public string? KernelIndexTypeName { get; set; }

    /// <summary>
    /// How the entry point is launched.
    /// </summary>
    public EntryPointLaunch Launch { get; set; }

    /// <summary>
    /// The generated device source code of the kernel.
    /// </summary>
    public string? KernelSourceCode { get; init; }
}

/// <summary>
/// Abstract base for platform-specific kernel launcher emission strategies.
/// </summary>
abstract class LauncherEmitter
{
    /// <summary>
    /// Returns the platform-specific using directives needed in the launcher header.
    /// </summary>
    public abstract string[] RequiredUsings { get; }

    /// <summary>
    /// Whether this backend requires a flat KernelArgs struct for dispatch.
    /// </summary>
    public abstract bool NeedsKernelArgsStruct { get; }

    /// <summary>
    /// Whether implicitly grouped launches pass the launch extent to the
    /// kernel as hidden trailing arguments. Must match
    /// <see cref="LanguageConfiguration.LaunchExtentTypeName"/> of the
    /// backend's language configuration.
    /// </summary>
    public virtual bool PassesLaunchExtents => false;

    /// <summary>
    /// Emits the platform-specific launch body inside the Launch method.
    /// </summary>
    public abstract void EmitLaunchBody(
        LauncherEmissionContext ctx,
        List<ParameterInfo> parameters);

    /// <summary>
    /// Returns the C# expressions for the hidden launch-extent arguments of
    /// implicitly grouped launches, one per index dimension (empty for
    /// grouped launches).
    /// </summary>
    protected static string[] GetLaunchExtentExpressions(LauncherEmissionContext ctx)
    {
        if (!ctx.Launch.IsImplicit)
            return [];
        return ctx.Launch.IndexDimensions switch
        {
            3 => ["(long)_dimX", "(long)_dimY", "(long)_dimZ"],
            2 => ["(long)_dimX", "(long)_dimY"],
            _ => ["userExtent"],
        };
    }

    /// <summary>
    /// Emits a small block of C# that derives explicit 3-D grid and block
    /// dimensions (<c>_ilgpuc_gX/Y/Z</c> / <c>_ilgpuc_bX/Y/Z</c>). Grouped
    /// launches use the user's <c>KernelConfig</c> as is. Implicitly grouped
    /// launches distribute the auto group size (<c>launchConfig.GroupSize</c>)
    /// across the dimensions of the user extent (<c>userExtent</c>, or
    /// <c>_dimX</c>/<c>_dimY</c>/<c>_dimZ</c> for 2D/3D) and round the grid up
    /// to whole groups; the kernel skips the extra threads.
    /// </summary>
    protected static void EmitLaunchDimensions(LauncherEmissionContext ctx)
    {
        ctx.WriteLine(
            "int _ilgpuc_groupSize = launchConfig.GroupSize > 0 " +
            "? launchConfig.GroupSize : 1;");
        ctx.WriteLine("int _ilgpuc_bX, _ilgpuc_bY, _ilgpuc_bZ;");
        ctx.WriteLine("int _ilgpuc_gX, _ilgpuc_gY, _ilgpuc_gZ;");
        if (ctx.Launch.IsGrouped)
        {
            ctx.WriteLine("_ilgpuc_bX = _ilgpuc_groupSize;");
            ctx.WriteLine("_ilgpuc_gX = (int)launchConfig.GridSize;");
            ctx.WriteLine("_ilgpuc_bY = _ilgpuc_bZ = _ilgpuc_gY = _ilgpuc_gZ = 1;");
            return;
        }

        switch (ctx.Launch.IndexDimensions)
        {
            case 3:
                ctx.WriteLine(
                    "_ilgpuc_bX = System.Math.Max(1, " +
                    "System.Math.Min(_dimX, _ilgpuc_groupSize));");
                ctx.WriteLine(
                    "_ilgpuc_bY = System.Math.Max(1, System.Math.Min(_dimY, " +
                    "_ilgpuc_groupSize / _ilgpuc_bX));");
                ctx.WriteLine(
                    "_ilgpuc_bZ = System.Math.Max(1, System.Math.Min(_dimZ, " +
                    "_ilgpuc_groupSize / (_ilgpuc_bX * _ilgpuc_bY)));");
                ctx.WriteLine(
                    "_ilgpuc_gX = (_dimX + _ilgpuc_bX - 1) / _ilgpuc_bX;");
                ctx.WriteLine(
                    "_ilgpuc_gY = (_dimY + _ilgpuc_bY - 1) / _ilgpuc_bY;");
                ctx.WriteLine(
                    "_ilgpuc_gZ = (_dimZ + _ilgpuc_bZ - 1) / _ilgpuc_bZ;");
                break;
            case 2:
                ctx.WriteLine(
                    "_ilgpuc_bX = System.Math.Max(1, " +
                    "System.Math.Min(_dimX, _ilgpuc_groupSize));");
                ctx.WriteLine(
                    "_ilgpuc_bY = System.Math.Max(1, System.Math.Min(_dimY, " +
                    "_ilgpuc_groupSize / _ilgpuc_bX));");
                ctx.WriteLine("_ilgpuc_bZ = 1;");
                ctx.WriteLine(
                    "_ilgpuc_gX = (_dimX + _ilgpuc_bX - 1) / _ilgpuc_bX;");
                ctx.WriteLine(
                    "_ilgpuc_gY = (_dimY + _ilgpuc_bY - 1) / _ilgpuc_bY;");
                ctx.WriteLine("_ilgpuc_gZ = 1;");
                break;
            default:
                // A single block of exactly userExtent threads when it fits,
                // otherwise auto-sized blocks rounded up to cover the extent.
                ctx.WriteLine(
                    "long _ilgpuc_extent = userExtent > 0 ? userExtent " +
                    ": (long)launchConfig.GridSize * _ilgpuc_groupSize;");
                ctx.WriteLine(
                    "int _ilgpuc_maxBlock = " +
                    "stream.Accelerator?.MaxNumThreadsPerGroup ?? " +
                    "_ilgpuc_groupSize;");
                ctx.WriteLine(
                    "_ilgpuc_bX = _ilgpuc_extent <= _ilgpuc_maxBlock " +
                    "? (int)_ilgpuc_extent " +
                    ": _ilgpuc_groupSize;");
                ctx.WriteLine("if (_ilgpuc_bX < 1) _ilgpuc_bX = 1;");
                ctx.WriteLine(
                    "_ilgpuc_gX = (int)" +
                    "((_ilgpuc_extent + _ilgpuc_bX - 1) / _ilgpuc_bX);");
                ctx.WriteLine("if (_ilgpuc_gX < 1) _ilgpuc_gX = 1;");
                ctx.WriteLine("_ilgpuc_bY = _ilgpuc_bZ = _ilgpuc_gY = _ilgpuc_gZ = 1;");
                break;
        }
    }
}
