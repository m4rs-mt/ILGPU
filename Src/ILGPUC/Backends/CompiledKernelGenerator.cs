// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompiledKernelGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ILGPUC.Backends;

/// <summary>
/// Result of compiled kernel generation.
/// </summary>
/// <param name="SourceCode">The generated C# source code.</param>
/// <param name="ClassName">The name of the generated class.</param>
/// <param name="AcceleratorType">The backend's accelerator type.</param>
readonly record struct CompiledKernelGenerationResult(
    string SourceCode,
    string ClassName,
    AcceleratorType AcceleratorType);

/// <summary>
/// Generates a single self-contained C# class per kernel that derives from a
/// backend-specific <see cref="CompiledKernel"/> subclass.
/// Embeds kernel source, metadata, marshaling structs, and the launcher in one file.
/// </summary>
sealed class CompiledKernelGenerator : DisposeBase
{
    /// <summary>Backend-specific emitter for the compiled kernel class.</summary>
    private readonly CompiledKernelEmitter _compiledKernelEmitter;
    /// <summary>Backend-specific emitter for the launch body.</summary>
    private readonly LauncherEmitter _launcherEmitter;
    /// <summary>The pre-backend-transform IR module.</summary>
    private readonly Module _module;
    /// <summary>
    /// The code generation result containing source code and entry point name.
    /// </summary>
    private readonly CodeGenerationResult _compiled;
    /// <summary>The kernel's compile-time GUID.</summary>
    private readonly Guid _kernelGuid;
    /// <summary>Optional pre-compiled binary data.</summary>
    private readonly byte[]? _compiledBinary;
    /// <summary>
    /// The effective embed mode after considering binary availability</summary>
    private readonly KernelEmbedMode _effectiveEmbedMode;
    /// <summary>The kernel class name (for CPU backend).</summary>
    private readonly string? _kernelClassName;
    /// <summary>The SIMD width (for CPU backend).</summary>
    private readonly int _simdWidth;
    /// <summary>User-facing kernel name for the generated class.</summary>
    private readonly string? _kernelName;
    /// <summary>User-facing C# type names for Launch method parameters.</summary>
    private readonly string[]? _launchParamTypeNames;
    /// <summary>User property names per struct param for CPU marshaling.</summary>
    private readonly string[]?[]? _launchParamFieldAccessors;
    /// <summary>
    /// When set, overrides the heuristic index-dimension detection in
    /// <see cref="EmitLaunchMethod"/>. The Roslyn pipeline uses this to
    /// ensure the generated Launch signature matches the stub and call-site
    /// rewriter (which both derive dimensions from <c>LaunchVariant</c>).
    /// </summary>
    private readonly int? _indexDimOverride;
    /// <summary>The string builder accumulating the generated source code.</summary>
    private readonly StringBuilder _builder;
    /// <summary>The text writer wrapping the string builder.</summary>
    private readonly StringWriter _writer;
    /// <summary>The current indentation level.</summary>
    private int _indent;

