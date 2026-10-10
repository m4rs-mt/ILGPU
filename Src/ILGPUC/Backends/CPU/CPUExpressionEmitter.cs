// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUExpressionEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// Expression emitter for array-based vectorized C# code generation.
/// </summary>
/// <remarks>
/// All values are vectorized (T[] arrays). Binary arithmetic calls
/// CPUMathIntrinsics methods, comparisons call CPUMathIntrinsics comparison
/// methods, and masked loads/stores use CPUVectorIntrinsics.
/// </remarks>
sealed class CPUExpressionEmitter(
    GenerationContext context,
    VectorizationAnalysis analysis)
{
    private readonly ExpressionEmitter _sharedEmitter =
        new(context, new TypeEmitter(context));

    /// <summary>
    /// Returns the name prefixed with "var " on its first declaration and
    /// bare afterwards (see <see cref="GenerationContext.DeclarePrefix"/>).
    /// </summary>
    private string Declare(string name) => context.DeclarePrefix(name) + name;

    /// <summary>
    /// Optional overrides for broadcast temp variable names.
    /// Maps the default name (e.g., <c>"_bcast_pool_long_1"</c>) to the
    /// actual pool parameter name (e.g., <c>"pool_long_2"</c>).
    /// </summary>
    public System.Collections.Generic.Dictionary<string, string>?
        BroadcastNameOverrides
    { get; set; }

    /// <summary>
    /// Returns the broadcast temp name for the given value, applying
    /// overrides if available. Keying by the IR value's ID (not the
    /// result's pool slot name) ensures each call site gets a unique
    /// name even when many values share the same pool slot (which
    /// happens for short-lived compares/predicates).
    /// </summary>
    private string GetBroadcastName(Value value)
    {
        var name = $"_bcast_tmp_{value.Id}";
        if (BroadcastNameOverrides != null &&
            BroadcastNameOverrides.TryGetValue(name, out var overrideName))
            return overrideName;
        return name;
    }

    /// <summary>
    /// Resolves an operand: scalar values and non-scalar values without a
    /// pre-allocated variable (e.g. GroupIndexValue → laneIdx) are inlined
    /// via the shared expression emitter; others use the variable name.
    /// </summary>
    public string ResolveOperand(Value operand) =>
        analysis.IsScalar(operand) || !context.NeedsVariable(operand)
            ? _sharedEmitter.EmitExpression(operand)
            : context.GetValueName(operand);
    /// <summary>
    /// The name of the active mask variable managed by the method emitter.
    /// </summary>
    public const string ActiveMaskName = "activeMask";

    /// <summary>
    /// Returns the C# type name for an IR type, handling both primitives
    /// and structs. Used for generic type parameters in PointerLoad/Store.
    /// </summary>
    private string GetTypeNameForCodegen(TypeValue type)
    {
        if (type is PrimitiveType pt)
            return ((CPULanguageConfiguration)context.LanguageConfig)
                .GetPrimitiveTypeName(pt.BasicValueType);
        if (type is StructureType st)
            return $"struct_{st.Id}";
        if (type is PointerType)
            return "nint";
        if (type is ViewType view)
            return GetViewTypeName(view);
        return "int";
    }

    /// <summary>
    /// Returns the C# type name of a view of the given type.
    /// </summary>
    private string GetViewTypeName(ViewType view) =>
        "CPURuntimeView<" + VectorizationAnalysis.GetScalarTypeName(
            view.ElementType,
            (CPULanguageConfiguration)context.LanguageConfig) + ">";

    private int _viewBufferCounter;

    /// <summary>
    /// Returns a buffer holding the view of every lane
    /// (<c>CPURuntimeView&lt;T&gt;[]</c>). A uniform view is broadcast, and a
    /// view of a local array becomes the views of the private arrays of all
    /// lanes.
    /// </summary>
    public string ResolveViews(Value view)
    {
        if (analysis.IsPerLaneView(view))
            return ResolveOperand(view);

        var viewType = (ViewType)view.Type;
        var typeName = GetViewTypeName(viewType);
        var source = ResolveOperand(view);
        var buffer = $"_views_{_viewBufferCounter++}";
        context.WriteLine($"var {buffer} = new {typeName}[SIMDWidth];");
        if (GetLaneArrayLength(view) is int laneStride)
        {
            var elemType = VectorizationAnalysis.GetScalarTypeName(
                viewType.ElementType,
                (CPULanguageConfiguration)context.LanguageConfig);
            context.WriteLine(
                $"CPUVectorIntrinsics.LaneViews<{elemType}>(" +
                $"{buffer}, {source}, {laneStride});");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{typeName}>({buffer}, {source});");
        }
        return buffer;
    }

    /// <summary>
    /// Returns the element type name of a view.
    /// </summary>
    private string GetViewElementTypeName(Value view) =>
        VectorizationAnalysis.GetScalarTypeName(
            view.GetTypeAs<ViewType>().ElementType,
            (CPULanguageConfiguration)context.LanguageConfig);

    /// <summary>
    /// Returns the index operand of an access through views that differ per
    /// lane: a per-lane index buffer or a uniform 64-bit index.
    /// </summary>
    private string GetLaneViewIndex(Value offset) =>
        analysis.IsScalar(offset)
            ? $"(long)({ResolveOperand(offset)})"
            : ResolveOperand(offset);

    /// <summary>
    /// Returns the number of elements of each lane's private array if the view
    /// addresses a local array, or null for memory shared by all lanes. Local
    /// arrays are allocated once per lane (see
    /// <see cref="CPUMethodEmitter"/>), so element accesses are offset into
    /// the private array of every lane.
    /// </summary>
    public static int? GetLaneArrayLength(Value view)
    {
        while (view is AddressSpaceCast cast)
            view = cast.Source;
        if (view is NewView nv
            && nv.Pointer is Global { AddressSpace: MemoryAddressSpace.Local } g)
        {
            return GetLaneArrayLength(g);
        }
        return null;
    }

    /// <summary>
    /// Returns the number of elements of each lane's copy of a local global.
    /// </summary>
    public static int GetLaneArrayLength(Global global) =>
        global.ArrayLength?.Int32Value ?? 1;

    /// <summary>
    /// Emits a load operation. When the source is a LoadElementAddress on a
    /// view, fuses the LEA into an indexed view access; otherwise falls back
    /// to a masked pointer load.
    /// </summary>
    public void EmitLoad(Load load)
    {
        var result = context.GetValueName(load);

        if (load.Source is LoadElementAddress lea && lea.IsViewAccess)
        {
            if (analysis.IsPerLaneView(lea.Source))
            {
                // Every active lane loads through its own view
                context.WriteLine(
                    "CPUVectorIntrinsics.ViewGatherLoad<" +
                    $"{GetViewElementTypeName(lea.Source)}>({result}, " +
                    $"{ResolveOperand(lea.Source)}, " +
                    $"{GetLaneViewIndex(lea.Offset)}, {ActiveMaskName});");
                return;
            }

            var view = ResolveOperand(lea.Source);
            var index = ResolveOperand(lea.Offset);

            if (GetLaneArrayLength(lea.Source) is int laneStride)
            {
                context.WriteLine(
                    $"{view}.LaneGatherLoad(" +
                    $"{result}, {index}, {ActiveMaskName}, {laneStride});");
                return;
            }

            if (analysis.IsScalar(lea.Offset))
            {
                // Use 'var' only for inline declarations; pre-declared
                // variables (e.g., pool redirects) must not re-declare.
                if (context.NeedsVariable(load))
                    context.WriteLine($"{result} = {view}[{index}];");
                else
                    context.WriteLine($"{Declare(result)} = {view}[{index}];");
            }
            else
            {
                context.WriteLine(
                    $"{view}.GatherLoad(" +
                    $"{result}, {index}, {ActiveMaskName});");
            }
            return;
        }

        // Pointer-type sources (nint[]) use PointerLoad intrinsics
        // instead of MaskedLoad (which is a CPURuntimeView method).
        if (load.Source.Type is PointerType ptrLoad)
        {
            var source = ResolveAddresses(load.Source, load);
            var elemType = GetTypeNameForCodegen(ptrLoad.ElementType);
            context.WriteLine(
                $"CPUVectorIntrinsics.PointerLoad<{elemType}>(" +
                $"{result}, {source}, {ActiveMaskName});");
        }
        else
        {
            var source = ResolveOperand(load.Source);
            context.WriteLine(
                $"{source}.MaskedLoad(" +
                $"{result}, {ActiveMaskName});");
        }
    }

    /// <summary>
    /// Emits a store operation. When the target is a LoadElementAddress on a
    /// view, fuses the LEA into an indexed view access; otherwise falls back
    /// to a masked pointer store.
    /// </summary>
    public void EmitStore(Store store)
    {
        var value = ResolveOperand(store.Value);

        if (store.Target is LoadElementAddress lea && lea.IsViewAccess)
        {
            if (analysis.IsPerLaneView(lea.Source))
            {
                // Every active lane stores through its own view
                var method = analysis.IsScalar(store.Value)
                    ? "ViewScatterFill"
                    : "ViewScatterStore";
                context.WriteLine(
                    $"CPUVectorIntrinsics.{method}<" +
                    $"{GetViewElementTypeName(lea.Source)}>(" +
                    $"{ResolveOperand(lea.Source)}, " +
                    $"{GetLaneViewIndex(lea.Offset)}, {value}, {ActiveMaskName});");
                return;
            }

            var view = ResolveOperand(lea.Source);
            var index = ResolveOperand(lea.Offset);

            if (GetLaneArrayLength(lea.Source) is int laneStride)
            {
                var method = analysis.IsScalar(store.Value)
                    ? "LaneScatterFill"
                    : "LaneScatterStore";
                context.WriteLine(
                    $"{view}.{method}(" +
                    $"{index}, {value}, {ActiveMaskName}, {laneStride});");
                return;
            }

            if (analysis.IsScalar(lea.Offset))
            {
                // All active lanes store to the same element: the last active
                // lane wins, as in a sequential execution of the lanes. Blocks
                // are only emitted for active lanes, so no guard is needed.
                if (!analysis.IsScalar(store.Value))
                {
                    var elemType = GetTypeNameForCodegen(store.Value.Type);
                    context.WriteLine(
                        $"{view}[{index}] = " +
                        $"CPUVectorIntrinsics.LastActive<{elemType}>(" +
                        $"{value}, {ActiveMaskName});");
                }
                else
                {
                    context.WriteLine($"{view}[{index}] = {value};");
                }
            }
            else
            {
                // Vectorized index — use scatter. When the value is scalar
                // (e.g., a constant), use ScatterFill to avoid broadcasting.
                if (analysis.IsScalar(store.Value))
                {
                    context.WriteLine(
                        $"{view}.ScatterFill(" +
                        $"{index}, {value}, {ActiveMaskName});");
                }
                else
                {
                    context.WriteLine(
                        $"{view}.ScatterStore(" +
                        $"{index}, {value}, {ActiveMaskName});");
                }
            }
            return;
        }

        // Pointer-type targets (nint[]) use PointerStore intrinsics.
        var target = store.Target.Type is PointerType
            ? ResolveAddresses(store.Target, store)
            : ResolveOperand(store.Target);
        if (store.Target.Type is PointerType ptrStore)
        {
            var storeValType = GetTypeNameForCodegen(ptrStore.ElementType);
            if (analysis.IsScalar(store.Value))
            {
                context.WriteLine(
                    $"CPUVectorIntrinsics.PointerStoreFill<{storeValType}>(" +
                    $"{target}, {value}, {ActiveMaskName});");
            }
            else
            {
                context.WriteLine(
                    $"CPUVectorIntrinsics.PointerStore<{storeValType}>(" +
                    $"{target}, {value}, {ActiveMaskName});");
            }
        }
        else
        {
            context.WriteLine(
                $"{target}.MaskedStore(" +
                $"{value}, {ActiveMaskName});");
        }
    }

    /// <summary>
    /// Returns the per-lane addresses (<c>nint[]</c>) a memory operation
    /// accesses through the given pointer. Element addresses fused into the
    /// operation are computed into a scratch buffer of the consumer.
    /// </summary>
    private string ResolveAddresses(Value pointer, Value consumer)
    {
        if (pointer is LoadElementAddress lea && !lea.IsViewAccess
            && !context.NeedsVariable(lea))
        {
            var addresses = $"_ptroff_{consumer.Id}";
            context.WriteLine($"var {addresses} = new nint[SIMDWidth];");
            EmitPointerAddresses(lea, addresses);
            return addresses;
        }
        return ResolvePointers(pointer, consumer);
    }

    /// <summary>
    /// Returns the per-lane addresses (<c>nint[]</c>) of a pointer value. A
    /// uniform pointer is broadcast into a scratch buffer of the consumer, as
    /// are the addresses of the per-lane copies of a local global.
    /// </summary>
    public string ResolvePointers(Value pointer, Value consumer)
    {
        if (!analysis.IsScalar(pointer) && GetLocalGlobal(pointer) is null)
            return ResolveOperand(pointer);
        var addresses = $"_ptrs_{consumer.Id}";
        context.WriteLine($"var {addresses} = new nint[SIMDWidth];");
        EmitPointerCopy(addresses, pointer);
        return addresses;
    }

    /// <summary>
    /// Writes the per-lane addresses of a pointer value into a buffer.
    /// </summary>
    public void EmitPointerCopy(string target, Value pointer)
    {
        if (analysis.IsScalar(pointer))
        {
            var address = _sharedEmitter.EmitExpression(pointer);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<nint>({target}, (nint)({address}));");
        }
        else if (GetLocalGlobal(pointer) is { } global)
        {
            // Every lane addresses its own copy of a local global
            context.WriteLine(
                $"CPUVectorIntrinsics.LanePointers({target}, " +
                $"{ResolveOperand(global)});");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.CopyPointers({target}, " +
                $"{ResolveOperand(pointer)});");
        }
    }

    /// <summary>
    /// Returns the local global a pointer refers to (looking through casts),
    /// or null if it refers to other memory.
    /// </summary>
    private static Global? GetLocalGlobal(Value pointer)
    {
        while (pointer is BaseAddressSpaceCast cast)
            pointer = cast.Source;
        return pointer as Global is { AddressSpace: MemoryAddressSpace.Local } global
            ? global
            : null;
    }

    /// <summary>
    /// Computes the per-lane addresses of an element address on a (uniform or
    /// per-lane) pointer with a (uniform or per-lane) offset into a buffer.
    /// </summary>
    public void EmitPointerAddresses(LoadElementAddress lea, string target)
    {
        var elemSize = lea.Source.GetTypeAs<PointerType>().ElementType.Size;
        var source = target;
        if (analysis.IsScalar(lea.Source))
            EmitPointerCopy(target, lea.Source);
        else
            source = ResolveOperand(lea.Source);

        if (analysis.IsScalar(lea.Offset))
        {
            var offset = _sharedEmitter.EmitExpression(lea.Offset);
            context.WriteLine(
                $"CPUVectorIntrinsics.OffsetPointers({target}, {source}, " +
                $"(long)({offset}) * {elemSize});");
        }
        else
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.OffsetPointers({target}, {source}, " +
                $"{ResolveOperand(lea.Offset)}, {elemSize});");
        }
    }

    /// <summary>
    /// Returns the C# type name of a basic value type, which is the element
    /// type of the buffers storing values of that type.
    /// </summary>
    private string GetStorageType(BasicValueType type) =>
        ((CPULanguageConfiguration)context.LanguageConfig).GetPrimitiveTypeName(type);

    /// <summary>
    /// Returns the C# type name of an arithmetic type, which carries the
    /// signedness an operation works with.
    /// </summary>
    private string GetSemanticType(ArithmeticBasicValueType type) =>
        ((CPULanguageConfiguration)context.LanguageConfig).GetPrimitiveTypeName(type);

    /// <summary>
    /// Reinterprets a per-lane buffer as a span of another element type of the
    /// same size (e.g. int[] as Span&lt;uint&gt; for unsigned operations).
    /// </summary>
    private static string Reinterpret(string buffer, string from, string to) =>
        from == to ? buffer : $"MemoryMarshal.Cast<{from}, {to}>({buffer}.AsSpan())";

    /// <summary>
    /// Casts a scalar operand to another C# type unless it already has it.
    /// </summary>
    private static string CastScalar(string operand, string from, string to) =>
        from == to ? Parenthesize(operand) : $"(({to})({operand}))";

    /// <summary>
    /// Wraps an inlined operand expression in parentheses unless it is a
    /// plain name or literal.
    /// </summary>
    private static string Parenthesize(string operand)
    {
        foreach (var c in operand)
        {
            if (!char.IsLetterOrDigit(c) && c is not ('_' or '.'))
                return $"({operand})";
        }
        return operand;
    }

    /// <summary>
    /// Returns true if the result of the operation depends on whether its
    /// operands are interpreted as signed or unsigned integers.
    /// </summary>
    private static bool IsSignSensitive(BinaryArithmeticKind kind) =>
        kind is BinaryArithmeticKind.Div or BinaryArithmeticKind.Rem
            or BinaryArithmeticKind.Shr or BinaryArithmeticKind.Min
            or BinaryArithmeticKind.Max;

    /// <summary>
    /// Returns the name of the method that implements the given binary operation
    /// on the .NET floating-point types and in CPUMathIntrinsics.
    /// </summary>
    private static string GetFloatMethodName(BinaryArithmeticKind kind) =>
        kind switch
        {
            BinaryArithmeticKind.IEEERemainder => "Ieee754Remainder",
            BinaryArithmeticKind.BinaryLog => "Log",
            _ => kind.ToString(),
        };

    /// <summary>
    /// Emits a binary arithmetic operation using CPUMathIntrinsics or a plain
    /// C# operator when both operands are scalar. Values are stored in the
    /// C# types of their basic value types; sign-sensitive operations
    /// reinterpret their operands with the signedness of the operation.
    /// </summary>
    public void EmitBinaryOp(BinaryArithmeticValue binary)
    {
        var left = ResolveOperand(binary.Left);
        var right = ResolveOperand(binary.Right);
        var result = context.GetValueName(binary);

        bool lScalar = analysis.IsScalar(binary.Left);
        bool rScalar = analysis.IsScalar(binary.Right);
        bool isShift = binary.Kind is BinaryArithmeticKind.Shl
            or BinaryArithmeticKind.Shr;

        var storageType = GetStorageType(binary.BasicValueType);
        var opType = IsSignSensitive(binary.Kind)
            ? GetSemanticType(binary.ArithmeticBasicValueType)
            : storageType;
        var rightType = GetStorageType(binary.Right.BasicValueType);

        if (lScalar && rScalar)
        {
            EmitScalarBinaryOp(
                binary, result, left, right, storageType, opType, rightType);
            return;
        }

        string method = binary.Kind switch
        {
            BinaryArithmeticKind.Add => "Add",
            BinaryArithmeticKind.Sub => "Subtract",
            BinaryArithmeticKind.Mul => "Multiply",
            BinaryArithmeticKind.Div => "Divide",
            BinaryArithmeticKind.Rem => "Remainder",
            BinaryArithmeticKind.And => "BitwiseAnd",
            BinaryArithmeticKind.Or => "BitwiseOr",
            BinaryArithmeticKind.Xor => "Xor",
            BinaryArithmeticKind.Shl => "ShiftLeft",
            BinaryArithmeticKind.Shr => "ShiftRight",
            BinaryArithmeticKind.Max => "Max",
            BinaryArithmeticKind.Min => "Min",
            BinaryArithmeticKind.Pow => "Pow",
            BinaryArithmeticKind.CopySign => "CopySign",
            BinaryArithmeticKind.Atan2
                or BinaryArithmeticKind.IEEERemainder
                or BinaryArithmeticKind.BinaryLog => GetFloatMethodName(binary.Kind),
            _ => throw new System.NotSupportedException(
                $"Binary op {binary.Kind} not supported")
        };

        // Bool bitwise ops: bool doesn't implement IBitwiseOperators,
        // so use CPUVectorIntrinsics mask helpers instead of
        // CPUMathIntrinsics.BitwiseAnd/Or/Xor.
        if (binary.BasicValueType == BasicValueType.Int1)
        {
            var boolMethod = binary.Kind switch
            {
                BinaryArithmeticKind.And => "AndMask",
                BinaryArithmeticKind.Or => "OrMask",
                BinaryArithmeticKind.Xor => "XorMask",
                _ => null
            };
            if (boolMethod is not null)
            {
                if (lScalar)
                {
                    var bcast = GetBroadcastName(binary);
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<bool>({bcast}, {left});");
                    left = bcast;
                }
                else if (rScalar)
                {
                    var bcast = GetBroadcastName(binary);
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<bool>({bcast}, {right});");
                    right = bcast;
                }
                context.WriteLine(
                    $"CPUVectorIntrinsics.{boolMethod}({result}, " +
                    $"{left}, {right});");
                return;
            }
        }

        if (lScalar)
        {
            var bcast = GetBroadcastName(binary);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{storageType}>(" +
                $"{bcast}, {left});");
            left = bcast;
        }

        // Shift amounts are ints; a uniform amount stays scalar
        if (isShift)
        {
            string amount;
            if (rScalar)
                amount = $"(int)({right})";
            else if (rightType == "int")
                amount = right;
            else
            {
                amount = $"_shamt_{binary.Id}";
                context.WriteLine($"var {amount} = new int[SIMDWidth];");
                context.WriteLine(
                    $"CPUMathIntrinsics.Convert<{rightType}, int>(" +
                    $"{amount}, {right});");
            }
            context.WriteLine(
                $"CPUMathIntrinsics.{method}<{opType}>(" +
                $"{Reinterpret(result, storageType, opType)}, " +
                $"{Reinterpret(left, storageType, opType)}, {amount});");
            return;
        }

        if (rScalar)
        {
            var bcast = GetBroadcastName(binary);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{storageType}>(" +
                $"{bcast}, {right});");
            right = bcast;
        }

        // Division and remainder only compute the active lanes: inactive
        // lanes may hold zero divisors, and the divisor buffer itself must
        // not be modified as it may be used again later.
        var maskArg = "";
        if (binary.Kind is BinaryArithmeticKind.Div or BinaryArithmeticKind.Rem)
        {
            method += "Masked";
            maskArg = $", {ActiveMaskName}";
        }

        context.WriteLine(
            $"CPUMathIntrinsics.{method}<{opType}>(" +
            $"{Reinterpret(result, storageType, opType)}, " +
            $"{Reinterpret(left, storageType, opType)}, " +
            $"{Reinterpret(right, storageType, opType)}{maskArg});");
    }

    /// <summary>
    /// Emits a binary operation on two uniform operands.
    /// </summary>
    private void EmitScalarBinaryOp(
        BinaryArithmeticValue binary,
        string result,
        string left,
        string right,
        string storageType,
        string opType,
        string rightType)
    {
        string? op = binary.Kind switch
        {
            BinaryArithmeticKind.Add => "+",
            BinaryArithmeticKind.Sub => "-",
            BinaryArithmeticKind.Mul => "*",
            BinaryArithmeticKind.Div => "/",
            BinaryArithmeticKind.Rem => "%",
            BinaryArithmeticKind.And => "&",
            BinaryArithmeticKind.Or => "|",
            BinaryArithmeticKind.Xor => "^",
            BinaryArithmeticKind.Shl => "<<",
            BinaryArithmeticKind.Shr => ">>",
            _ => null
        };

        var l = CastScalar(left, storageType, opType);
        string expr;
        if (op is not null)
        {
            // Shift amounts must be ints in C#
            var r = binary.Kind is BinaryArithmeticKind.Shl
                or BinaryArithmeticKind.Shr
                ? CastScalar(right, rightType, "int")
                : CastScalar(right, storageType, opType);
            expr = $"{l} {op} {r}";
        }
        else if (binary.Kind is BinaryArithmeticKind.Max
            or BinaryArithmeticKind.Min)
        {
            var r = CastScalar(right, storageType, opType);
            var function = binary.Kind == BinaryArithmeticKind.Max ? "Max" : "Min";
            expr = $"System.Math.{function}({l}, {r})";
        }
        else if (binary.Kind is BinaryArithmeticKind.Pow
            or BinaryArithmeticKind.CopySign
            or BinaryArithmeticKind.Atan2
            or BinaryArithmeticKind.IEEERemainder
            or BinaryArithmeticKind.BinaryLog)
        {
            // The floating-point types implement these as static methods with
            // the semantics of their System.Math counterparts
            var function = GetFloatMethodName(binary.Kind);
            var r = CastScalar(right, storageType, opType);
            expr = $"{opType}.{function}({l}, {r})";
        }
        else
        {
            throw new System.NotSupportedException(
                $"Binary op {binary.Kind} not supported");
        }

        // C# promotes small integer operands to int: truncate the result back
        // to the storage type with wrap-around semantics.
        var cast = storageType == "bool" ? "" : $"({storageType})";
        context.WriteLine($"{Declare(result)} = unchecked({cast}({expr}));");
    }

    /// <summary>
    /// Emits a unary arithmetic operation using CPUMathIntrinsics or a plain
    /// C# operator when the operand is scalar.
    /// </summary>
    public void EmitUnaryOp(UnaryArithmeticValue unary)
    {
        var operand = ResolveOperand(unary.Value);
        var result = context.GetValueName(unary);

        // Bit counting takes the operand's integer type and yields an int.
        var bitCountMethod = unary.Kind switch
        {
            UnaryArithmeticKind.PopC => "PopCount",
            UnaryArithmeticKind.CLZ => "LeadingZeroCount",
            UnaryArithmeticKind.CTZ => "TrailingZeroCount",
            _ => null
        };
        // Floating-point classification takes the operand's type and yields
        // a bool; C# has no isnan/isinf/isfinite functions.
        var classifyMethod = unary.Kind switch
        {
            UnaryArithmeticKind.IsNaN => "IsNaN",
            UnaryArithmeticKind.IsInf => "IsInfinity",
            UnaryArithmeticKind.IsFin => "IsFinite",
            _ => null
        };
        if (classifyMethod is not null
            && unary.Value.Type is PrimitiveType classifyType)
        {
            var classifyConfig = (CPULanguageConfiguration)context.LanguageConfig;
            var classifyElemType = classifyConfig.GetPrimitiveTypeName(
                classifyType.BasicValueType);
            context.WriteLine(analysis.IsScalar(unary.Value)
                ? $"{Declare(result)} = " +
                    $"{classifyElemType}.{classifyMethod}({operand});"
                : $"CPUMathIntrinsics.{classifyMethod}<{classifyElemType}>(" +
                    $"{result}, {operand});");
            return;
        }

        if (bitCountMethod is not null
            && unary.Value.Type is PrimitiveType operandType)
        {
            var bitConfig = (CPULanguageConfiguration)context.LanguageConfig;
            var operandElemType = bitConfig.GetPrimitiveTypeName(
                operandType.BasicValueType);
            context.WriteLine(analysis.IsScalar(unary.Value)
                ? $"{Declare(result)} = " +
                    $"(int){operandElemType}.{bitCountMethod}({operand});"
                : $"CPUMathIntrinsics.{bitCountMethod}<{operandElemType}>(" +
                    $"{result}, {operand});");
            return;
        }

        if (analysis.IsScalar(unary.Value))
        {
            // Scalar unary operations use plain C# operators or Math calls;
            // float operands use MathF so that the result stays a float.
            bool isFloat = unary.BasicValueType == BasicValueType.Float32;
            var math = isFloat ? "MathF" : "Math";
            var one = isFloat ? "1.0f" : "1.0";
            var two = isFloat ? "2.0f" : "2.0";
            var expr = unary.Kind switch
            {
                UnaryArithmeticKind.Neg => $"-{operand}",
                // `~` does not compile on C# bool; logical Not must use `!`.
                UnaryArithmeticKind.Not =>
                    unary.BasicValueType == BasicValueType.Int1
                        ? $"!{operand}"
                        : $"~{operand}",
                UnaryArithmeticKind.Abs => $"{math}.Abs({operand})",
                UnaryArithmeticKind.Sqrt => $"{math}.Sqrt({operand})",
                UnaryArithmeticKind.Sin => $"{math}.Sin({operand})",
                UnaryArithmeticKind.Cos => $"{math}.Cos({operand})",
                UnaryArithmeticKind.Tan => $"{math}.Tan({operand})",
                UnaryArithmeticKind.Asin => $"{math}.Asin({operand})",
                UnaryArithmeticKind.Acos => $"{math}.Acos({operand})",
                UnaryArithmeticKind.Atan => $"{math}.Atan({operand})",
                UnaryArithmeticKind.Sinh => $"{math}.Sinh({operand})",
                UnaryArithmeticKind.Cosh => $"{math}.Cosh({operand})",
                UnaryArithmeticKind.Tanh => $"{math}.Tanh({operand})",
                UnaryArithmeticKind.Asinh => $"{math}.Asinh({operand})",
                UnaryArithmeticKind.Acosh => $"{math}.Acosh({operand})",
                UnaryArithmeticKind.Atanh => $"{math}.Atanh({operand})",
                UnaryArithmeticKind.Exp => $"{math}.Exp({operand})",
                UnaryArithmeticKind.Exp2 => $"{math}.Pow({two}, {operand})",
                UnaryArithmeticKind.Log => $"{math}.Log({operand})",
                UnaryArithmeticKind.Log2 => $"{math}.Log2({operand})",
                UnaryArithmeticKind.Log10 => $"{math}.Log10({operand})",
                UnaryArithmeticKind.Floor => $"{math}.Floor({operand})",
                UnaryArithmeticKind.Ceiling => $"{math}.Ceiling({operand})",
                UnaryArithmeticKind.Rcp => $"({one} / {operand})",
                UnaryArithmeticKind.Rsqrt => $"({one} / {math}.Sqrt({operand}))",
                _ => null
            };
            if (expr != null)
            {
                context.WriteLine($"{Declare(result)} = {expr};");
                return;
            }
            // Unknown kind — fall through to shared emitter via return false
        }

        // Vectorized unary operations use CPUMathIntrinsics
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var elemType = (unary.Type is PrimitiveType pt)
            ? config.GetPrimitiveTypeName(pt.BasicValueType) : "int";

        // Bool masks: bool does not implement IBitwiseOperators, and a
        // bitwise complement would be wrong anyway — use the dedicated
        // mask intrinsic (mirrors the AndMask/OrMask binary handling).
        if (unary.Kind == UnaryArithmeticKind.Not &&
            unary.BasicValueType == BasicValueType.Int1)
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.NotMask({result}, {operand});");
            return;
        }

        string? method = unary.Kind switch
        {
            UnaryArithmeticKind.Neg => "Negate",
            UnaryArithmeticKind.Not => "OnesComplement",
            UnaryArithmeticKind.Abs => "Abs",
            UnaryArithmeticKind.Sqrt => "Sqrt",
            UnaryArithmeticKind.Sin => "Sin",
            UnaryArithmeticKind.Cos => "Cos",
            UnaryArithmeticKind.Tan => "Tan",
            UnaryArithmeticKind.Asin => "Asin",
            UnaryArithmeticKind.Acos => "Acos",
            UnaryArithmeticKind.Atan => "Atan",
            UnaryArithmeticKind.Sinh => "Sinh",
            UnaryArithmeticKind.Cosh => "Cosh",
            UnaryArithmeticKind.Tanh => "Tanh",
            UnaryArithmeticKind.Asinh => "Asinh",
            UnaryArithmeticKind.Acosh => "Acosh",
            UnaryArithmeticKind.Atanh => "Atanh",
            UnaryArithmeticKind.Exp => "Exp",
            UnaryArithmeticKind.Exp2 => "Exp2",
            UnaryArithmeticKind.Log => "Log",
            UnaryArithmeticKind.Log2 => "Log2",
            UnaryArithmeticKind.Log10 => "Log10",
            UnaryArithmeticKind.Floor => "Floor",
            UnaryArithmeticKind.Ceiling => "Ceiling",
            UnaryArithmeticKind.Rcp => "Reciprocal",
            UnaryArithmeticKind.Rsqrt => "Rsqrt",
            _ => null
        };

        if (method != null)
        {
            context.WriteLine(
                $"CPUMathIntrinsics.{method}<{elemType}>(" +
                $"{result}, {operand});");
            return;
        }

        // Fallback to shared expression emitter for unhandled kinds
        var typeEmitter = new TypeEmitter(context);
        var exprEmitter = new ExpressionEmitter(context, typeEmitter);
        var expr2 = exprEmitter.EmitDefinition(unary);
        context.WriteLine($"{result} = {expr2};");
    }

    /// <summary>
    /// Emits a vectorized multiply-add lane by lane, as its operands may mix
    /// uniform and per-lane values. Like the scalar emission, it computes
    /// a * b + c.
    /// </summary>
    public void EmitTernaryOp(TernaryArithmeticValue ternary)
    {
        if (ternary.Kind != TernaryArithmeticKind.MultiplyAdd)
        {
            throw new System.NotSupportedException(
                $"Ternary op {ternary.Kind} not supported");
        }

        string Lane(Value operand)
        {
            var name = ResolveOperand(operand);
            return analysis.IsScalar(operand) ? name : $"{name}[_bi]";
        }

        var expr = $"{Lane(ternary.First)} * {Lane(ternary.Second)} + " +
            Lane(ternary.Third);
        context.WriteLine(
            $"for (int _bi = 0; _bi < SIMDWidth; _bi++) " +
            $"{context.GetValueName(ternary)}[_bi] = {expr};");
    }

    /// <summary>
    /// Emits a comparison operation producing a bool[] mask using CPUMathIntrinsics,
    /// or a plain C# operator when both operands are scalar. Integer orderings
    /// use the signedness of the comparison; floating-point comparisons
    /// flagged unordered are also true if either operand is NaN.
    /// </summary>
    public void EmitCompare(CompareValue compare)
    {
        var left = ResolveOperand(compare.Left);
        var right = ResolveOperand(compare.Right);
        var result = context.GetValueName(compare);

        bool lScalar = analysis.IsScalar(compare.Left);
        bool rScalar = analysis.IsScalar(compare.Right);

        var leftBvt = compare.Left.BasicValueType;
        bool isFloat = leftBvt.IsFloat();
        bool unordered = isFloat && compare.IsUnsignedOrUnordered;
        bool isOrdering = compare.Kind is not (
            CompareKind.Equal or CompareKind.NotEqual);
        var storageType = GetStorageType(leftBvt);
        var opType = !isFloat && isOrdering
            ? GetSemanticType(compare.CompareType)
            : storageType;

        if (lScalar && rScalar)
        {
            var l = CastScalar(left, storageType, opType);
            var r = CastScalar(right, storageType, opType);
            string ordered(CompareKind kind) => kind switch
            {
                CompareKind.Equal => $"{l} == {r}",
                CompareKind.NotEqual => $"{l} != {r}",
                CompareKind.LessThan => $"{l} < {r}",
                CompareKind.LessEqual => $"{l} <= {r}",
                CompareKind.GreaterThan => $"{l} > {r}",
                CompareKind.GreaterEqual => $"{l} >= {r}",
                _ => throw new System.NotSupportedException(
                    $"Compare {compare.Kind} not supported")
            };
            string expr;
            if (!isFloat)
                expr = ordered(compare.Kind);
            else if (unordered)
            {
                // An unordered comparison is the negated ordered inverse
                expr = compare.Kind switch
                {
                    CompareKind.Equal => $"!({l} < {r} || {l} > {r})",
                    CompareKind.NotEqual => $"{l} != {r}",
                    CompareKind.LessThan => $"!({l} >= {r})",
                    CompareKind.LessEqual => $"!({l} > {r})",
                    CompareKind.GreaterThan => $"!({l} <= {r})",
                    _ => $"!({l} < {r})"
                };
            }
            else
            {
                // C# != is unordered; the ordered variant excludes NaNs
                expr = compare.Kind == CompareKind.NotEqual
                    ? $"({l} < {r} || {l} > {r})"
                    : ordered(compare.Kind);
            }
            context.WriteLine($"{Declare(result)} = {expr};");
            return;
        }

        // Detect type mismatch (e.g., int vs long in bounds checks).
        // CPUMathIntrinsics comparison methods require both operands to
        // be the same type T. When they differ, widen the narrower operand.
        var leftPrim = compare.Left.Type as PrimitiveType;
        var rightPrim = compare.Right.Type as PrimitiveType;
        if (leftPrim != null && rightPrim != null
            && leftPrim.BasicValueType != rightPrim.BasicValueType)
        {
            string mismatchMethod = compare.Kind switch
            {
                CompareKind.Equal => "Equal",
                CompareKind.NotEqual => "NotEqual",
                CompareKind.LessThan => "LessThan",
                CompareKind.LessEqual => "LessEqual",
                CompareKind.GreaterThan => "GreaterThan",
                _ => "GreaterEqual",
            };
            EmitMismatchedCompare(
                left, right, result, lScalar, rScalar,
                mismatchMethod, leftPrim, rightPrim);
            return;
        }

        if (lScalar)
        {
            var bcast = GetBroadcastName(compare);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{storageType}>(" +
                $"{bcast}, {left});");
            left = bcast;
        }
        else if (rScalar)
        {
            var bcast = GetBroadcastName(compare);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{storageType}>(" +
                $"{bcast}, {right});");
            right = bcast;
        }

        // Bool masks do not implement the generic comparison interfaces
        if (leftBvt == BasicValueType.Int1 && !isOrdering)
        {
            var maskMethod = compare.Kind == CompareKind.Equal
                ? "EqualMask"
                : "XorMask";
            context.WriteLine(
                $"CPUVectorIntrinsics.{maskMethod}({result}, {left}, {right});");
            return;
        }

        // Unordered comparisons negate the ordered inverse comparison
        bool negate = false;
        string method;
        if (unordered && compare.Kind != CompareKind.NotEqual)
        {
            negate = compare.Kind != CompareKind.Equal;
            method = compare.Kind switch
            {
                CompareKind.Equal => "EqualUnordered",
                CompareKind.LessThan => "GreaterEqual",
                CompareKind.LessEqual => "GreaterThan",
                CompareKind.GreaterThan => "LessEqual",
                _ => "LessThan"
            };
        }
        else
        {
            method = compare.Kind switch
            {
                CompareKind.Equal => "Equal",
                CompareKind.NotEqual => isFloat && !unordered
                    ? "NotEqualOrdered"
                    : "NotEqual",
                CompareKind.LessThan => "LessThan",
                CompareKind.LessEqual => "LessEqual",
                CompareKind.GreaterThan => "GreaterThan",
                CompareKind.GreaterEqual => "GreaterEqual",
                _ => throw new System.NotSupportedException(
                    $"Compare {compare.Kind} not supported")
            };
        }

        context.WriteLine(
            $"CPUMathIntrinsics.{method}<{opType}>({result}, " +
            $"{Reinterpret(left, storageType, opType)}, " +
            $"{Reinterpret(right, storageType, opType)});");
        if (negate)
            context.WriteLine($"CPUVectorIntrinsics.NotMask({result}, {result});");
    }

    /// <summary>
    /// Emits a comparison where the left and right operands have different
    /// primitive types (e.g., <c>int</c> vs <c>long</c> in bounds checks).
    /// The narrower operand is widened to match the wider operand's type
    /// before the comparison.
    /// </summary>
    private void EmitMismatchedCompare(
        string left, string right, string result,
        bool lScalar, bool rScalar,
        string method,
        PrimitiveType leftPrim, PrimitiveType rightPrim)
    {
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        bool leftNarrower = leftPrim.Size < rightPrim.Size;
        var widerBvt = leftNarrower
            ? rightPrim.BasicValueType : leftPrim.BasicValueType;
        var narrowerBvt = leftNarrower
            ? leftPrim.BasicValueType : rightPrim.BasicValueType;
        var widerType = config.GetPrimitiveTypeName(widerBvt);
        var narrowerType = config.GetPrimitiveTypeName(narrowerBvt);

        // Sanitize result for use in C# variable names (pooled names
        // contain dots like "pool.pool_int_0" which are invalid in
        // identifiers).
        var safeResult = result.Replace('.', '_');

        // Widen the narrower operand
        if (leftNarrower)
        {
            if (lScalar)
            {
                // Scalar narrower: cast and broadcast into a fresh buffer
                var bcast = $"_cvt_{safeResult}_lb";
                context.WriteLine(
                    $"var {bcast} = new {widerType}[SIMDWidth];");
                context.WriteLine(
                    $"CPUVectorIntrinsics.Broadcast<{widerType}>(" +
                    $"{bcast}, ({widerType}){left});");
                left = bcast;
            }
            else
            {
                // Vectorized narrower: convert to wider type
                var cvt = $"_cvt_{safeResult}_l";
                context.WriteLine(
                    $"var {cvt} = new {widerType}[SIMDWidth];");
                context.WriteLine(
                    $"CPUMathIntrinsics.Convert<{narrowerType}, " +
                    $"{widerType}>({cvt}, {left});");
                left = cvt;
            }
        }
        else
        {
            if (rScalar)
            {
                var bcast = $"_cvt_{safeResult}_rb";
                context.WriteLine(
                    $"var {bcast} = new {widerType}[SIMDWidth];");
                context.WriteLine(
                    $"CPUVectorIntrinsics.Broadcast<{widerType}>(" +
                    $"{bcast}, ({widerType}){right});");
                right = bcast;
            }
            else
            {
                var cvt = $"_cvt_{safeResult}_r";
                context.WriteLine(
                    $"var {cvt} = new {widerType}[SIMDWidth];");
                context.WriteLine(
                    $"CPUMathIntrinsics.Convert<{narrowerType}, " +
                    $"{widerType}>({cvt}, {right});");
                right = cvt;
            }
        }

        // Both operands now have the same wider type
        context.WriteLine(
            $"CPUMathIntrinsics.{method}<{widerType}>(" +
            $"{result}, {left}, {right});");
    }

    /// <summary>
    /// Emits a float-as-int reinterpret bit cast. For scalar operands, uses
    /// <c>BitConverter.*Bits</c>. For vectorized (span) operands, copies
    /// bits lane by lane with <c>BitConverter</c> in a tight loop.
    /// </summary>
    public void EmitFloatAsIntCast(FloatAsIntCast cast)
    {
        var operand = ResolveOperand(cast.Source);
        var result = context.GetValueName(cast);
        var srcBvt = cast.Source.BasicValueType;
        var scalarHelper = srcBvt switch
        {
            BasicValueType.Float32 => "BitConverter.SingleToInt32Bits",
            BasicValueType.Float64 => "BitConverter.DoubleToInt64Bits",
            _ => null
        };
        if (scalarHelper is null)
            throw new System.NotSupportedException(
                $"FloatAsIntCast from {srcBvt} not supported on CPU");

        // Scalar cast: the result name is a scalar variable (e.g. "x_42"),
        // use `var result = helper(operand);`. For vectorized casts the
        // result is a pool slot (e.g. `pool.pool_long_2`) — emit a per-lane
        // loop without `var`.
        if (analysis.IsScalar(cast))
        {
            context.WriteLine($"{Declare(result)} = {scalarHelper}({operand});");
            return;
        }

        // Vectorized result; the source may be scalar (broadcast) or
        // vectorized (per-lane). We detect scalar source by checking the
        // analysis and avoid indexing the operand in that case.
        var operandAccess = analysis.IsScalar(cast.Source)
            ? operand
            : $"{operand}[_bi]";
        context.WriteLine(
            $"for (int _bi = 0; _bi < SIMDWidth; _bi++) " +
            $"{result}[_bi] = {scalarHelper}({operandAccess});");
    }

    /// <summary>
    /// Emits an int-as-float reinterpret bit cast. For scalar operands, uses
    /// <c>BitConverter.*Bits</c>. For vectorized (span) operands, copies
    /// bits lane by lane.
    /// </summary>
    public void EmitIntAsFloatCast(IntAsFloatCast cast)
    {
        var operand = ResolveOperand(cast.Source);
        var result = context.GetValueName(cast);
        var tgtBvt = cast.BasicValueType;
        var scalarHelper = tgtBvt switch
        {
            BasicValueType.Float32 => "BitConverter.Int32BitsToSingle",
            BasicValueType.Float64 => "BitConverter.Int64BitsToDouble",
            _ => null
        };
        if (scalarHelper is null)
            throw new System.NotSupportedException(
                $"IntAsFloatCast to {tgtBvt} not supported on CPU");

        // Scalar cast: the result name is a scalar variable (e.g. "x_42"),
        // use `var result = helper(operand);`. For vectorized casts the
        // result is a pool slot (e.g. `pool.pool_long_2`) — emit a per-lane
        // loop without `var`.
        if (analysis.IsScalar(cast))
        {
            context.WriteLine($"{Declare(result)} = {scalarHelper}({operand});");
            return;
        }

        var operandAccess = analysis.IsScalar(cast.Source)
            ? operand
            : $"{operand}[_bi]";
        context.WriteLine(
            $"for (int _bi = 0; _bi < SIMDWidth; _bi++) " +
            $"{result}[_bi] = {scalarHelper}({operandAccess});");
    }

    /// <summary>
    /// Emits a type conversion operation. The operand is read with the
    /// signedness of the conversion's source type and converted to its target
    /// type before it is stored in the result's storage type.
    /// </summary>
    public void EmitConvert(ConvertValue convert)
    {
        var operand = ResolveOperand(convert.Value);
        var result = context.GetValueName(convert);

        var sourceBvt = convert.Value.BasicValueType;
        var targetBvt = convert.BasicValueType;
        var sourceStorage = GetStorageType(sourceBvt);
        var sourceType = GetSemanticType(convert.SourceType);
        var targetStorage = GetStorageType(targetBvt);
        var targetType = GetSemanticType(convert.TargetType);

        if (analysis.IsScalar(convert.Value))
        {
            string expr;
            if (sourceBvt == BasicValueType.Int1)
            {
                expr = $"(({operand}) ? ({targetType})1 : ({targetType})0)";
            }
            else if (targetBvt == BasicValueType.Int1)
            {
                var source = CastScalar(operand, sourceStorage, sourceType);
                expr = $"{source} != ({sourceType})0";
            }
            else
            {
                var source = CastScalar(operand, sourceStorage, sourceType);
                expr = $"({targetType}){source}";
            }
            expr = CastScalar(expr, targetType, targetStorage);
            context.WriteLine($"{Declare(result)} = unchecked({expr});");
            return;
        }

        var target = Reinterpret(result, targetStorage, targetType);
        if (sourceBvt == BasicValueType.Int1)
        {
            context.WriteLine(
                $"CPUMathIntrinsics.ConvertFromBool<{targetType}>(" +
                $"{target}, {operand});");
        }
        else if (targetBvt == BasicValueType.Int1)
        {
            context.WriteLine(
                $"CPUMathIntrinsics.ConvertToBool<{sourceType}>(" +
                $"{result}, {Reinterpret(operand, sourceStorage, sourceType)});");
        }
        else
        {
            context.WriteLine(
                $"CPUMathIntrinsics.Convert<{sourceType}, {targetType}>(" +
                $"{target}, {Reinterpret(operand, sourceStorage, sourceType)});");
        }
    }

    /// <summary>
    /// Emits a predicate (conditional select) operation. When the condition
    /// is vectorized (<c>bool[]</c>), uses <c>CPUVectorIntrinsics.Select</c>
    /// instead of the C# ternary operator.
    /// </summary>
    public void EmitPredicate(Predicate predicate)
    {
        var condition = ResolveOperand(predicate.Condition);
        var trueVal = ResolveOperand(predicate.TrueValue);
        var falseVal = ResolveOperand(predicate.FalseValue);
        var result = context.GetValueName(predicate);

        if (analysis.IsScalar(predicate))
        {
            // Scalar condition and operands — plain ternary
            context.WriteLine(
                $"{Declare(result)} = {condition} ? {trueVal} : {falseVal};");
            return;
        }

        // Vectorized select — use Select with explicit type arg
        var elemType = GetTypeNameForCodegen(predicate.Type);

        // Use the value's unique ID for temporary variable names to avoid
        // collisions when multiple Predicate values reuse the same pool slot.
        var uid = predicate.Id;

        // Views select the view of every lane; views of local arrays become
        // the views of the private arrays of all lanes
        if (predicate.Type is ViewType)
        {
            trueVal = ResolveViews(predicate.TrueValue);
            falseVal = ResolveViews(predicate.FalseValue);
        }

        // A uniform condition selecting between per-lane operands is
        // broadcast to a lane mask; a plain ternary would assign one
        // operand array by reference (aliasing e.g. laneIdx).
        if (analysis.IsScalar(predicate.Condition))
        {
            var mask = $"_sel_{uid}_c";
            context.WriteLine($"var {mask} = new bool[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<bool>({mask}, {condition});");
            condition = mask;
        }

        // Ensure true/false values are vectorized (broadcast if scalar)
        bool isView = predicate.Type is ViewType;
        if (!isView && analysis.IsScalar(predicate.TrueValue))
        {
            var bcast = $"_sel_{uid}_t";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {ToLaneElement(trueVal, elemType)});");
            trueVal = bcast;
        }
        if (!isView && analysis.IsScalar(predicate.FalseValue))
        {
            var bcast = $"_sel_{uid}_f";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {ToLaneElement(falseVal, elemType)});");
            falseVal = bcast;
        }

        context.WriteLine(
            $"CPUVectorIntrinsics.Select<{elemType}>(" +
            $"{result}, {condition}, {trueVal}, {falseVal});");
    }

    /// <summary>
    /// Converts a uniform operand to an element of a per-lane buffer of the
    /// given element type: uniform pointers become addresses.
    /// </summary>
    internal static string ToLaneElement(string operand, string elementType) =>
        elementType == "nint" ? $"(nint)({operand})" : operand;

    /// <summary>
    /// Emits a non-fusable vectorized LoadElementAddress on a view.
    /// Computes per-lane memory addresses (<c>nint[]</c>) from the view's
    /// base pointer and per-lane indices, for use by atomic operations.
    /// </summary>
    public void EmitViewLEA(LoadElementAddress lea)
    {
        var view = ResolveOperand(lea.Source);
        var index = ResolveOperand(lea.Offset);
        var result = context.GetValueName(lea);

        // Get the element size for pointer arithmetic. Use TypeValue.Size so
        // struct view elements produce the correct stride (e.g. a view of an
        // 8-int [InlineArray(8)] struct must stride by 32, not fall back to 4).
        var elemType = lea.Source.Type is ViewType vt
            ? vt.ElementType : lea.Source.Type;
        var elemSize = elemType.Size;

        if (analysis.IsPerLaneView(lea.Source))
        {
            // Every active lane addresses an element of its own view
            context.WriteLine(
                "CPUVectorIntrinsics.ComputeViewAddresses<" +
                $"{GetViewElementTypeName(lea.Source)}>({result}, {view}, " +
                $"{GetLaneViewIndex(lea.Offset)}, {elemSize}, {ActiveMaskName});");
            return;
        }

        // Scalar offset: broadcast to a per-lane array first
        if (analysis.IsScalar(lea.Offset))
        {
            var bcast = $"_lea_idx_{lea.Id}";
            context.WriteLine(
                $"var {bcast} = new int[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<int>({bcast}, {index});");
            index = bcast;
        }

        if (GetLaneArrayLength(lea.Source) is int laneStride)
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.ComputeLaneViewAddresses(" +
                $"{result}, {view}, {index}, {elemSize}, {ActiveMaskName}, " +
                $"{laneStride});");
            return;
        }

        context.WriteLine(
            $"CPUVectorIntrinsics.ComputeViewAddresses(" +
            $"{result}, {view}, {index}, {elemSize}, {ActiveMaskName});");
    }

    /// <summary>
    /// Emits a group or warp predicate barrier. The lanes of a vectorized call
    /// form one warp that runs in lockstep, and the whole group of kernels
    /// that synchronize their group, so the barrier itself needs no
    /// synchronization and the predicate is combined across the active lanes.
    /// </summary>
    public void EmitPredicateBarrier(PredicateBarrier barrier)
    {
        if (!context.NeedsVariable(barrier))
            return;

        var result = context.GetValueName(barrier);
        var predicate = ResolveOperand(barrier.Predicate);
        bool uniform = analysis.IsScalar(barrier.Predicate);
        if (uniform &&
            barrier.PredicateKind != PredicateBarrierPredicateKind.PopCount)
        {
            // A uniform predicate is its own logical and/or
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<bool>({result}, {predicate});");
            return;
        }

        var method = barrier.PredicateKind switch
        {
            PredicateBarrierPredicateKind.PopCount => "BarrierPopCount",
            PredicateBarrierPredicateKind.And => "BarrierAnd",
            PredicateBarrierPredicateKind.Or => "BarrierOr",
            _ => throw new System.NotSupportedException(
                $"Predicate barrier {barrier.PredicateKind} not supported")
        };
        context.WriteLine(
            $"CPUVectorIntrinsics.{method}({result}, {predicate}, " +
            $"{ActiveMaskName});");
    }

    /// <summary>
    /// Returns the C# type that stores the values of a warp reduction or scan and
    /// the C# type that its operation works with, which is the unsigned type of
    /// an unsigned minimum or maximum.
    /// </summary>
    private (string StorageType, string SemanticType) GetCollectiveTypes(
        ThreadValue collective,
        ArithmeticBasicValueType type) =>
        (GetStorageType(collective.BasicValueType), GetSemanticType(type));

    /// <summary>
    /// Reinterprets the per-lane result array of a warp reduction or scan that
    /// works with another C# type than its storage type: an array of an
    /// unsigned integer type is also an array of the signed type of its size.
    /// </summary>
    private static string ReinterpretResult(string array, string from, string to) =>
        from == to ? array : $"({from}[])(object){array}";

    /// <summary>
    /// Emits a warp reduce operation using the CPU's vectorized helpers.
    /// The operand is resolved through the vectorized path so that
    /// per-lane arrays feed into the <c>ReadOnlySpan&lt;T&gt;</c> parameters
    /// of <c>CPUVectorIntrinsics.WarpReduce*</c>.
    /// </summary>
    public void EmitWarpReduce(WarpReduce reduce)
    {
        var variable = ResolveOperand(reduce.Variable);
        var result = context.GetValueName(reduce);
        var op = reduce.IntrinsicOp
            ?? throw new System.NotSupportedException(
                "Custom warp reduce operations must be lowered before codegen");
        var type = reduce.BasicValueType.GetArithmeticBasicValueType(reduce.IsUnsigned);

        if (analysis.IsScalar(reduce.Variable))
        {
            var config = (CPULanguageConfiguration)context.LanguageConfig;
            var elemType = config.GetPrimitiveTypeName(
                ((PrimitiveType)reduce.Type).BasicValueType);
            var bcast = $"_wr_bcast_{reduce.Id}";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {variable});");
            variable = bcast;
        }

        var (storageType, semanticType) = GetCollectiveTypes(reduce, type);
        var text = context.IntrinsicEmitter.EmitWarpReduce(
            Reinterpret(variable, storageType, semanticType), op, reduce.Kind, type);
        context.WriteLine(
            $"{result} = {ReinterpretResult(text, storageType, semanticType)};");
    }

    /// <summary>
    /// Emits a warp scan operation using the CPU's vectorized helpers.
    /// </summary>
    public void EmitWarpScan(WarpScan scan)
    {
        var variable = ResolveOperand(scan.Variable);
        var result = context.GetValueName(scan);
        var op = scan.IntrinsicOp
            ?? throw new System.NotSupportedException(
                "Custom warp scan operations must be lowered before codegen");
        var type = scan.BasicValueType.GetArithmeticBasicValueType(scan.IsUnsigned);

        if (analysis.IsScalar(scan.Variable))
        {
            var config = (CPULanguageConfiguration)context.LanguageConfig;
            var elemType = config.GetPrimitiveTypeName(
                ((PrimitiveType)scan.Type).BasicValueType);
            var bcast = $"_ws_bcast_{scan.Id}";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {variable});");
            variable = bcast;
        }

        var identity = scan.Identity is { } scanIdentity
            ? ResolveOperand(scanIdentity)
            : null;
        var (storageType, semanticType) = GetCollectiveTypes(scan, type);
        var text = CPUIntrinsicEmitter.EmitWarpScan(
            Reinterpret(variable, storageType, semanticType),
            op,
            scan.Kind,
            type,
            identity);
        context.WriteLine(
            $"{result} = {ReinterpretResult(text, storageType, semanticType)};");
    }

    /// <summary>
    /// Emits a shuffle operation with proper vectorized operand resolution.
    /// Broadcasts scalar origin (e.g., warpIdx) to a per-lane array before
    /// calling the CPU shuffle helper.
    /// </summary>
    public void EmitShuffle(Shuffle shuffle)
    {
        var variable = ResolveOperand(shuffle.Variable);
        var origin = ResolveOperand(shuffle.Origin);
        var result = context.GetValueName(shuffle);
        var type = shuffle.BasicValueType.GetArithmeticBasicValueType(false);

        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var elemType = config.GetPrimitiveTypeName(
            ((PrimitiveType)shuffle.Type).BasicValueType);

        if (analysis.IsScalar(shuffle.Origin))
        {
            // Generic shuffle takes per-lane indices (ReadOnlySpan<int>),
            // so broadcast the scalar to an array. Up/Down/Xor take a
            // scalar int delta — pass through unchanged.
            if (shuffle.Kind == ShuffleKind.Generic)
            {
                var bcast = $"_shfl_org_{shuffle.Id}";
                context.WriteLine(
                    $"var {bcast} = new int[SIMDWidth];");
                context.WriteLine(
                    $"CPUVectorIntrinsics.Broadcast<int>(" +
                    $"{bcast}, {origin});");
                origin = bcast;
            }
        }

        if (analysis.IsScalar(shuffle.Variable))
        {
            var bcast = $"_shfl_val_{shuffle.Id}";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {variable});");
            variable = bcast;
        }

        var text = context.IntrinsicEmitter.EmitShuffle(
            variable, origin, shuffle.Kind, type);
        context.WriteLine($"{result} = {text};");
    }

    /// <summary>
    /// Emits a warp or group broadcast as a shuffle from the origin lane. The
    /// lanes of a call form one warp and, for kernels that synchronize their
    /// group, the whole group, so the origin is a lane of the call.
    /// </summary>
    public void EmitBroadcast(Broadcast broadcast)
    {
        var variable = ResolveOperand(broadcast.Variable);
        var origin = ResolveOperand(broadcast.Origin);
        var result = context.GetValueName(broadcast);
        var type = broadcast.BasicValueType.GetArithmeticBasicValueType(false);

        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var elemType = config.GetPrimitiveTypeName(
            ((PrimitiveType)broadcast.Type).BasicValueType);

        // Group origins are relative to the first lane of the call
        var lanes = $"_bc_org_{broadcast.Id}";
        context.WriteLine($"var {lanes} = new int[SIMDWidth];");
        if (analysis.IsScalar(broadcast.Origin))
        {
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<int>({lanes}, {origin});");
        }
        else
        {
            context.WriteLine($"{origin}.CopyTo({lanes}, 0);");
        }
        if (broadcast.Kind == BroadcastKind.GroupLevel)
        {
            context.WriteLine(
                $"for (int _bi = 0; _bi < SIMDWidth; _bi++) " +
                $"{lanes}[_bi] -= {CPUIntrinsicEmitter.GroupOffsetName};");
        }

        if (analysis.IsScalar(broadcast.Variable))
        {
            var bcast = $"_bc_val_{broadcast.Id}";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {variable});");
            variable = bcast;
        }

        var text = context.IntrinsicEmitter.EmitShuffle(
            variable, lanes, ShuffleKind.Generic, type);
        context.WriteLine($"{result} = {text};");
    }

    /// <summary>
    /// Emits a masked atomic operation (only active lanes perform the operation).
    /// </summary>
    /// <remarks>
    /// The target is a <c>nint[]</c> of per-lane addresses. The active
    /// mask suppresses side effects for inactive lanes.
    /// </remarks>
    public void EmitAtomic(GenericAtomic atomic)
    {
        var valueType = ((PrimitiveType)atomic.Value.Type).BasicValueType;
        if (valueType is BasicValueType.Int8 or BasicValueType.Int16
                       or BasicValueType.Float16)
            throw new System.NotSupportedException(
                $"Sub-word atomic operations ({valueType}) are not supported " +
                $"on the CPU backend. Use 32-bit or wider types for atomics.");

        var target = ResolvePointers(atomic.Target, atomic);
        var value = ResolveOperand(atomic.Value);

        string operation = atomic.Kind switch
        {
            GenericAtomicKind.Add => "Add",
            GenericAtomicKind.Exchange => "Exchange",
            GenericAtomicKind.Min => "Min",
            GenericAtomicKind.Max => "Max",
            GenericAtomicKind.And => "And",
            GenericAtomicKind.Or => "Or",
            GenericAtomicKind.Xor => "Xor",
            _ => throw new System.NotSupportedException(
                $"Atomic {atomic.Kind} not supported")
        };

        var storageType = GetStorageType(valueType);
        var opType = GetSemanticType(atomic.ArithmeticBasicValueType);
        value = BroadcastAtomicOperand(atomic, atomic.Value, value, storageType, "v");
        var result = GetAtomicResult(atomic, storageType);

        // The per-lane old values are written into the value's buffer for the
        // active lanes; unsigned operations reinterpret the buffers.
        context.WriteLine(
            $"CPUAtomicIntrinsics.{operation}(" +
            $"{Reinterpret(result, storageType, opType)}, {target}, " +
            $"{Reinterpret(value, storageType, opType)}, {ActiveMaskName});");
    }

    /// <summary>
    /// Emits a masked atomic compare-and-swap.
    /// </summary>
    public void EmitAtomicCAS(AtomicCAS atomic)
    {
        var target = ResolvePointers(atomic.Target, atomic);
        var storageType = GetStorageType(atomic.Value.BasicValueType);
        var opType = GetSemanticType(atomic.ArithmeticBasicValueType);
        var value = BroadcastAtomicOperand(
            atomic, atomic.Value, ResolveOperand(atomic.Value), storageType, "v");
        var compare = BroadcastAtomicOperand(
            atomic, atomic.Compare, ResolveOperand(atomic.Compare), storageType, "c");
        var result = GetAtomicResult(atomic, storageType);
        context.WriteLine(
            $"CPUAtomicIntrinsics.CompareExchange(" +
            $"{Reinterpret(result, storageType, opType)}, {target}, " +
            $"{Reinterpret(value, storageType, opType)}, " +
            $"{Reinterpret(compare, storageType, opType)}, {ActiveMaskName});");
    }

    /// <summary>
    /// Broadcasts a uniform atomic operand to a per-lane buffer.
    /// </summary>
    private string BroadcastAtomicOperand(
        Value atomic,
        Value operand,
        string expression,
        string storageType,
        string suffix)
    {
        if (!analysis.IsScalar(operand))
            return expression;
        var bcast = $"_atomic_{suffix}_{atomic.Id}";
        context.WriteLine($"var {bcast} = new {storageType}[SIMDWidth];");
        context.WriteLine(
            $"CPUVectorIntrinsics.Broadcast<{storageType}>({bcast}, {expression});");
        return bcast;
    }

    /// <summary>
    /// Returns the buffer receiving the old values of an atomic operation; a
    /// scratch buffer if the old values are not used.
    /// </summary>
    private string GetAtomicResult(Value atomic, string storageType)
    {
        if (context.NeedsVariable(atomic))
            return context.GetValueName(atomic);
        var result = $"_atomic_r_{atomic.Id}";
        context.WriteLine($"var {result} = new {storageType}[SIMDWidth];");
        return result;
    }
}
