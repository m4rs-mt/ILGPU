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
    /// Emits the platform-specific launch body inside the Launch method.
    /// </summary>
    public abstract void EmitLaunchBody(
        LauncherEmissionContext ctx,
        List<ParameterInfo> parameters);
}