    /// <summary>
    /// Constructs a new compiled kernel generator.
    /// </summary>
    /// <param name="compiledKernelEmitter">
    /// Backend-specific emitter for the compiled kernel class.
    /// </param>
    /// <param name="launcherEmitter">
    /// Backend-specific emitter for the launch body.
    /// </param>
    /// <param name="module">
    /// The pre-backend-transform module (still has ViewType info).
    /// </param>
    /// <param name="compiled">
    /// The code generation result containing source code and entry point name.
    /// </param>
    /// <param name="kernelGuid">The kernel's compile-time GUID.</param>
    /// <param name="compiledBinary">Optional pre-compiled binary data.</param>
    /// <param name="kernelClassName">The kernel class name (for CPU backend).</param>
    /// <param name="simdWidth">The SIMD width (for CPU backend).</param>
    /// <param name="kernelName">
    /// User-facing kernel method name. When provided, the generated class is named
    /// <c>{kernelName}_CompiledKernel</c> to match CallSiteRewriter expectations.
    /// When null, falls back to the internal entry point name.
    /// </param>
    /// <param name="launchParamTypeNames">
    /// User-facing C# type names for the Launch method parameters, extracted
    /// from the Roslyn KernelDescriptor. When provided, these override the
    /// IR-derived type names so the Launch signature matches the call site.
    /// </param>
    /// <param name="launchParamFieldAccessors">
    /// Per-parameter struct field/property accessor names (one inner array
    /// per parameter, <see langword="null"/> for non-struct params). Used by
    /// the launch-side marshaling code to access user struct fields by their
    /// original names instead of the synthetic <c>Field0</c>/<c>Field1</c>
    /// fallback.
    /// </param>
    /// <param name="indexDimOverride">
    /// When set, overrides the heuristic index-dimension detection. The
    /// Roslyn pipeline passes the value derived from
    /// <c>LaunchVariant</c> so the generated Launch signature matches
    /// the stub and the rewritten call site.
    /// </param>
    public CompiledKernelGenerator(
        CompiledKernelEmitter compiledKernelEmitter,
        LauncherEmitter launcherEmitter,
        Module module,
        CodeGenerationResult compiled,
        Guid kernelGuid,
        byte[]? compiledBinary = null,
        string? kernelClassName = null,
        int simdWidth = 8,
        string? kernelName = null,
        string[]? launchParamTypeNames = null,
        string[]?[]? launchParamFieldAccessors = null,
        int? indexDimOverride = null)
    {
        _compiledKernelEmitter = compiledKernelEmitter;
        _launcherEmitter = launcherEmitter;
        _module = module;
        _compiled = compiled;
        _kernelGuid = kernelGuid;
        _compiledBinary = compiledBinary;
        _kernelClassName = kernelClassName ?? compiled.KernelClassName;
        _simdWidth = simdWidth;
        _kernelName = kernelName;
        _launchParamTypeNames = launchParamTypeNames;
        _launchParamFieldAccessors = launchParamFieldAccessors;
        _indexDimOverride = indexDimOverride;
        _builder = new StringBuilder(16 * 1024);
        _writer = new StringWriter(_builder);

        // Compute effective embed mode: Binary requires actual binary data
        _effectiveEmbedMode = _compiledKernelEmitter.EmbedMode switch
        {
            KernelEmbedMode.Binary when compiledBinary is not null =>
                KernelEmbedMode.Binary,
            KernelEmbedMode.Binary when compiledBinary is null =>
                KernelEmbedMode.Source, // fallback
            var mode => mode,
        };
    }

