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
    private string ResolveOperand(Value operand) =>
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
        return "int";
    }

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
            var view = ResolveOperand(lea.Source);
            var index = ResolveOperand(lea.Offset);

            if (analysis.IsScalar(lea.Offset))
            {
                // Use 'var' only for inline declarations; pre-declared
                // variables (e.g., pool redirects) must not re-declare.
                if (context.NeedsVariable(load))
                    context.WriteLine($"{result} = {view}[{index}];");
                else
                    context.WriteLine($"var {result} = {view}[{index}];");
            }
            else
            {
                context.WriteLine(
                    $"{view}.GatherLoad(" +
                    $"{result}, {index}, {ActiveMaskName});");
            }
            return;
        }

        // For pointer LEAs with per-lane base pointers and scalar offsets,
        // compute per-lane offset pointers (same as in EmitStore).
        string source;
        if (load.Source is LoadElementAddress ptrLeaLoad
            && !ptrLeaLoad.IsViewAccess
            && ptrLeaLoad.Source.Type is PointerType
            && !analysis.IsScalar(ptrLeaLoad.Source)
            && analysis.IsScalar(ptrLeaLoad.Offset))
        {
            var ptrSrc = ResolveOperand(ptrLeaLoad.Source);
            var offset = _sharedEmitter.EmitExpression(ptrLeaLoad.Offset);
            var elemSize = ptrLeaLoad.Source.GetTypeAs<PointerType>()
                .ElementType.Size;
            var tmpPtrs = $"_ptroff_{ptrLeaLoad.Id}";
            context.WriteLine($"var {tmpPtrs} = new nint[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.PointerOffset(" +
                $"{tmpPtrs}, {ptrSrc}, {offset} * {elemSize});");
            source = tmpPtrs;
        }
        else
        {
            source = ResolveOperand(load.Source);
        }

        // Pointer-type sources (nint[]) use PointerLoad intrinsics
        // instead of MaskedLoad (which is a CPURuntimeView method).
        if (load.Source.Type is PointerType ptrLoad)
        {
            var elemType = GetTypeNameForCodegen(ptrLoad.ElementType);
            context.WriteLine(
                $"CPUVectorIntrinsics.PointerLoad<{elemType}>(" +
                $"{result}, {source}, {ActiveMaskName});");
        }
        else
        {
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
            var view = ResolveOperand(lea.Source);
            var index = ResolveOperand(lea.Offset);

            if (analysis.IsScalar(lea.Offset))
            {
                // For scalar index into a view, extract lane 0 if value is vectorized
                if (!analysis.IsScalar(store.Value))
                    context.WriteLine($"{view}[{index}] = {value}[0];");
                else
                    context.WriteLine($"{view}[{index}] = {value};");
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

        // For pointer LEAs with per-lane base pointers and scalar offsets,
        // compute per-lane offset pointers via PointerOffset instead of
        // invalid inline arithmetic (nint[] + int).
        string target;
        if (store.Target is LoadElementAddress ptrLea
            && !ptrLea.IsViewAccess
            && ptrLea.Source.Type is PointerType
            && !analysis.IsScalar(ptrLea.Source)
            && analysis.IsScalar(ptrLea.Offset))
        {
            var ptrSource = ResolveOperand(ptrLea.Source);
            var offset = _sharedEmitter.EmitExpression(ptrLea.Offset);
            var elemSize = ptrLea.Source.GetTypeAs<PointerType>().ElementType.Size;
            var tmpPtrs = $"_ptroff_{ptrLea.Id}";
            context.WriteLine($"var {tmpPtrs} = new nint[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.PointerOffset(" +
                $"{tmpPtrs}, {ptrSource}, {offset} * {elemSize});");
            target = tmpPtrs;
        }
        else
        {
            target = ResolveOperand(store.Target);
        }

        // Pointer-type targets (nint[]) use PointerStore intrinsics.
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
    /// Emits a binary arithmetic operation using CPUMathIntrinsics or a plain
    /// C# operator when both operands are scalar.
    /// </summary>
    public void EmitBinaryOp(BinaryArithmeticValue binary)
    {
        var left = ResolveOperand(binary.Left);
        var right = ResolveOperand(binary.Right);
        var result = context.GetValueName(binary);

        bool lScalar = analysis.IsScalar(binary.Left);
        bool rScalar = analysis.IsScalar(binary.Right);

        if (lScalar && rScalar)
        {
            // Operations that map to a C# operator
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
            if (op is not null)
            {
                context.WriteLine($"var {result} = {left} {op} {right};");
                return;
            }

            // Operations that require a method call (scalar path)
            string? scalarMethod = binary.Kind switch
            {
                BinaryArithmeticKind.Max => "System.Math.Max",
                BinaryArithmeticKind.Min => "System.Math.Min",
                _ => null
            };
            if (scalarMethod is not null)
            {
                context.WriteLine(
                    $"var {result} = {scalarMethod}({left}, {right});");
                return;
            }

            throw new System.NotSupportedException(
                $"Binary op {binary.Kind} not supported");
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
            _ => throw new System.NotSupportedException(
                $"Binary op {binary.Kind} not supported")
        };

        // Bool bitwise ops: bool doesn't implement IBitwiseOperators,
        // so use CPUVectorIntrinsics mask helpers instead of
        // CPUMathIntrinsics.BitwiseAnd/Or/Xor.
        if (binary.Type is PrimitiveType pt
            && pt.BasicValueType == BasicValueType.Int1)
        {
            var boolMethod = binary.Kind switch
            {
                BinaryArithmeticKind.And => "AndMask",
                BinaryArithmeticKind.Or => "OrMask",
                _ => null
            };
            if (boolMethod is not null)
            {
                if (lScalar)
                {
                    var bcast = GetBroadcastName(binary);
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<bool>({bcast}, {left});");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.{boolMethod}({result}, " +
                        $"{bcast}, {right});");
                }
                else if (rScalar)
                {
                    var bcast = GetBroadcastName(binary);
                    context.WriteLine(
                        $"CPUVectorIntrinsics.Broadcast<bool>({bcast}, {right});");
                    context.WriteLine(
                        $"CPUVectorIntrinsics.{boolMethod}({result}, " +
                        $"{left}, {bcast});");
                }
                else
                {
                    context.WriteLine(
                        $"CPUVectorIntrinsics.{boolMethod}({result}, " +
                        $"{left}, {right});");
                }
                return;
            }
        }

        // Derive the element type for explicit generic arguments to
        // avoid type inference failures with mixed scalar/vector operands.
        // Shift operations (ShiftLeft/ShiftRight) are non-generic — they
        // have concrete int/long overloads, so skip the <T> suffix.
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var elemType = config.GetPrimitiveTypeName(
            ((PrimitiveType)binary.Type).BasicValueType);
        bool isShift = binary.Kind is BinaryArithmeticKind.Shl
            or BinaryArithmeticKind.Shr;
        var typeArg = isShift ? "" : $"<{elemType}>";

        // For division/remainder, guard inactive lanes in the divisor
        // with 1 to prevent DivideByZeroException on masked-out lanes
        bool isDivRem = binary.Kind is BinaryArithmeticKind.Div
            or BinaryArithmeticKind.Rem;

        if (lScalar)
        {
            var bcast = GetBroadcastName(binary);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {left});");
            if (isDivRem)
                EmitDivGuard(right, elemType);
            context.WriteLine(
                $"CPUMathIntrinsics.{method}{typeArg}(" +
                $"{result}, {bcast}, {right});");
        }
        else if (rScalar)
        {
            var bcast = GetBroadcastName(binary);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {right});");
            // Scalar divisor is safe (broadcast fills all lanes)
            context.WriteLine(
                $"CPUMathIntrinsics.{method}{typeArg}(" +
                $"{result}, {left}, {bcast});");
        }
        else
        {
            if (isDivRem)
                EmitDivGuard(right, elemType);
            context.WriteLine(
                $"CPUMathIntrinsics.{method}{typeArg}(" +
                $"{result}, {left}, {right});");
        }
    }

    /// <summary>
    /// Fills inactive lanes of a divisor array with 1 to prevent
    /// DivideByZeroException on masked-out SIMD lanes.
    /// </summary>
    private void EmitDivGuard(string divisor, string elemType)
    {
        // For each inactive lane, set divisor to 1
        context.WriteLine(
            $"for (int _g = 0; _g < SIMDWidth; _g++) " +
            $"if (!{ActiveMaskName}[_g]) " +
            $"{divisor}[_g] = ({elemType})1;");
    }

    /// <summary>
    /// Emits a unary arithmetic operation using CPUMathIntrinsics or a plain
    /// C# operator when the operand is scalar.
    /// </summary>
    public void EmitUnaryOp(UnaryArithmeticValue unary)
    {
        var operand = ResolveOperand(unary.Value);
        var result = context.GetValueName(unary);

        if (analysis.IsScalar(unary.Value))
        {
            // Scalar unary operations use plain C# operators or Math calls
            var expr = unary.Kind switch
            {
                UnaryArithmeticKind.Neg => $"-{operand}",
                UnaryArithmeticKind.Not => $"~{operand}",
                UnaryArithmeticKind.Abs => $"Math.Abs({operand})",
                UnaryArithmeticKind.Sqrt => $"Math.Sqrt({operand})",
                UnaryArithmeticKind.Sin => $"Math.Sin({operand})",
                UnaryArithmeticKind.Cos => $"Math.Cos({operand})",
                UnaryArithmeticKind.Tan => $"Math.Tan({operand})",
                UnaryArithmeticKind.Asin => $"Math.Asin({operand})",
                UnaryArithmeticKind.Acos => $"Math.Acos({operand})",
                UnaryArithmeticKind.Atan => $"Math.Atan({operand})",
                UnaryArithmeticKind.Sinh => $"Math.Sinh({operand})",
                UnaryArithmeticKind.Cosh => $"Math.Cosh({operand})",
                UnaryArithmeticKind.Tanh => $"Math.Tanh({operand})",
                UnaryArithmeticKind.Asinh => $"Math.Asinh({operand})",
                UnaryArithmeticKind.Acosh => $"Math.Acosh({operand})",
                UnaryArithmeticKind.Atanh => $"Math.Atanh({operand})",
                UnaryArithmeticKind.Exp => $"Math.Exp({operand})",
                UnaryArithmeticKind.Exp2 => $"Math.Pow(2.0, {operand})",
                UnaryArithmeticKind.Log => $"Math.Log({operand})",
                UnaryArithmeticKind.Log2 => $"Math.Log2({operand})",
                UnaryArithmeticKind.Log10 => $"Math.Log10({operand})",
                UnaryArithmeticKind.Floor => $"Math.Floor({operand})",
                UnaryArithmeticKind.Ceiling => $"Math.Ceiling({operand})",
                UnaryArithmeticKind.Rcp => $"(1.0 / {operand})",
                _ => null
            };
            if (expr != null)
            {
                context.WriteLine($"var {result} = {expr};");
                return;
            }
            // Unknown kind — fall through to shared emitter via return false
        }

        // Vectorized unary operations use CPUMathIntrinsics
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var elemType = (unary.Type is PrimitiveType pt)
            ? config.GetPrimitiveTypeName(pt.BasicValueType) : "int";

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
    /// Emits a comparison operation producing a bool[] mask using CPUMathIntrinsics,
    /// or a plain C# operator when both operands are scalar.
    /// </summary>
    public void EmitCompare(CompareValue compare)
    {
        var left = ResolveOperand(compare.Left);
        var right = ResolveOperand(compare.Right);
        var result = context.GetValueName(compare);

        bool lScalar = analysis.IsScalar(compare.Left);
        bool rScalar = analysis.IsScalar(compare.Right);

        if (lScalar && rScalar)
        {
            string op = compare.Kind switch
            {
                CompareKind.Equal => "==",
                CompareKind.NotEqual => "!=",
                CompareKind.LessThan => "<",
                CompareKind.LessEqual => "<=",
                CompareKind.GreaterThan => ">",
                CompareKind.GreaterEqual => ">=",
                _ => throw new System.NotSupportedException(
                    $"Compare {compare.Kind} not supported")
            };
            context.WriteLine($"var {result} = {left} {op} {right};");
            return;
        }

        string method = compare.Kind switch
        {
            CompareKind.Equal => "Equal",
            CompareKind.NotEqual => "NotEqual",
            CompareKind.LessThan => "LessThan",
            CompareKind.LessEqual => "LessEqual",
            CompareKind.GreaterThan => "GreaterThan",
            CompareKind.GreaterEqual => "GreaterEqual",
            _ => throw new System.NotSupportedException(
                $"Compare {compare.Kind} not supported")
        };

        // Detect type mismatch (e.g., int vs long in bounds checks).
        // CPUMathIntrinsics comparison methods require both operands to
        // be the same type T. When they differ, widen the narrower operand.
        var leftPrim = compare.Left.Type as PrimitiveType;
        var rightPrim = compare.Right.Type as PrimitiveType;
        if (leftPrim != null && rightPrim != null
            && leftPrim.BasicValueType != rightPrim.BasicValueType)
        {
            EmitMismatchedCompare(
                left, right, result, lScalar, rScalar,
                method, leftPrim, rightPrim);
            return;
        }

        // Derive the element type for explicit generic arguments
        var cmpConfig = (CPULanguageConfiguration)context.LanguageConfig;
        var cmpElemType = leftPrim != null
            ? cmpConfig.GetPrimitiveTypeName(leftPrim.BasicValueType)
            : "int";

        if (lScalar)
        {
            var bcast = GetBroadcastName(compare);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{cmpElemType}>(" +
                $"{bcast}, {left});");
            context.WriteLine(
                $"CPUMathIntrinsics.{method}<{cmpElemType}>(" +
                $"{result}, {bcast}, {right});");
        }
        else if (rScalar)
        {
            var bcast = GetBroadcastName(compare);
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{cmpElemType}>(" +
                $"{bcast}, {right});");
            context.WriteLine(
                $"CPUMathIntrinsics.{method}<{cmpElemType}>(" +
                $"{result}, {left}, {bcast});");
        }
        else
        {
            context.WriteLine(
                $"CPUMathIntrinsics.{method}<{cmpElemType}>(" +
                $"{result}, {left}, {right});");
        }
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
            context.WriteLine($"var {result} = {scalarHelper}({operand});");
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
            context.WriteLine($"var {result} = {scalarHelper}({operand});");
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
    /// Emits a type conversion operation.
    /// </summary>
    public void EmitConvert(ConvertValue convert)
    {
        var operand = ResolveOperand(convert.Value);
        var result = context.GetValueName(convert);
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var targetType = config.GetPrimitiveTypeName(convert.TargetType);

        if (analysis.IsScalar(convert.Value))
        {
            // For sign-extension from an unsigned C# type to a wider type
            // (e.g., byte→int where the IR says sign-extend), cast through
            // the signed intermediate: (int)(sbyte)byteVal. Without this,
            // (int)byteVal zero-extends (255 stays 255 instead of -1).
            var sourceArith = convert.SourceType;
            var signedSourceType = config.GetPrimitiveTypeName(sourceArith);
            var sourceBvt = convert.Value.BasicValueType;
            var unsignedSourceType = config.GetPrimitiveTypeName(sourceBvt);
            if (!convert.IsSourceUnsigned
                && signedSourceType != unsignedSourceType)
            {
                // The IR source is signed but the C# type is unsigned
                // (e.g., Int8→sbyte but the var is byte). Cast through
                // the signed type to get proper sign extension.
                context.WriteLine(
                    $"var {result} = ({targetType})({signedSourceType})" +
                    $"{operand};");
            }
            else
            {
                context.WriteLine(
                    $"var {result} = ({targetType}){operand};");
            }
        }
        else
        {
            var sourceType = config.GetPrimitiveTypeName(
                ((PrimitiveType)convert.Value.Type).BasicValueType);
            context.WriteLine(
                $"CPUMathIntrinsics.Convert<{sourceType}, {targetType}>(" +
                $"{result}, {operand});");
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

        if (analysis.IsScalar(predicate.Condition))
        {
            // Scalar condition — plain ternary
            context.WriteLine(
                $"var {result} = {condition} ? {trueVal} : {falseVal};");
            return;
        }

        // Vectorized condition — use Select with explicit type arg
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var elemType = predicate.Type is PrimitiveType pt
            ? config.GetPrimitiveTypeName(pt.BasicValueType) : "int";

        // Use the value's unique ID for temporary variable names to avoid
        // collisions when multiple Predicate values reuse the same pool slot.
        var uid = predicate.Id;

        // Ensure true/false values are vectorized (broadcast if scalar)
        if (analysis.IsScalar(predicate.TrueValue))
        {
            var bcast = $"_sel_{uid}_t";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {trueVal});");
            trueVal = bcast;
        }
        if (analysis.IsScalar(predicate.FalseValue))
        {
            var bcast = $"_sel_{uid}_f";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {falseVal});");
            falseVal = bcast;
        }

        context.WriteLine(
            $"CPUVectorIntrinsics.Select<{elemType}>(" +
            $"{result}, {condition}, {trueVal}, {falseVal});");
    }

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

        context.WriteLine(
            $"CPUVectorIntrinsics.ComputeViewAddresses(" +
            $"{result}, {view}, {index}, {elemSize}, {ActiveMaskName});");
    }

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
        var type = reduce.BasicValueType.GetArithmeticBasicValueType(false);

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

        var text = context.IntrinsicEmitter.EmitWarpReduce(
            variable, op, reduce.Kind, type);
        context.WriteLine($"{result} = {text};");
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
        var type = scan.BasicValueType.GetArithmeticBasicValueType(false);

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

        var text = context.IntrinsicEmitter.EmitWarpScan(
            variable, op, scan.Kind, type);
        context.WriteLine($"{result} = {text};");
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

        var target = ResolveOperand(atomic.Target);
        var value = ResolveOperand(atomic.Value);
        var result = context.GetValueName(atomic);

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

        // CPUAtomicIntrinsics methods return T[] (per-lane results).
        // The value argument must be a span — broadcast scalar if needed.
        var config = (CPULanguageConfiguration)context.LanguageConfig;
        var elemType = config.GetPrimitiveTypeName(valueType);

        if (analysis.IsScalar(atomic.Value))
        {
            var bcast = $"_atomic_val_{atomic.Id}";
            context.WriteLine(
                $"var {bcast} = new {elemType}[SIMDWidth];");
            context.WriteLine(
                $"CPUVectorIntrinsics.Broadcast<{elemType}>(" +
                $"{bcast}, {value});");
            value = bcast;
        }

        context.WriteLine(
            $"{result} = CPUAtomicIntrinsics.{operation}(" +
            $"{target}, {value}, {ActiveMaskName});");
    }
}
