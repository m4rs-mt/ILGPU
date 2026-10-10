// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Backend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPUC.Compilers;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Roslyn.Analysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Backends;

// These types will be consumed by external test frameworks
#pragma warning disable CA1515 // Consider making public types internal

/// <summary>
/// Represents the general type of a backend.
/// </summary>
public enum BackendType
{
    /// <summary>
    /// A metal backend.
    /// </summary>
    Metal,

    /// <summary>
    /// An OpenCL backend.
    /// </summary>
    OpenCL,

    /// <summary>
    /// A CUDA backend.
    /// </summary>
    Cuda,

    /// <summary>
    /// A ROCm backend.
    /// </summary>
    ROCm,

    /// <summary>
    /// A CPU backend for vectorized CPU execution.
    /// </summary>
    CPU,
}

/// <summary>
/// Represents a general ILGPU backend.
/// </summary>
/// <remarks>
/// Constructs a new generic backend.
/// </remarks>
/// <param name="backendType">The backend type.</param>
/// <param name="acceleratorType">The accelerator type.</param>
/// <param name="capabilities">The supported capabilities.</param>
abstract class Backend(
    BackendType backendType,
    AcceleratorType acceleratorType,
    AcceleratorCapabilities capabilities)
{
    /// <summary>
    /// Returns the associated backend type.
    /// </summary>
    public BackendType BackendType { get; } = backendType;

    /// <summary>
    /// Returns the associated accelerator type.
    /// </summary>
    public AcceleratorType AcceleratorType { get; } = acceleratorType;

    /// <summary>
    /// Returns the current warp size (if available).
    /// </summary>
    public virtual int? CurrentWarpSize { get; }

    /// <summary>
    /// Returns the supported capabilities.
    /// </summary>
    public AcceleratorCapabilities Capabilities { get; } = capabilities;

    /// <summary>
    /// Returns the underlying language configuration.
    /// </summary>
    public abstract LanguageConfiguration LanguageConfiguration { get; }

    /// <summary>
    /// Creates a platform-specific launcher emitter for this backend.
    /// </summary>
    public abstract LauncherEmitter CreateLauncherEmitter();

    /// <summary>
    /// Creates a platform-specific compiled kernel emitter for this backend.
    /// </summary>
    public abstract CompiledKernelEmitter CreateCompiledKernelEmitter();

    /// <summary>
    /// The maximum number of threads in a group of a GPU accelerator.
    /// </summary>
    protected const int MaxNumGPUThreadsPerGroup = 1024;

    /// <summary>
    /// Creates a new architecture specification used to specialize a module for this
    /// backend.
    /// </summary>
    protected abstract ArchitectureSpecification GetArchitectureSpecification();

    /// <summary>
    /// Creates a backend transformer to transform a given module into the right IR shape.
    /// </summary>
    /// <param name="properties">The compilation properties to use.</param>
    /// <returns>The transformer to be used.</returns>
    public Transformer CreateBackendTransformer(CompilationProperties properties)
    {
        var builder = Transformer.CreateBuilder();

        // Specialize for this accelerator
        builder.AddAcceleratorSpecializer(GetArchitectureSpecification());

        // Add general optimizations
        builder.AddBasicOptimizations();

        return builder.ToTransformer();
    }

    /// <summary>
    /// Generates code for the given module.
    /// </summary>
    /// <param name="properties">Compilation properties to use.</param>
    /// <param name="typeInformationManager">Shared type manager to use.</param>
    /// <param name="module">The module to generate code for.</param>
    /// <param name="launch">
    /// How the entry point is launched; derived from the entry point's first
    /// parameter when <see langword="null"/>.
    /// </param>
    /// <returns>The generated code.</returns>
    public CodeGenerationResult GenerateCode(
        CompilationProperties properties,
        TypeInformationManager typeInformationManager,
        Module module,
        EntryPointLaunch? launch = null) =>
        GenerateCode(
            properties,
            typeInformationManager,
            module,
            dumpWriter: null,
            launch: launch);

    /// <summary>
    /// Generates code for the given module, optionally dumping the post-backend-
    /// transform IR to <paramref name="dumpWriter"/>.
    /// </summary>
    /// <param name="properties">Compilation properties to use.</param>
    /// <param name="typeInformationManager">Shared type manager to use.</param>
    /// <param name="module">The module to generate code for.</param>
    /// <param name="dumpWriter">
    /// Optional writer that receives a normalized IR dump after backend-specific
    /// transforms are applied. Pass <see langword="null"/> to skip the dump.
    /// </param>
    /// <param name="dumpFormat">
    /// The IR printer format to use when writing the dump.
    /// Defaults to <see cref="IRPrinterFormat.ILGPU"/>.
    /// </param>
    /// <param name="launch">
    /// How the entry point is launched; derived from the entry point's first
    /// parameter when <see langword="null"/>.
    /// </param>
    /// <returns>The generated code.</returns>
    public CodeGenerationResult GenerateCode(
        CompilationProperties properties,
        TypeInformationManager typeInformationManager,
        Module module,
        TextWriter? dumpWriter,
        IRPrinterFormat dumpFormat = IRPrinterFormat.ILGPU,
        EntryPointLaunch? launch = null)
    {
        var transformer = CreateBackendTransformer(properties);
        var finalModule = transformer.Apply(properties, typeInformationManager, module);

        if (dumpWriter is not null)
        {
            finalModule.Dump(
                dumpWriter,
                IRDumpMode.Normalized,
                format: dumpFormat,
                point: IRDumpPoint.AfterBackendTransforms,
                annotation: $"backend: {BackendType}  " +
                $"opt: {properties.OptimizationLevel}");
        }

        if (!LanguageConfiguration.SupportsFeature(LanguageFeature.GenericAddressSpace))
            VerifyNoGenericAddressSpaces(finalModule);
        if (!LanguageConfiguration.SupportsFeature(LanguageFeature.Recursion))
            VerifyNoRecursion(finalModule);

        return GenerateCode(finalModule, EntryPointLaunch.Resolve(module, launch));
    }

    /// <summary>
    /// Rejects recursive calls, which this backend cannot run. The methods of a
    /// call cycle are never inlined, so the cycle remains unless optimization
    /// removes the calls that close it.
    /// </summary>
    /// <param name="module">The final module after all transformations.</param>
    private void VerifyNoRecursion(Module module)
    {
        var cycles = CallCycle.FindAll(module.Methods.ToArray());
        if (cycles.Count < 1)
            return;

        var cycle = cycles[0];
        throw new KernelCompilationException(
            KernelDiagnostic.UnsupportedRecursionId,
            string.Format(
                ErrorMessages.KernelDiagnosticUnsupportedRecursion,
                cycle.FormatMethodNames(),
                BackendType),
            cycle.Calls[0].Location);
    }

    /// <summary>
    /// Rejects generic pointers and references into shared or local memory,
    /// which this backend cannot express: they remain generic when they can
    /// point to several address spaces, are returned by a helper method or are
    /// stored to memory. Generic pointers into global memory or with an unknown
    /// address space (for example, pointers loaded from memory) are emitted in
    /// the backend's default (global) address space and are not rejected.
    /// </summary>
    /// <remarks>
    /// Comparisons are verified after all other values. Pointers of unknown
    /// origin, such as pointers loaded from memory, are treated as pointers
    /// to global memory, so a pointer into shared or local memory that was
    /// stored to memory and loaded back is reported as a pointer to global
    /// memory.
    /// </remarks>
    /// <param name="module">The final module after all transformations.</param>
    private void VerifyNoGenericAddressSpaces(Module module)
    {
        var results = PointerAddressSpaces.AnalyzeModule(
            module.EntryPoint,
            PointerAddressSpaces.AnalysisFlags.IgnoreGenericAddressSpace);
        var visited = new HashSet<Value>();
        var worklist = new Stack<Value>();
        Dictionary<Method, List<MethodCall>>? calls = null;
        var comparisons = new List<(Method Method, CompareValue Compare)>();
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;

            foreach (var param in method.Parameters)
                VerifyNotGeneric(module, results, method, param);

            // Pure values and returned values are not part of the value chain
            // of any block: reach them as operands
            foreach (var block in method.Blocks)
            {
                foreach (var value in block)
                    worklist.Push(value);
                if (block.TerminationValue is { } terminationValue)
                    worklist.Push(terminationValue);
            }
            while (worklist.TryPop(out var value))
            {
                if (value is Method or TypeValue or BasicBlock || !visited.Add(value))
                    continue;
                VerifyNotGeneric(module, results, method, value);
                if (value is Store store)
                    VerifyNotStoredGeneric(module, results, method, store);
                if (value is IntAsPointerCast cast &&
                    IsUsedAsAddress(module, cast, ref calls))
                {
                    VerifyIntegerPointer(
                        module,
                        results,
                        method,
                        cast.Source,
                        cast.Location,
                        ref calls);
                }
                if (value is LoadElementAddress address &&
                    !IsKnownAddress(results, address.Source) &&
                    IsUsedAsAddress(module, address, ref calls))
                {
                    VerifyIntegerPointer(
                        module,
                        results,
                        method,
                        address.Offset,
                        address.Location,
                        ref calls);
                }
                if (value is CompareValue compare)
                    comparisons.Add((method, compare));
                foreach (var operand in value.Values)
                    worklist.Push(operand);
            }
        }
        foreach (var (method, compare) in comparisons)
            VerifyPointerComparison(results, method, compare);
    }

    /// <summary>
    /// Throws if the given generic pointer or view can point to shared or local
    /// memory in a way the target cannot express: it can point to several
    /// address spaces, or it crosses a method boundary as a parameter or call
    /// result. A generic value that provably points into a single address space
    /// inside a method is emitted in that address space.
    /// </summary>
    private void VerifyNotGeneric(
        Module module,
        AddressSpaceResults results,
        Method method,
        Value value)
    {
        if (value.Type is not AddressSpaceType
            {
                AddressSpace: MemoryAddressSpace.Generic
            })
        {
            return;
        }

        const AddressSpaceFlags Spaces = AddressSpaceFlags.Global |
            AddressSpaceFlags.Shared | AddressSpaceFlags.Local;
        var flags = results[value].Flags & Spaces;
        if ((flags & (AddressSpaceFlags.Shared | AddressSpaceFlags.Local)) ==
            AddressSpaceFlags.None)
        {
            return;
        }

        bool severalSpaces = (flags & (flags - 1)) != AddressSpaceFlags.None;
        bool crossesMethod = value is Parameter || value is MethodCall ||
            IsPassedAcrossMethods(value);
        if (!severalSpaces && !crossesMethod)
            return;

        var reason = severalSpaces
            ? $"can point to {FormatSpaces(flags)} memory"
            : $"points to {FormatSpaces(flags)} memory but is passed to or " +
                "returned from a helper method as a generic pointer";
        throw GetGenericPointerException(module, method, value.Location, reason);
    }

    /// <summary>
    /// Throws if the given store writes a generic pointer or view into shared or
    /// local memory to memory, directly or as a structure field: a pointer loaded
    /// from memory points into the backend's default (global) address space.
    /// </summary>
    private void VerifyNotStoredGeneric(
        Module module,
        AddressSpaceResults results,
        Method method,
        Store store)
    {
        static bool IsGeneric(TypeValue type) =>
            type is AddressSpaceType
            {
                AddressSpace: MemoryAddressSpace.Generic
            };

        var value = store.Value;
        var info = results.GetAnalysisValue(value);
        var flags = AddressSpaceFlags.None;
        if (IsGeneric(value.Type))
        {
            flags = info.Data.Flags;
        }
        else if (value.Type is StructureType structureType)
        {
            for (int i = 0; i < structureType.NumFields; ++i)
            {
                if (!IsGeneric(structureType[i]))
                    continue;
                flags |= info.IsStructure && i < info.NumFields
                    ? info[i].Flags
                    : info.Data.Flags;
            }
        }

        flags &= AddressSpaceFlags.Shared | AddressSpaceFlags.Local;
        if (flags == AddressSpaceFlags.None)
            return;

        throw GetGenericPointerException(
            module,
            method,
            store.Location,
            $"points to {FormatSpaces(flags)} memory but is stored to memory " +
            "as a generic pointer");
    }

    /// <summary>
    /// Returns the exception rejecting a generic pointer or reference of the
    /// given method for the given reason.
    /// </summary>
    private NotSupportedException GetGenericPointerException(
        Module module,
        Method method,
        Location location,
        string reason)
    {
        var kernel = module.EntryPoint;
        var where = method == kernel
            ? string.Empty
            : $" in '{GetDisplayName(method)}'";
        var message = location.FormatErrorMessage(
            $"Kernel '{GetDisplayName(kernel)}': a pointer or reference{where} " +
            reason);
        return new NotSupportedException(
            $"{message}. {BackendType} does not support generic pointers; make " +
            "each pointer or reference point into a single address space.");
    }

    /// <summary>
    /// Returns true if the given native integer, an integer converted to a
    /// pointer or a native integer moved by an offset, is used as an address.
    /// Native integers are untyped pointers in the IR, so integers such as the
    /// difference of two addresses are converted to pointers even if they never
    /// become addresses again. Converting them back to integers, comparing them
    /// and storing them are no address uses, also after phis, selects, fields
    /// of structures, helper parameters, helper results and moves by bytes, such
    /// as <c>(int)(b - a)</c>. All other uses are, such as loads and stores to
    /// the address.
    /// </summary>
    private static bool IsUsedAsAddress(
        Module module,
        Value value,
        ref Dictionary<Method, List<MethodCall>>? calls)
    {
        // Fields are the indices of the flattened fields of structure values
        // that hold the native integer, or -1 for other values
        var visited = new HashSet<(Value, int)>();
        var worklist = new Stack<(Value Value, int Field)>();
        worklist.Push((value, -1));
        while (worklist.TryPop(out var entry))
        {
            if (!visited.Add(entry))
                continue;
            var field = entry.Field;
            foreach (Use use in entry.Value.Uses)
            {
                switch (use.Target)
                {
                    case PointerAsIntCast:
                    case CompareValue:
                        // Converted back to an integer or compared
                        break;
                    case Store when use.Index == 1:
                        // Stored integers are not traced: integers loaded from
                        // memory are assumed to hold global addresses
                        break;
                    case Method:
                        // The method declaring a parameter
                        break;
                    case PhiValue:
                    case Predicate when use.Index > 0:
                        // Values: an address only if they are used as one
                        worklist.Push((use.Target, field));
                        break;
                    case PointerCast:
                    case AddressSpaceCast:
                    case LoadElementAddress:
                        // Typed or moved addresses: an address only if they are
                        // used as one
                        worklist.Push((use.Target, -1));
                        break;
                    case StructureValue:
                        // Fields of structures: an address only if they are
                        // loaded and used as one
                        worklist.Push((use.Target, use.Index));
                        break;
                    case SetField setField when use.Index == 1:
                        worklist.Push((
                            setField,
                            setField.FieldSpan.Index + Math.Max(field, 0)));
                        break;
                    case SetField setField:
                        // Other fields keep the native integer
                        if (!Contains(setField.FieldSpan, field))
                            worklist.Push((setField, field));
                        break;
                    case GetField getField:
                        if (Contains(getField.FieldSpan, field))
                        {
                            worklist.Push((
                                getField,
                                getField.FieldSpan.HasSpan
                                    ? field - getField.FieldSpan.Index
                                    : -1));
                        }
                        break;
                    case MethodCall { Target.HasImplementation: true } call
                        when use.Index > 0:
                        // Arguments are the parameters of the called helper
                        worklist.Push((
                            call.Target.Parameters[use.Index - 1],
                            field));
                        break;
                    case BasicBlock
                    {
                        TerminationKind: BlockTerminationKind.Return
                    } block:
                        // Returned values are the results of all calls
                        calls ??= GetCalls(module);
                        if (calls.TryGetValue(block.Scope, out var methodCalls))
                        {
                            foreach (var call in methodCalls)
                                worklist.Push((call, field));
                        }
                        break;
                    default:
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the given field span contains the given flattened field
    /// of a structure value.
    /// </summary>
    private static bool Contains(FieldSpan span, int field) =>
        field >= span.Index && field < span.Index + span.Span;

    /// <summary>
    /// Returns true if the given value is a pointer to known memory only, not a
    /// native integer or a pointer of unknown origin, such as a pointer loaded
    /// from memory. Offsets move such a pointer inside its memory, while native
    /// integers moved by an offset, such as <c>n + (nint)(long)&amp;x</c>, hold
    /// the sum of both integers.
    /// </summary>
    private static bool IsKnownAddress(AddressSpaceResults results, Value value)
    {
        if (value.Type is not AddressSpaceType)
            return false;
        var flags = results[value].Flags;
        return flags != AddressSpaceFlags.None &&
            (flags & AddressSpaceFlags.Generic) == AddressSpaceFlags.None;
    }

    /// <summary>
    /// Throws if the given integer, which becomes an address, holds the address
    /// of shared or local memory. Without generic pointers, integers converted
    /// to pointers refer to global memory, which is what addresses of unknown
    /// origin, such as addresses passed by the host, refer to. The integer is
    /// traced through phis, selects, fields of structures, helper parameters,
    /// helper results, arithmetic and the native integers it was converted
    /// from. Integers loaded from memory are assumed to hold global addresses.
    /// </summary>
    private void VerifyIntegerPointer(
        Module module,
        AddressSpaceResults results,
        Method method,
        Value integer,
        Location location,
        ref Dictionary<Method, List<MethodCall>>? calls)
    {
        const AddressSpaceFlags Spaces =
            AddressSpaceFlags.Shared | AddressSpaceFlags.Local;
        var flags = AddressSpaceFlags.None;
        var visited = new HashSet<(Value, int)>();
        var worklist = new Stack<(Value Value, int Field)>();
        worklist.Push((integer, -1));
        while (worklist.TryPop(out var entry))
        {
            var (value, field) = entry;
            if (!visited.Add(entry))
                continue;
            if (value.Type is AddressSpaceType)
            {
                // Pointers hold the addresses of the memory they point to
                flags |= results[value].Flags & Spaces;
                if (IsKnownAddress(results, value))
                    continue;
            }
            switch (value)
            {
                case PhiValue phi:
                    foreach (var argument in phi.Arguments)
                        worklist.Push((argument, field));
                    break;
                case Predicate predicate:
                    worklist.Push((predicate.TrueValue, field));
                    worklist.Push((predicate.FalseValue, field));
                    break;
                case Parameter parameter when parameter.Method != module.EntryPoint:
                    // Helper parameters hold the arguments of all calls
                    calls ??= GetCalls(module);
                    if (!calls.TryGetValue(parameter.Method, out var methodCalls))
                        break;
                    foreach (var call in methodCalls)
                        worklist.Push((call.Arguments[parameter.Index], field));
                    break;
                case MethodCall { Target.HasImplementation: true } call:
                    foreach (var block in call.Target.Blocks)
                    {
                        if (block.TerminationKind == BlockTerminationKind.Return &&
                            block.TerminationValue is { } returnValue)
                        {
                            worklist.Push((returnValue, field));
                        }
                    }
                    break;
                case StructureValue structure
                    when field >= 0 && field < structure.Count:
                    worklist.Push((structure.Values[field], -1));
                    break;
                case GetField getField:
                    for (int i = 0; i < getField.FieldSpan.Span; ++i)
                    {
                        if (field < 0 || field == i)
                        {
                            worklist.Push((
                                getField.Source,
                                getField.FieldSpan.Index + i));
                        }
                    }
                    break;
                case SetField setField when Contains(setField.FieldSpan, field):
                    worklist.Push((
                        setField.Value,
                        setField.FieldSpan.HasSpan
                            ? field - setField.FieldSpan.Index
                            : -1));
                    break;
                case SetField setField when field >= 0:
                    worklist.Push((setField.Source, field));
                    break;
                case PureValue:
                    foreach (var operand in value.Values)
                        worklist.Push((operand, -1));
                    break;
            }
        }
        if (flags == AddressSpaceFlags.None)
            return;

        throw new KernelCompilationException(
            KernelDiagnostic.IntegerToPointerId,
            string.Format(
                CultureInfo.InvariantCulture,
                ErrorMessages.KernelDiagnosticIntegerToPointer,
                GetDisplayName(method),
                FormatSpaces(flags),
                BackendType),
            location);
    }

    /// <summary>
    /// Returns the calls of each method of the given module.
    /// </summary>
    private static Dictionary<Method, List<MethodCall>> GetCalls(Module module)
    {
        var calls = new Dictionary<Method, List<MethodCall>>();
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            method.ForEachValue<MethodCall>(call =>
            {
                if (!calls.TryGetValue(call.Target, out var targetCalls))
                    calls.Add(call.Target, targetCalls = []);
                targetCalls.Add(call);
            });
        }
        return calls;
    }

    /// <summary>
    /// Throws if the given comparison compares pointers into different address
    /// spaces. Without generic pointers, addresses in different address spaces
    /// can be equal. Pointers of unknown origin are treated as pointers to
    /// global memory.
    /// Null pointers point into no address space and can be compared with any
    /// pointer.
    /// </summary>
    private void VerifyPointerComparison(
        AddressSpaceResults results,
        Method method,
        CompareValue compare)
    {
        if (compare.Left.Type is not PointerType)
            return;

        var left = GetKnownSpaces(results, compare.Left);
        var right = GetKnownSpaces(results, compare.Right);
        if (left == AddressSpaceFlags.None || right == AddressSpaceFlags.None ||
            left == right)
        {
            return;
        }

        throw new KernelCompilationException(
            KernelDiagnostic.PointerComparisonId,
            string.Format(
                CultureInfo.InvariantCulture,
                ErrorMessages.KernelDiagnosticPointerComparison,
                GetDisplayName(method),
                FormatSpaces(left),
                FormatSpaces(right),
                BackendType),
            compare.Location);
    }

    /// <summary>
    /// Returns the address spaces the given pointer can point into, where
    /// pointers of unknown origin are treated as pointers into global memory
    /// (see <see cref="VerifyNoGenericAddressSpaces"/>).
    /// </summary>
    private static AddressSpaceFlags GetKnownSpaces(
        AddressSpaceResults results,
        Value pointer)
    {
        var flags = results[pointer].Flags;
        if ((flags & AddressSpaceFlags.Generic) != AddressSpaceFlags.None)
            flags = (flags & ~AddressSpaceFlags.Generic) | AddressSpaceFlags.Global;
        return flags;
    }

    /// <summary>
    /// Returns true if the given value is returned from its method or passed to
    /// a called method.
    /// </summary>
    private static bool IsPassedAcrossMethods(Value value)
    {
        foreach (Use use in value.Uses)
        {
            if (use.Target is MethodCall ||
                use.Target is BasicBlock
                {
                    TerminationKind: BlockTerminationKind.Return
                })
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the address spaces of the given flags as a readable list.
    /// </summary>
    private static string FormatSpaces(AddressSpaceFlags flags) =>
        string.Join(
            " and ",
            new[]
            {
                (AddressSpaceFlags.Global, "global"),
                (AddressSpaceFlags.Shared, "shared"),
                (AddressSpaceFlags.Local, "local"),
            }
            .Where(space => (flags & space.Item1) != AddressSpaceFlags.None)
            .Select(space => space.Item2));

    /// <summary>
    /// Returns the user-facing name of a method.
    /// </summary>
    private static string GetDisplayName(Method method) =>
        method.Source is { } source
            ? $"{source.DeclaringType?.Name}.{source.Name}".TrimStart('.')
            : method.Name;

    /// <summary>
    /// Generates code for the given module.
    /// </summary>
    /// <param name="module">The module to generate code for.</param>
    /// <param name="launch">How the entry point is launched.</param>
    /// <returns>The generated code.</returns>
    protected virtual CodeGenerationResult GenerateCode(
        Module module,
        EntryPointLaunch launch)
    {
        var codeGenerator = new CodeGenerator(module, LanguageConfiguration, launch);
        return codeGenerator.GenerateCode();
    }

    /// <summary>
    /// Compiles generated source code into a platform binary using the given compiler
    /// manager.
    /// </summary>
    /// <typeparam name="TManager">
    /// A type that implements <see cref="ICompilerManager"/>.
    /// </typeparam>
    /// <param name="source">The code generation result containing source code.</param>
    /// <param name="manager">The compiler manager to use.</param>
    /// <param name="properties">
    /// The compilation properties driving this compile. Used by backends to map the
    /// ILGPU <see cref="CompilationProperties.OptimizationLevel"/> to a native
    /// compiler optimization flag.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The compilation result, or null if this backend does not support compilation.
    /// </returns>
    public virtual Task<CompilationResult?> CompileSourceAsync<TManager>(
        CodeGenerationResult source,
        TManager manager,
        CompilationProperties properties,
        CancellationToken ct = default)
        where TManager : ICompilerManager =>
        Task.FromResult<CompilationResult?>(null);

    /// <summary>
    /// Returns the native compiler flag that disables multiply-add fusion
    /// if <see cref="CompilationProperties.FusedMultiplyAddMode"/> turns it off.
    /// </summary>
    /// <param name="properties">The compilation properties.</param>
    /// <param name="flag">The flag of the native compiler.</param>
    /// <returns>The flag, or an empty string if fusion is allowed.</returns>
    protected static string GetFusionFlag(
        CompilationProperties properties,
        string flag) =>
        properties.FusedMultiplyAddMode == FusedMultiplyAddMode.Off
            ? flag
            : string.Empty;

    /// <summary>
    /// Translates an ILGPU <see cref="OptimizationLevel"/> to the corresponding
    /// <see cref="NativeOptimizationLevel"/>. ILGPU exposes O0..O2; we offset by one
    /// (O0 → native O1, O1 → native O2, O2 → native O3) so even an "unoptimized" IR
    /// build still gets basic register allocation and inlining from the native
    /// toolchain.
    /// </summary>
    /// <param name="level">The IR optimization level.</param>
    /// <returns>
    /// The native optimization level, never <see cref="NativeOptimizationLevel.O0"/>.
    /// </returns>
    protected static NativeOptimizationLevel GetNativeOptLevel(OptimizationLevel level)
        => level switch
        {
            OptimizationLevel.O0 => NativeOptimizationLevel.O1,
            OptimizationLevel.O1 => NativeOptimizationLevel.O2,
            OptimizationLevel.O2 => NativeOptimizationLevel.O3,
            _ => NativeOptimizationLevel.O2,
        };
}

#pragma warning restore CA1515