    /// <summary>
    /// Generates the complete compiled kernel class.
    /// </summary>
    public CompiledKernelGenerationResult Generate()
    {
        var entryPoint = _module.EntryPoint;
        if (entryPoint is null)
            throw new InvalidOperationException("Module has no entry point.");

        var className = _kernelName is not null
            ? $"{_kernelName}_CompiledKernel"
            : $"{_compiled.EntryPointName}_CompiledKernel";

        // Analyze parameters (skip index param for auto-sized launches)
        var parameters = AnalyzeParameters(entryPoint);

        // Emit file header
        EmitHeader();

        // Emit class declaration
        WriteLine($"internal sealed class {className} : " +
            $"{_compiledKernelEmitter.BaseClassName}, ICompiledKernelKind");
        OpenScope();

        // 1. GeneralAcceleratorType (ICompiledKernelKind)
        var accelType = _compiledKernelEmitter.AcceleratorType;
        WriteLine($"public static AcceleratorType GeneralAcceleratorType => " +
            $"AcceleratorType.{accelType};");
        WriteLine();

        // 2. Kernel GUID
        WriteLine($"public static readonly Guid KernelId = " +
            $"new Guid(\"{_kernelGuid}\");");
        WriteLine();

        // 2b. Runtime kernel cache — set once during accelerator init,
        // accessed at zero cost per launch
        WriteLine("private static Kernel? _runtimeKernel;");
        WriteLine(
            "protected override void OnKernelLoaded(Kernel kernel) " +
            "=> _runtimeKernel = kernel;");
        WriteLine();

        // 3. Emit fallback warning if binary was requested but not available
        if (_compiledKernelEmitter.EmbedMode == KernelEmbedMode.Binary
            && _effectiveEmbedMode == KernelEmbedMode.Source)
        {
            WriteLine("#warning Kernel source embedded as fallback — " +
                "use --compile to embed pre-compiled binary for better " +
                "load performance.");
            WriteLine();
        }

        // 4. Embed kernel data and constructor based on effective mode
        switch (_effectiveEmbedMode)
        {
            case KernelEmbedMode.Binary:
                EmitKernelBinary();
                EmitConstructor(className);
                WriteLine(
                    "public override ReadOnlyMemory<byte> " +
                    "GetCompiledBinary() => KernelBinary;");
                WriteLine(
                    "public override ReadOnlyMemory<byte> " +
                    "GetCompiledBinaryOrDefault() => KernelBinary;");
                WriteLine();
                break;

            case KernelEmbedMode.Source:
                EmitKernelSource();
                EmitConstructor(className);
                WriteLine(
                    "public override string " +
                    "GetSourceAsString() => KernelSource;");
                WriteLine();
                break;

            case KernelEmbedMode.InlineCode:
                EmitKernelCodeInline();
                EmitConstructor(className);
                // No GetSourceAsString override — the base class
                // (e.g., CPUCompiledKernel) already seals it.
                WriteLine();
                break;
        }

        // 5. RequiredCapabilities override
        _compiledKernelEmitter.EmitRequiredCapabilitiesProperty(CreateEmissionContext());
        WriteLine();

        // 6. Marshaled struct types for structs with views
        EmitMarshaledStructTypes(parameters);

        // 7. KernelArgs struct if needed
        if (_launcherEmitter.NeedsKernelArgsStruct)
            EmitKernelArgsStruct(parameters);

        // 8. Launch method
        EmitLaunchMethod(className, parameters);

        CloseScope(); // class

        return new CompiledKernelGenerationResult(
            _writer.ToString(),
            className,
            _compiledKernelEmitter.AcceleratorType);
    }

    #region Kernel Source Embedding

    /// <summary>
    /// Emits the pre-compiled kernel binary as a base64-encoded byte array.
    /// </summary>
    private void EmitKernelBinary()
    {
        var base64 = Convert.ToBase64String(_compiledBinary!);
        WriteLine(
            "private static readonly byte[] KernelBinary = " +
            "Convert.FromBase64String(");
        _indent++;
        WriteLine($"@\"{base64}\");");
        _indent--;
        WriteLine();
    }

    /// <summary>
    /// Emits the kernel source code as a verbatim string constant.
    /// </summary>
    private void EmitKernelSource()
    {
        // Escape for verbatim string: only " → ""
        var escaped = _compiled.SourceCode.Replace(
            "\"",
            "\"\"",
            StringComparison.InvariantCultureIgnoreCase);
        WriteLine("private const string KernelSource = @\"");
        // Write raw source lines (already indented in the verbatim string)
        _writer.Write(escaped);
        _writer.WriteLine("\";");
        WriteLine();
    }

    /// <summary>
    /// Emits the kernel source code directly as inline code in the class body.
    /// The source code already contains a static class wrapper.
    /// </summary>
    private void EmitKernelCodeInline()
    {
        // Write each line of the kernel source with proper indentation.
        // Skip 'using' directives and standalone comment headers — these
        // belong at the file level and are already emitted by EmitHeader.
        using var reader = new StringReader(_compiled.SourceCode);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("using ", StringComparison.Ordinal)
                || trimmed.StartsWith("// ---", StringComparison.Ordinal)
                || trimmed.StartsWith("// Generated by", StringComparison.Ordinal))
                continue;
            // Skip blank lines that follow stripped directives
            if (trimmed.Length == 0)
                continue;
            WriteLine(line);
        }
        WriteLine();
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Emits the constructor calling base with kernel metadata and platform-specific
    /// arguments.
    /// </summary>
    private void EmitConstructor(string className)
    {
        // Constructor calling base with explicit metadata
        WriteLine($"public {className}()");
        _indent++;
        WriteLine(": base(");
        _indent++;
        WriteLine($"KernelId,");
        WriteLine($"\"{_compiled.EntryPointName}\",");
        WriteLine($"CompiledKernelType.Auto,");
        WriteLine($"CompiledKernelSharedMemoryMode.Static,");
        _compiledKernelEmitter.EmitPlatformConstructorArgs(
            CreateEmissionContext());
        _indent--;
        _indent--;
        OpenScope();
        CloseScope();
        WriteLine();
    }

    #endregion

    #region Parameter Analysis

    /// <summary>
    /// Analyzes the entry point's parameters, classifying each for marshaling.
    /// </summary>
    private List<ParameterInfo> AnalyzeParameters(Method entryPoint)
    {
        var result = new List<ParameterInfo>();

        // For auto-sized launches, skip parameter 0 (the implicit thread
        // index). For grouped launches (_indexDimOverride == 0), there is
        // no index parameter — all IR parameters are real kernel params.
        int startParam = _indexDimOverride == 0 ? 0 : 1;
        for (int i = startParam; i < entryPoint.NumParameters; i++)
        {
            var param = entryPoint.Parameters[i];

            var kind = AnalyzeParameter(param.Type);
            var csharpType = GetCSharpTypeName(param.Type, kind);

            // Entry point type names come from the post-backend-transform
            // module (CPU backend only).
            var paramIdx = i - startParam;
            var entryPointType = _compiled.EntryPointParamTypeNames is not null
                && paramIdx < _compiled.EntryPointParamTypeNames.Length
                ? _compiled.EntryPointParamTypeNames[paramIdx]
                : null;

            // User field accessors for struct marshaling.
            var fieldAccessors = _launchParamFieldAccessors is not null
                && paramIdx < _launchParamFieldAccessors.Length
                ? _launchParamFieldAccessors[paramIdx]
                : null;

            // User-facing C# type name from the Roslyn frontend (when
            // available); falls back to IR-derived. Resolved here so the
            // KernelArgs struct (emitted before EmitLaunchMethod) can use
            // it for plain-struct parameters whose synthetic
            // <c>Struct_{id}</c> name is never declared.
            var launchType = _launchParamTypeNames is not null
                && paramIdx < _launchParamTypeNames.Length
                ? _launchParamTypeNames[paramIdx]
                : csharpType;

            // Marshaled type lives in the KernelArgs struct. For plain
            // structs we use the user-facing type directly so the field
            // type matches the kernel argument type that the user passes.
            var marshaledType = kind == ParameterKind.StructPlain
                ? launchType
                : GetMarshaledTypeName(param.Type, kind);

            result.Add(new ParameterInfo
            {
                Index = i,
                Name = $"param_{param.Name}",
                Type = param.Type,
                Kind = kind,
                CSharpTypeName = csharpType,
                MarshaledTypeName = marshaledType,
                EntryPointTypeName = entryPointType,
                UserFieldAccessors = fieldAccessors,
                LaunchTypeName = launchType,
            });
        }

        return result;
    }

    /// <summary>
    /// Classifies an IR type into its marshaling kind.
    /// </summary>
    private static ParameterKind AnalyzeParameter(TypeValue type)
    {
        return type switch
        {
            ViewType => ParameterKind.View,
            PointerType => ParameterKind.Pointer,
            StructureType st when st.HasFlags(TypeFlags.ViewDependent) =>
                ParameterKind.StructWithViews,
            StructureType => ParameterKind.StructPlain,
            _ => ParameterKind.Primitive,
        };
    }

    #endregion

    #region Type Name Resolution

    /// <summary>
    /// Returns the C# type name for a parameter in the Launch method signature.
    /// </summary>
    private static string GetCSharpTypeName(TypeValue type, ParameterKind kind)
    {
        return kind switch
        {
            ParameterKind.View => $"ArrayView<{GetElementTypeName((ViewType)type)}>",
            ParameterKind.Pointer => "IntPtr",
            ParameterKind.StructWithViews => GetStructUserTypeName((StructureType)type),
            ParameterKind.StructPlain => GetStructUserTypeName((StructureType)type),
            _ => GetPrimitiveTypeName(type),
        };
    }

    /// <summary>
    /// Returns the marshaled type name for a parameter in the KernelArgs struct.
    /// </summary>
    private string GetMarshaledTypeName(TypeValue type, ParameterKind kind)
    {
        return kind switch
        {
            ParameterKind.View =>
                $"ViewImplementation<{GetElementTypeName((ViewType)type)}>",
            ParameterKind.Pointer => "IntPtr",
            ParameterKind.StructWithViews =>
                GetMarshaledStructName((StructureType)type),
            ParameterKind.StructPlain =>
                GetStructUserTypeName((StructureType)type),
            _ => GetPrimitiveTypeName(type),
        };
    }

    /// <summary>
    /// Returns the C# primitive type keyword for an IR type.
    /// </summary>
    private static string GetPrimitiveTypeName(TypeValue type)
    {
        return type.BasicValueType switch
        {
            BasicValueType.Int1 => "bool",
            BasicValueType.Int8 => "byte",
            BasicValueType.Int16 => "short",
            BasicValueType.Int32 => "int",
            BasicValueType.Int64 => "long",
            BasicValueType.Float16 => "Half",
            BasicValueType.Float32 => "float",
            BasicValueType.Float64 => "double",
            _ => "int",
        };
    }

    /// <summary>
    /// Returns the C# element type name for a view type.
    /// </summary>
    private static string GetElementTypeName(ViewType viewType) =>
        viewType.ElementType is StructureType st
            ? $"struct_{st.Id}"
            : GetPrimitiveTypeName(viewType.ElementType);

    /// <summary>
    /// Returns the generated user-facing struct name for a structure type.
    /// </summary>
    private static string GetStructUserTypeName(StructureType st) =>
        $"Struct_{st.Id}";

    /// <summary>
    /// Returns the generated marshaled struct name for a structure type with views.
    /// </summary>
    private string GetMarshaledStructName(StructureType st) =>
        $"Marshaled_{st.Id}";

    #endregion

    #region Marshaled Struct Emission

    /// <summary>
    /// Emits marshaled struct types for all parameters that contain views.
    /// </summary>
    private void EmitMarshaledStructTypes(List<ParameterInfo> parameters)
    {
        var emitted = new HashSet<IR.ValueId>();

        foreach (var param in parameters)
        {
            if (param.Kind != ParameterKind.StructWithViews)
                continue;

            var st = (StructureType)param.Type;
            EmitMarshaledStructType(st, emitted);
        }
    }

    /// <summary>
    /// Emits a single marshaled struct declaration, recursing into nested view-dependent
    /// structs.
    /// </summary>
    private void EmitMarshaledStructType(StructureType st, HashSet<IR.ValueId> emitted)
    {
        if (!emitted.Add(st.Id))
            return;

        for (int i = 0; i < st.NumFields; i++)
        {
            var fieldType = st.Fields[i];
            if (fieldType is StructureType nested &&
                nested.HasFlags(TypeFlags.ViewDependent))
            {
                EmitMarshaledStructType(nested, emitted);
            }
        }

        WriteLine("[StructLayout(LayoutKind.Sequential)]");
        WriteLine($"private unsafe struct {GetMarshaledStructName(st)}");
        OpenScope();

        for (int i = 0; i < st.NumFields; i++)
        {
            var fieldType = st.Fields[i];
            var fieldName = StructureType.GetFieldName(i);
            var fieldTypeName = GetMarshaledFieldTypeName(fieldType);

            WriteLine($"public {fieldTypeName} {fieldName};");
        }

        CloseScope();
        WriteLine();
    }

    /// <summary>
    /// Returns the marshaled type name for a struct field, converting views to
    /// ViewImplementation.
    /// </summary>
    private string GetMarshaledFieldTypeName(TypeValue type)
    {
        return type switch
        {
            // For struct-element views, use the user-facing element type
            // in ViewImplementation (same memory layout). The IR struct
            // type name would reference post-backend IDs not declared here.
            ViewType vt when vt.ElementType is StructureType =>
                $"ViewImplementation<byte>",
            ViewType vt =>
                $"ViewImplementation<{GetElementTypeName(vt)}>",
            StructureType st when st.HasFlags(TypeFlags.ViewDependent) =>
                GetMarshaledStructName(st),
            StructureType st => GetStructUserTypeName(st),
            PointerType => "IntPtr",
            _ => GetPrimitiveTypeName(type),
        };
    }

    #endregion

    #region KernelArgs Struct

    /// <summary>
    /// Emits the flat KernelArgs struct used by FlatStructLauncherEmitter backends.
    /// </summary>
    private void EmitKernelArgsStruct(List<ParameterInfo> parameters)
    {
        WriteLine("[StructLayout(LayoutKind.Sequential)]");
        WriteLine("private unsafe struct KernelArgs");
        OpenScope();

        foreach (var param in parameters)
        {
            WriteLine($"public {param.MarshaledTypeName} {param.Name};");
        }

        CloseScope();
        WriteLine();
    }

    #endregion

    #region Launch Method

    /// <summary>
    /// Emits the static Launch method with stream, config, and kernel parameters.
    /// </summary>
    private void EmitLaunchMethod(
        string className,
        List<ParameterInfo> parameters)
    {
        // Detect multi-dim index from entry point's first parameter type.
        // StructureType with 2 fields = Index2D, 3 fields = Index3D.
        // When the Roslyn pipeline provides an override (derived from
        // LaunchVariant), use it — this prevents grouped kernels whose
        // first user parameter happens to be a struct from being
        // misidentified as multi-dim index kernels.
        // Fallback heuristic: only treat as multi-dim index if the struct
        // has no pointer/view dependencies (index types are pure numeric
        // structs; ArrayView and other user structs carry pointer/view
        // flags).
        int indexDims = _indexDimOverride
            ?? (_module.EntryPoint?.Parameters[0].Type is
                StructureType st
                && !st.HasFlags(TypeFlags.PointerDependent
                    | TypeFlags.ViewDependent)
                    ? st.NumFields : 1);

        var sigParams = new List<string>
        {
            "AcceleratorStream stream",
            "KernelConfig config",
            "long userExtent"
        };

        // Add dimension parameters for multi-dim index reconstruction
        if (indexDims >= 2)
        {
            sigParams.Add("int _dimX");
            sigParams.Add("int _dimY");
        }
        if (indexDims >= 3)
            sigParams.Add("int _dimZ");

        // Use Roslyn-extracted type names when available (matches call site).
        // Fall back to IR-derived names for non-Roslyn-frontend paths.
        // Also store the resolved type name on each ParameterInfo so the
        // launcher emitter can use it for sizeof/ViewImplementation.
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            var typeName = _launchParamTypeNames is not null
                && i < _launchParamTypeNames.Length
                ? _launchParamTypeNames[i]
                : param.CSharpTypeName;
            param.LaunchTypeName = typeName;
            sigParams.Add($"{typeName} {param.Name}");
        }

        var paramList = string.Join(",\n        ", sigParams);

        WriteLine("[MethodImpl(MethodImplOptions.AggressiveOptimization)]");
        WriteLine("internal static unsafe void Launch(");
        _indent++;
        WriteLine($"{paramList})");
        _indent--;
        OpenScope();

        var ctx = CreateEmissionContext();
        ctx.IndexDimensions = indexDims;
        _launcherEmitter.EmitLaunchBody(ctx, parameters);

        CloseScope();
        WriteLine();
    }

    #endregion

    #region Marshaling Helpers

    /// <summary>
    /// Emits the marshaling block that converts all parameters into the KernelArgs
    /// structure.
    /// </summary>
    private void EmitArgsMarshalingBlock(List<ParameterInfo> parameters)
    {
        WriteLine("var args = new KernelArgs();");

        foreach (var param in parameters)
        {
            switch (param.Kind)
            {
                case ParameterKind.View:
                    var argElemType = ((ViewType)param.Type).ElementType
                        is StructureType ? "byte"
                        : GetElementTypeName((ViewType)param.Type);
                    WriteLine(
                        $"args.{param.Name} = " +
                        $"new ViewImplementation<{argElemType}>({param.Name});");
                    break;

                case ParameterKind.StructWithViews:
                    EmitStructMarshalingCode(
                        (StructureType)param.Type,
                        param.Name,
                        $"args.{param.Name}",
                        param.UserFieldAccessors);
                    break;

                case ParameterKind.StructPlain:
                    // KernelArgs field type IS the user type (set in
                    // AnalyzeParameters), so direct assignment works.
                    WriteLine($"args.{param.Name} = {param.Name};");
                    break;

                default:
                    // Primitive: cast to the marshaled (IR) type to bridge
                    // signedness mismatches like sbyte → byte (IR maps both
                    // Int8 / UInt8 to "byte"). The cast is identity for
                    // matched types.
                    WriteLine(
                        $"args.{param.Name} = " +
                        $"({param.MarshaledTypeName}){param.Name};");
                    break;
            }
        }

        WriteLine();
    }

    /// <summary>
    /// Recursively emits field-by-field marshaling code for a struct with
    /// views. The destination uses IR-internal field names
    /// (<c>Field0</c>/<c>Field1</c>/...) because it writes into the synthesized
    /// <c>Marshaled_xxx</c> struct. The source uses user-side accessor names
    /// (e.g. <c>BaseView</c>, <c>Extent</c>, <c>Stride</c>) when
    /// <paramref name="userFieldAccessors"/> is provided so that the wrapper
    /// can compile against user-facing types like
    /// <c>ArrayView1D&lt;T, Stride1D.Dense&gt;</c>.
    /// </summary>
    private void EmitStructMarshalingCode(
        StructureType st,
        string sourcePrefix,
        string destPrefix,
        string[]? userFieldAccessors = null)
    {
        for (int i = 0; i < st.NumFields; i++)
        {
            var fieldType = st.Fields[i];
            var irFieldName = StructureType.GetFieldName(i);
            // Source side uses user-facing accessor; dest stays IR-internal.
            var srcAccessor = userFieldAccessors is not null
                && i < userFieldAccessors.Length
                ? userFieldAccessors[i]
                : irFieldName;
            var src = $"{sourcePrefix}.{srcAccessor}";
            var dst = $"{destPrefix}.{irFieldName}";

            switch (fieldType)
            {
                case ViewType vt:
                    // Always use the raw (ptr, length) constructor: it
                    // tolerates element-type mismatches between the user
                    // view (e.g. ArrayView<uint>) and the marshaled type
                    // (ViewImplementation<int> — IR maps both Int32 and
                    // UInt32 to "int"), and works for struct-element
                    // views (ViewImplementation<byte>) the same way.
                    var marshalElemType = vt.ElementType is StructureType
                        ? "byte"
                        : GetElementTypeName(vt);
                    WriteLine(
                        $"{dst} = {src}.IsValid" +
                        $" ? new ViewImplementation<{marshalElemType}>(" +
                        $"Unsafe.AsPointer(ref {src}.LoadEffectiveAddress()), " +
                        $"{src}.Length)" +
                        $" : default(ViewImplementation<{marshalElemType}>);");
                    break;

                case StructureType nested when
                    nested.HasFlags(TypeFlags.ViewDependent):
                    // Nested view-dependent structs: we don't have
                    // user-side accessors at this level, so fall back to
                    // IR field names — works only when the user nested
                    // type also exposes Field0/1/... (rare).
                    EmitStructMarshalingCode(nested, src, dst);
                    break;

                default:
                    // CPU's heuristic: skip small primitive fields (≤2 bytes)
                    // inside StructWithViews because they typically come from
                    // decomposed zero-size structs (e.g. Stride1D.Dense → byte
                    // padding) that the user-side type does not expose as a
                    // matching primitive accessor.
                    if (fieldType is PrimitiveType pt && pt.Size <= 2)
                        continue;
                    // Use Unsafe.WriteUnaligned to bit-cast the user value
                    // (e.g. LongIndex1D, which has the same memory layout as
                    // long but no implicit conversion) into the IR-typed
                    // marshaled struct field. The user type and IR type
                    // must have compatible sizes; LongIndex1D ↔ long is the
                    // common case for ArrayView*D.Extent.
                    var irTypeName = GetPrimitiveTypeName(fieldType);
                    WriteLine(
                        $"Unsafe.WriteUnaligned(" +
                        $"ref Unsafe.As<{irTypeName}, byte>(ref {dst}), " +
                        $"{src});");
                    break;
            }
        }
    }

    #endregion

    #region Emission Context

    /// <summary>
    /// Creates a <see cref="LauncherEmissionContext"/> bridging this generator's helpers
    /// to platform-specific emitters.
    /// </summary>
    private LauncherEmissionContext CreateEmissionContext() =>
        new()
        {
            WriteLine = WriteLine,
            OpenScope = OpenScope,
            CloseScope = CloseScope,
            IncrementIndent = () => _indent++,
            DecrementIndent = () => _indent--,
            EmitArgsMarshalingBlock = EmitArgsMarshalingBlock,
            EmitStructMarshalingCode = EmitStructMarshalingCode,
            GetElementTypeName = GetElementTypeName,
            GetPrimitiveTypeName = GetPrimitiveTypeName,
            GetMarshaledStructName = GetMarshaledStructName,
            KernelClassName = _kernelClassName,
            SimdWidth = _simdWidth,
            BufferPool = _compiled.BufferPool,
            ActiveEmbedMode = _effectiveEmbedMode,
        };

    #endregion

    #region Header

    /// <summary>
    /// Emits the file header with standard and backend-specific using directives.
    /// </summary>
    private void EmitHeader()
    {
        WriteLine("// ---------------------------------------------------");
        WriteLine("// Generated by ILGPU Compiler — Compiled Kernel");
        WriteLine("// ---------------------------------------------------");
        WriteLine();
        // CS0164 unreferenced label, CS0649 field never assigned, and
        // CS1717 self-assignment are artifacts of the IR → C# lowering and
        // are benign in generated code — disable them so downstream
        // consumers (samples, user projects) don't get noise.
        WriteLine("#pragma warning disable CS0164, CS0649, CS1717");
        WriteLine();
        WriteLine("using System;");
        WriteLine("using System.Runtime.CompilerServices;");
        WriteLine("using System.Runtime.InteropServices;");
        WriteLine("using ILGPU;");
        WriteLine("using ILGPU.Runtime;");

        // Merge usings from both emitters (deduplicate)
        var allUsings = new HashSet<string>(
            _compiledKernelEmitter.RequiredUsings);
        foreach (var u in _launcherEmitter.RequiredUsings)
            allUsings.Add(u);

        foreach (var u in allUsings)
            WriteLine(u);

        WriteLine();
    }

    #endregion

    #region Text Output Helpers

    /// <summary>
    /// Writes an indented line of source code to the output.
    /// </summary>
    private void WriteLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            for (int i = 0; i < _indent; i++)
                _writer.Write("    ");
            _writer.WriteLine(text);
        }
        else
        {
            _writer.WriteLine();
        }
    }

    /// <summary>
    /// Opens a new brace scope and increments indentation.
    /// </summary>
    private void OpenScope()
    {
        WriteLine("{");
        _indent++;
    }

    /// <summary>
    /// Closes the current brace scope and decrements indentation.
    /// </summary>
    private void CloseScope()
    {
        _indent--;
        WriteLine("}");
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _writer.Dispose();

        base.Dispose(disposing);
    }


    #endregion
}
