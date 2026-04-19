// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ExpressionEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Text;
using Half = ILGPU.Half;

namespace ILGPUC.Backends;

/// <summary>
/// Emits expressions and statements from IR values directly as strings,
/// using inline precedence tracking for minimal parenthesization.
/// </summary>
sealed class ExpressionEmitter(GenerationContext context, TypeEmitter typeEmitter)
{
    /// <summary>
    /// Returns the language-specific intrinsic emitter from the generation context.
    /// </summary>
    private IntrinsicEmitter IntrinsicEmitter => context.IntrinsicEmitter;

    #region Core Data Structures

    /// <summary>
    /// A fully-emitted text fragment with its precedence level for parenthesization.
    /// </summary>
    private readonly record struct Emitted(
        string Text,
        int Precedence,
        bool IsLeftAssociative = true);

    /// <summary>
    /// C-language operator precedence levels for minimal parenthesization.
    /// Higher values bind more tightly.
    /// </summary>
    private static class Prec
    {
        public const int Assignment = 2;
        public const int Ternary = 3;
        public const int LogicalOr = 4;
        public const int LogicalAnd = 5;
        public const int BitwiseOr = 6;
        public const int BitwiseXor = 7;
        public const int BitwiseAnd = 8;
        public const int Equality = 9;
        public const int Relational = 10;
        public const int Shift = 11;
        public const int Additive = 12;
        public const int Multiplicative = 13;
        public const int Unary = 15;
        public const int Postfix = 16;
        public const int Atom = 17;
    }

    #endregion

    #region Parenthesization

    /// <summary>
    /// Returns the text of <paramref name="child"/>, wrapped in parentheses if needed.
    /// </summary>
    private static string Parenthesize(
        Emitted child,
        int parentPrecedence,
        bool isRightChild = false)
    {
        if (child.Precedence >= 16)
            return child.Text;

        if (child.Precedence < parentPrecedence)
            return $"({child.Text})";

        if (child.Precedence == parentPrecedence)
        {
            if (isRightChild && child.IsLeftAssociative)
                return $"({child.Text})";
            if (!isRightChild && !child.IsLeftAssociative)
                return $"({child.Text})";
        }

        return child.Text;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Emits an expression for a value with minimal parentheses.
    /// When the value has a variable, this returns the variable name.
    /// </summary>
    public string EmitExpression(Value value) => Emit(value).Text;

    /// <summary>
    /// Emits the defining expression for a value, bypassing the variable name
    /// shortcut. Used when generating the RHS of a variable assignment.
    /// </summary>
    public string EmitDefinition(Value value) => EmitDirect(value).Text;

    /// <summary>
    /// Emits a fully inlined expression that recursively expands all operands.
    /// Used for loop conditions where variables may be declared inside the body.
    /// </summary>
    public string EmitFullyInlined(Value value)
    {
        _forceInline = true;
        try { return EmitDirect(value).Text; }
        finally { _forceInline = false; }
    }

    private bool _forceInline;

    /// <summary>
    /// Emits a statement for a value that doesn't produce a result.
    /// </summary>
    public string EmitStatement(Value value)
    {
        return value switch
        {
            Store store => EmitStore(store),
            Barrier => $"{IntrinsicEmitter.EmitBarrier()};",
            MemoryBarrier => $"{IntrinsicEmitter.EmitMemoryFence()};",
            MethodCall call when call.Type is VoidType =>
                $"{EmitMethodCall(call).Text};",
            // Atomic operations have side effects and must be emitted even
            // when their return value (the old value) is unused.
            GenericAtomic atomic => $"{EmitAtomicOperation(atomic).Text};",
            AtomicCAS cas => $"{EmitAtomicCAS(cas).Text};",
            _ => string.Empty
        };
    }

    #endregion

    #region Main Dispatch

    /// <summary>
    /// Main dispatch: converts an IR value to an <see cref="Emitted"/> fragment.
    /// </summary>
    private Emitted Emit(Value value)
    {
        // Multi-use values that have a declared variable → use the variable name
        // Skip this shortcut when force-inlining (loop conditions)
        if (!_forceInline
            && context.NeedsVariable(value)
            && value is not (Parameter or Global))
            return new Emitted(context.GetValueName(value), Prec.Atom);

        return EmitDirect(value);
    }

    /// <summary>
    /// Emits the actual expression for a value, without checking for variable names.
    /// Used when generating the definition (RHS) of a variable assignment.
    /// </summary>
    private Emitted EmitDirect(Value value)
    {
        return value switch
        {
            // Constants
            PrimitiveValue primitive => EmitPrimitive(primitive),
            NullValue nullValue => new Emitted(
                context.LanguageConfig.FormatNullLiteral(
                    typeEmitter.GetTypeName(nullValue.Type)), Prec.Atom),
            UndefinedValue undef => EmitUndefined(undef),

            // Device constants
            SubGroupIndexValue => new Emitted(
                IntrinsicEmitter.EmitWarpIdx(0), Prec.Atom),
            SubGroupLaneIndexValue => new Emitted(
                IntrinsicEmitter.EmitLaneIdx(0), Prec.Atom),
            GroupIndexValue => new Emitted(
                IntrinsicEmitter.EmitThreadIdx(0), Prec.Atom),
            GridIndexValue => new Emitted(
                IntrinsicEmitter.EmitBlockIdx(0), Prec.Atom),
            SubGroupDimensionValue => new Emitted(
                IntrinsicEmitter.EmitWarpDim(0), Prec.Atom),
            GroupDimensionValue => new Emitted(
                IntrinsicEmitter.EmitBlockDim(0), Prec.Atom),
            GridDimensionValue => new Emitted(
                IntrinsicEmitter.EmitGridDim(0), Prec.Atom),

            // Parameters and globals
            Parameter param => new Emitted(
                context.GetValueName(param), Prec.Atom),
            Global global => new Emitted(
                context.GetValueName(global), Prec.Atom),

            // Pure values
            BinaryArithmeticValue binary => EmitBinaryArithmetic(binary),
            UnaryArithmeticValue unary => EmitUnaryArithmetic(unary),
            TernaryArithmeticValue ternary => EmitTernaryArithmetic(ternary),
            CompareValue compare => EmitCompare(compare),
            ConvertValue convert => EmitConvert(convert),
            AddressSpaceCast cast => EmitAddressSpaceCast(cast),
            ViewCast viewCast => EmitViewCast(viewCast),
            PointerCast ptrCast => EmitPointerCast(ptrCast),
            PointerAsIntCast ptrCast => EmitPointerAsInt(ptrCast),
            IntAsPointerCast intCast => EmitIntAsPointer(intCast),
            GetViewLength viewLen => EmitGetViewLength(viewLen),
            SubView subView => EmitSubView(subView),
            GetField getField => EmitGetField(getField),
            SetField setField => Emit(setField.Source),
            StructureValue structure => EmitStructure(structure),
            LoadElementAddress lea => EmitLoadElementAddress(lea),
            LoadFieldAddress lfa => EmitLoadFieldAddress(lfa),
            Predicate predicate => EmitPredicate(predicate),
            // NewView from a Global → resolve to the global's CPURuntimeView.
            // The global is declared as a CPURuntimeView<T> backed by stackalloc
            // in CPUMethodEmitter.EmitGlobalDeclarations().
            NewView nv when nv.Pointer is Global g => new Emitted(
                context.GetValueName(g), Prec.Atom),

            // Basic block values
            PhiValue phi => new Emitted(
                context.GetValueName(phi), Prec.Atom),
            MethodCall call => EmitMethodCall(call),
            Load load => EmitLoad(load),
            Alloca alloca => EmitAlloca(alloca),
            GenericAtomic atomic => EmitAtomicOperation(atomic),
            AtomicCAS cas => EmitAtomicCAS(cas),
            Shuffle shuffle => EmitShuffle(shuffle),
            Broadcast broadcast => EmitBroadcast(broadcast),
            WarpReduce reduce => EmitWarpReduce(reduce),
            WarpScan scan => EmitWarpScan(scan),
            GroupReduce => throw new NotSupportedException(
                "GroupReduce must be lowered before codegen"),
            GroupScan => throw new NotSupportedException(
                "GroupScan must be lowered before codegen"),
            WarpRadixSort => throw new NotSupportedException(
                "WarpRadixSort must be lowered before codegen"),
            GroupRadixSort => throw new NotSupportedException(
                "GroupRadixSort must be lowered before codegen"),
            CustomAtomic => throw new NotSupportedException(
                "CustomAtomic must be lowered before codegen"),

            _ => new Emitted(context.GetValueName(value), Prec.Atom)
        };
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Returns the effective address space of a value by looking through
    /// AddressSpaceCast chains to find the original allocation's address space.
    /// This is needed because the codegen strips cross-address-space casts,
    /// so the emitted expression uses the source's address space.
    /// </summary>
    private static MemoryAddressSpace GetEffectiveAddressSpace(Value value)
    {
        // Walk through cast chains to find the real source
        var current = value;
        while (current is AddressSpaceCast asc)
            current = asc.Source;

        if (current.Type is PointerType ptr)
            return ptr.AddressSpace;
        return MemoryAddressSpace.Generic;
    }

    #endregion

    #region Alloca, UndefinedValue, AddressSpaceCast

    /// <summary>
    /// Emits an alloca expression. For C-like GPU backends, returns a
    /// reference to the storage array emitted by <see cref="EmitAllocaDeclaration"/>.
    /// For CPU backend, returns the variable name (CPU handles allocas
    /// separately via stackalloc/CPURuntimeView).
    /// </summary>
    private Emitted EmitAlloca(Alloca alloca)
    {
        var addrKeyword = context.LanguageConfig.GetAddressSpaceKeyword(
            alloca.AddressSpace);
        // CPU backend has empty address space keywords — use plain name
        if (string.IsNullOrEmpty(addrKeyword))
            return new Emitted(context.GetValueName(alloca), Prec.Atom);

        // GPU backends: point to the storage array
        return new Emitted($"{context.GetValueName(alloca)}_storage", Prec.Atom);
    }

    /// <summary>
    /// Emits the full alloca declaration as a local array + pointer.
    /// Called from <see cref="MethodEmitter"/> instead of the default
    /// variable assignment path.
    /// </summary>
    internal string EmitAllocaDeclaration(Alloca alloca)
    {
        var pointerType = alloca.GetTypeAs<PointerType>();
        var elemTypeName = typeEmitter.GetTypeName(pointerType.ElementType);
        var addrSpace = context.LanguageConfig.GetAddressSpaceKeyword(
            pointerType.AddressSpace);
        var varName = context.GetValueName(alloca);
        var storageName = $"{varName}_storage";

        int arraySize = 1;
        if (alloca.ArrayLength.HasValue)
            arraySize = alloca.ArrayLength.Value.Int32Value;

        var sb = new StringBuilder();
        // Emit the storage array
        if (!string.IsNullOrEmpty(addrSpace))
            sb.Append($"{addrSpace} ");
        sb.Append($"{elemTypeName} {storageName}[{arraySize}];");

        // Emit the pointer variable
        sb.Append('\n');
        if (!string.IsNullOrEmpty(addrSpace))
            sb.Append($"{addrSpace} ");
        sb.Append($"{elemTypeName}* {varName} = {storageName};");

        return sb.ToString();
    }

    /// <summary>
    /// Emits an undefined value. For struct types, emits a zero-initialized
    /// struct literal. For scalar types, emits <c>0</c>.
    /// </summary>
    private Emitted EmitUndefined(UndefinedValue undef)
    {
        if (undef.Type is StructureType)
        {
            var typeName = typeEmitter.GetTypeName(undef.Type);
            return new Emitted(
                context.LanguageConfig.FormatDefaultValue(typeName), Prec.Atom);
        }
        return new Emitted("0", Prec.Atom);
    }

    /// <summary>
    /// Emits a pointer element type cast (different element type).
    /// Uses the source pointer's address space to avoid cross-address-space
    /// casts when the IR result type uses Generic (device) but the source
    /// is in thread/threadgroup space.
    /// For CPU: transparent pass-through (no pointer casts in C#).
    /// </summary>
    private Emitted EmitPointerCast(PointerCast cast)
    {
        var source = Emit(cast.Source);
        // Determine address space from source chain, not target
        var addrSpace = GetEffectiveAddressSpace(cast.Source);
        var addrKeyword = context.LanguageConfig.GetAddressSpaceKeyword(addrSpace);

        // CPU: no explicit pointer casts in C#
        if (string.IsNullOrEmpty(addrKeyword))
            return source;

        // Build cast type using source address space + target element type
        var targetPtr = cast.Type as PointerType;
        var elemTypeName = targetPtr is not null
            ? typeEmitter.GetTypeName(targetPtr.ElementType)
            : typeEmitter.GetTypeName(cast.Type);
        var castType = $"{addrKeyword} {elemTypeName}*";

        return new Emitted(
            $"({castType}){Parenthesize(source, Prec.Unary)}",
            Prec.Unary);
    }

    /// <summary>
    /// Emits an address space cast. For GPU backends with explicit address
    /// spaces, emits a C-style cast when safe. For CPU (no address space
    /// keywords), passes through transparently. For Metal, cross-address-space
    /// casts (e.g. threadgroup→device) are invalid and pass through using
    /// the source expression unchanged.
    /// </summary>
    private Emitted EmitAddressSpaceCast(AddressSpaceCast cast)
    {
        var source = Emit(cast.Source);
        var targetPtr = cast.Type as PointerType;
        if (targetPtr is null)
            return source;

        var targetKeyword = context.LanguageConfig.GetAddressSpaceKeyword(
            targetPtr.AddressSpace);
        if (string.IsNullOrEmpty(targetKeyword))
            return source; // CPU: transparent pass-through

        // Check source address space — if different from target, the cast
        // would be between mismatching address spaces (rejected by Metal).
        // Pass through instead and let the pointer be used in its original space.
        var sourcePtr = cast.Source.Type as PointerType;
        if (sourcePtr is not null)
        {
            var sourceKeyword = context.LanguageConfig.GetAddressSpaceKeyword(
                sourcePtr.AddressSpace);
            if (sourceKeyword != targetKeyword)
                return source; // Cross-address-space: pass through
        }

        var targetTypeName = typeEmitter.GetTypeName(cast.Type);
        return new Emitted(
            $"({targetTypeName}){Parenthesize(source, Prec.Unary)}",
            Prec.Unary);
    }

    /// <summary>
    /// Emits a view cast (element type reinterpretation).
    /// For the CPU backend, <c>CPURuntimeView&lt;A&gt;</c> and
    /// <c>CPURuntimeView&lt;B&gt;</c> have identical binary layout
    /// (pointer + length), so <c>Unsafe.BitCast</c> performs the conversion.
    /// GPU backends treat this as a transparent pointer-type change.
    /// </summary>
    private Emitted EmitViewCast(ViewCast viewCast)
    {
        var source = Emit(viewCast.Source);
        var typeEmitter = new TypeEmitter(context);
        var sourceViewType = (ViewType)viewCast.Source.Type;
        var targetViewType = (ViewType)viewCast.Type;

        // GPU backends: view cast is transparent (same pointer, different type)
        if (context.LanguageConfig is not CPU.CPULanguageConfiguration)
            return source;

        // CPU backend: BitCast between CPURuntimeView<A> and CPURuntimeView<B>
        var sourceElem = typeEmitter.GetTypeName(sourceViewType.ElementType);
        var targetElem = typeEmitter.GetTypeName(targetViewType.ElementType);
        if (sourceElem == targetElem)
            return source;

        return new Emitted(
            $"Unsafe.BitCast<CPURuntimeView<{sourceElem}>, " +
            $"CPURuntimeView<{targetElem}>>({source.Text})",
            Prec.Postfix);
    }

    #endregion

    #region Primitive Values

    /// <summary>
    /// Emits a primitive constant value (bool, int, float, etc.).
    /// </summary>
    private Emitted EmitPrimitive(PrimitiveValue primitive)
    {
        var text = primitive.BasicValueType switch
        {
            BasicValueType.Int1 => primitive.Int1Value ? "true" : "false",
            BasicValueType.Int8 => primitive.Int8Value.ToString(),
            BasicValueType.Int16 => primitive.Int16Value.ToString(),
            BasicValueType.Int32 => primitive.Int32Value.ToString(),
            BasicValueType.Int64 => primitive.Int64Value.ToString() + "L",
            BasicValueType.Float16 =>
                IntrinsicEmitter.EmitHalfConstant(primitive.Float16Value.RawValue),
            BasicValueType.Float32 => FormatFloat(primitive.Float32Value) + "f",
            BasicValueType.Float64 => FormatFloat(primitive.Float64Value),
            _ => "0"
        };
        return new Emitted(text, Prec.Atom);
    }

    /// <summary>
    /// Formats a float constant, handling NaN and infinity special cases.
    /// </summary>
    private static string FormatFloat(float value)
    {
        if (float.IsNaN(value)) return "NAN";
        if (float.IsPositiveInfinity(value)) return "INFINITY";
        if (float.IsNegativeInfinity(value)) return "-INFINITY";
        return value.ToString("F6");
    }

    /// <summary>
    /// Formats a double constant, handling NaN and infinity special cases.
    /// </summary>
    private static string FormatFloat(double value)
    {
        if (double.IsNaN(value)) return "NAN";
        if (double.IsPositiveInfinity(value)) return "INFINITY";
        if (double.IsNegativeInfinity(value)) return "-INFINITY";
        return value.ToString("F6");
    }

    #endregion

    #region Binary Arithmetic

    /// <summary>
    /// Emits a binary arithmetic operation as an infix operator or intrinsic call.
    /// </summary>
    private Emitted EmitBinaryArithmetic(BinaryArithmeticValue binary)
    {
        var left = Emit(binary.Left);
        var right = Emit(binary.Right);

        // Intrinsic function calls (Min, Max, Pow, Atan2)
        if (IsBinaryIntrinsic(binary.Kind))
            return EmitBinaryIntrinsicCall(binary.Kind, left, right,
                binary.ArithmeticBasicValueType);

        // Simple operators (+, -, *, /, etc.)
        var simpleOp = GetBinaryOperator(binary.Kind);
        if (simpleOp is not null)
        {
            // For sign-sensitive operators (>>, /, %), cast operands to the
            // correct unsigned type when the operation is flagged unsigned
            if (binary.IsUnsigned && IsSignSensitiveOperator(binary.Kind))
            {
                left = EmitUnsignedCast(left, binary.ArithmeticBasicValueType);
                right = EmitUnsignedCast(right, binary.ArithmeticBasicValueType);
            }

            var prec = GetOperatorPrecedence(simpleOp);
            var l = Parenthesize(left, prec);
            var r = Parenthesize(right, prec, isRightChild: true);
            return new Emitted($"{l} {simpleOp} {r}", prec);
        }

        // Special cases
        return EmitNonIntrinsicBinaryOp(binary.Kind, left, right,
            binary.ArithmeticBasicValueType);
    }

    /// <summary>
    /// Returns true if the binary operation maps to a language intrinsic function call.
    /// </summary>
    private static bool IsBinaryIntrinsic(BinaryArithmeticKind kind) => kind switch
    {
        BinaryArithmeticKind.Min => true,
        BinaryArithmeticKind.Max => true,
        BinaryArithmeticKind.Pow => true,
        BinaryArithmeticKind.Atan2 => true,
        _ => false
    };

    /// <summary>
    /// Emits a binary intrinsic as a function call (e.g., min, max, pow, atan2).
    /// </summary>
    private Emitted EmitBinaryIntrinsicCall(
        BinaryArithmeticKind kind,
        Emitted left,
        Emitted right,
        ArithmeticBasicValueType basicType)
    {
        var text = kind switch
        {
            BinaryArithmeticKind.Min =>
                IntrinsicEmitter.EmitMin(left.Text, right.Text, basicType),
            BinaryArithmeticKind.Max =>
                IntrinsicEmitter.EmitMax(left.Text, right.Text, basicType),
            BinaryArithmeticKind.Pow =>
                IntrinsicEmitter.EmitPow(left.Text, right.Text, basicType),
            BinaryArithmeticKind.Atan2 =>
                IntrinsicEmitter.EmitAtan2(left.Text, right.Text, basicType),
            _ => $"/* Unknown binary intrinsic: {kind} */"
        };
        return new Emitted(text, Prec.Postfix);
    }

    /// <summary>
    /// Returns the infix operator string for a binary arithmetic kind, or null if not a simple operator.
    /// </summary>
    private static string? GetBinaryOperator(BinaryArithmeticKind kind) => kind switch
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

    /// <summary>
    /// Returns true for operators whose behavior differs between signed and unsigned.
    /// </summary>
    private static bool IsSignSensitiveOperator(BinaryArithmeticKind kind) => kind switch
    {
        BinaryArithmeticKind.Shr => true,  // Arithmetic vs logical shift
        BinaryArithmeticKind.Div => true,  // Signed vs unsigned division
        BinaryArithmeticKind.Rem => true,  // Signed vs unsigned remainder
        _ => false
    };

    /// <summary>
    /// Wraps an expression with a cast to the unsigned type.
    /// </summary>
    private Emitted EmitUnsignedCast(Emitted operand, ArithmeticBasicValueType type)
    {
        var typeName = typeEmitter.GetTypeName(type);
        var inner = Parenthesize(operand, Prec.Unary);
        return new Emitted($"({typeName}){inner}", Prec.Unary);
    }

    /// <summary>
    /// Returns the precedence level for a given operator string.
    /// </summary>
    private static int GetOperatorPrecedence(string op) => op switch
    {
        "*" or "/" or "%" => Prec.Multiplicative,
        "+" or "-" => Prec.Additive,
        "<<" or ">>" => Prec.Shift,
        "<" or "<=" or ">" or ">=" => Prec.Relational,
        "==" or "!=" => Prec.Equality,
        "&" => Prec.BitwiseAnd,
        "^" => Prec.BitwiseXor,
        "|" => Prec.BitwiseOr,
        "&&" => Prec.LogicalAnd,
        "||" => Prec.LogicalOr,
        "=" => Prec.Assignment,
        _ => 0
    };

    /// <summary>
    /// Emits binary operations that are neither simple operators nor intrinsics
    /// (e.g., IEEERemainder, CopySign, BinaryLog).
    /// </summary>
    private static Emitted EmitNonIntrinsicBinaryOp(
        BinaryArithmeticKind kind,
        Emitted left,
        Emitted right,
        ArithmeticBasicValueType basicType)
    {
        // Direct function calls (remainder, copysign)
        var operation = kind switch
        {
            BinaryArithmeticKind.IEEERemainder => "remainder",
            BinaryArithmeticKind.CopySign => "copysign",
            _ => null
        };

        if (operation is not null)
            return new Emitted($"{operation}({left.Text}, {right.Text})", Prec.Postfix);

        // BinaryLog: log(left) / log(right)
        if (kind == BinaryArithmeticKind.BinaryLog)
        {
            var logLeft = new Emitted($"log({left.Text})", Prec.Postfix);
            var logRight = new Emitted($"log({right.Text})", Prec.Postfix);
            var l = Parenthesize(logLeft, Prec.Multiplicative);
            var r = Parenthesize(logRight, Prec.Multiplicative, isRightChild: true);
            return new Emitted($"{l} / {r}", Prec.Multiplicative);
        }

        return new Emitted($"/* Unknown binary op: {kind} */", Prec.Atom);
    }

    #endregion

    #region Unary Arithmetic

    /// <summary>
    /// Emits a unary arithmetic operation as a prefix operator or intrinsic call.
    /// </summary>
    private Emitted EmitUnaryArithmetic(UnaryArithmeticValue unary)
    {
        var operand = Emit(unary.Value);

        // Simple operators (-, ~)
        var simpleOp = GetUnaryOperator(unary.Kind);
        if (simpleOp is not null)
        {
            var inner = Parenthesize(operand, Prec.Unary);
            return new Emitted($"{simpleOp}{inner}", Prec.Unary);
        }

        // Intrinsic function calls (Sin, Cos, Sqrt, etc.)
        if (IsUnaryIntrinsic(unary.Kind))
            return EmitUnaryIntrinsicCall(unary.Kind, operand,
                unary.ArithmeticBasicValueType);

        // Non-intrinsic special cases
        return EmitNonIntrinsicUnaryOp(unary.Kind, operand,
            unary.ArithmeticBasicValueType);
    }

    /// <summary>
    /// Returns the prefix operator string for a unary arithmetic kind, or null if not a simple operator.
    /// </summary>
    private static string? GetUnaryOperator(UnaryArithmeticKind kind) => kind switch
    {
        UnaryArithmeticKind.Neg => "-",
        UnaryArithmeticKind.Not => "~",
        _ => null
    };

    /// <summary>
    /// Returns true if the unary operation maps to a language intrinsic function call.
    /// </summary>
    private static bool IsUnaryIntrinsic(UnaryArithmeticKind kind) => kind switch
    {
        UnaryArithmeticKind.Sin => true,
        UnaryArithmeticKind.Cos => true,
        UnaryArithmeticKind.Tan => true,
        UnaryArithmeticKind.Asin => true,
        UnaryArithmeticKind.Acos => true,
        UnaryArithmeticKind.Atan => true,
        UnaryArithmeticKind.Sqrt => true,
        UnaryArithmeticKind.Rsqrt => true,
        UnaryArithmeticKind.Exp => true,
        UnaryArithmeticKind.Exp2 => true,
        UnaryArithmeticKind.Log => true,
        UnaryArithmeticKind.Log2 => true,
        UnaryArithmeticKind.Log10 => true,
        UnaryArithmeticKind.Abs => true,
        UnaryArithmeticKind.Floor => true,
        UnaryArithmeticKind.Ceiling => true,
        _ => false
    };

    /// <summary>
    /// Emits a unary intrinsic as a function call (e.g., sin, cos, sqrt, abs).
    /// </summary>
    private Emitted EmitUnaryIntrinsicCall(
        UnaryArithmeticKind kind,
        Emitted operand,
        ArithmeticBasicValueType basicType)
    {
        var arg = operand.Text;
        var text = kind switch
        {
            UnaryArithmeticKind.Sin => IntrinsicEmitter.EmitSin(arg, basicType),
            UnaryArithmeticKind.Cos => IntrinsicEmitter.EmitCos(arg, basicType),
            UnaryArithmeticKind.Tan => IntrinsicEmitter.EmitTan(arg, basicType),
            UnaryArithmeticKind.Asin => IntrinsicEmitter.EmitAsin(arg, basicType),
            UnaryArithmeticKind.Acos => IntrinsicEmitter.EmitAcos(arg, basicType),
            UnaryArithmeticKind.Atan => IntrinsicEmitter.EmitAtan(arg, basicType),
            UnaryArithmeticKind.Sqrt => IntrinsicEmitter.EmitSqrt(arg, basicType),
            UnaryArithmeticKind.Rsqrt => IntrinsicEmitter.EmitRsqrt(arg, basicType),
            UnaryArithmeticKind.Exp => IntrinsicEmitter.EmitExp(arg, basicType),
            UnaryArithmeticKind.Exp2 => IntrinsicEmitter.EmitExp2(arg, basicType),
            UnaryArithmeticKind.Log => IntrinsicEmitter.EmitLog(arg, basicType),
            UnaryArithmeticKind.Log2 => IntrinsicEmitter.EmitLog2(arg, basicType),
            UnaryArithmeticKind.Log10 => IntrinsicEmitter.EmitLog10(arg, basicType),
            UnaryArithmeticKind.Abs => IntrinsicEmitter.EmitAbs(arg, basicType),
            UnaryArithmeticKind.Floor => IntrinsicEmitter.EmitFloor(arg, basicType),
            UnaryArithmeticKind.Ceiling => IntrinsicEmitter.EmitCeil(arg, basicType),
            _ => $"/* Unknown unary intrinsic: {kind} */"
        };
        return new Emitted(text, Prec.Postfix);
    }

    /// <summary>
    /// Emits unary operations that are neither simple operators nor standard intrinsics
    /// (e.g., reciprocal, hyperbolic functions, bit-counting operations).
    /// </summary>
    private Emitted EmitNonIntrinsicUnaryOp(
        UnaryArithmeticKind kind,
        Emitted operand,
        ArithmeticBasicValueType basicType)
    {
        // Reciprocal: 1.0(f) / x
        if (kind == UnaryArithmeticKind.Rcp)
        {
            var one = basicType switch
            {
                ArithmeticBasicValueType.Float64 => "1.0",
                ArithmeticBasicValueType.Float32 => "1.0f",
                _ => IntrinsicEmitter.EmitHalfConstant(Half.One.RawValue)
            };
            var oneEmitted = new Emitted(one, Prec.Atom);
            var l = Parenthesize(oneEmitted, Prec.Multiplicative);
            var r = Parenthesize(operand, Prec.Multiplicative, isRightChild: true);
            return new Emitted($"{l} / {r}", Prec.Multiplicative);
        }

        // Function-call-style operations
        var operation = kind switch
        {
            UnaryArithmeticKind.Sinh => "sinh",
            UnaryArithmeticKind.Cosh => "cosh",
            UnaryArithmeticKind.Tanh => "tanh",
            UnaryArithmeticKind.Asinh => "asinh",
            UnaryArithmeticKind.Acosh => "acosh",
            UnaryArithmeticKind.Atanh => "atanh",
            UnaryArithmeticKind.IsNaN => "isnan",
            UnaryArithmeticKind.IsInf => "isinf",
            UnaryArithmeticKind.IsFin => "isfinite",
            UnaryArithmeticKind.PopC => "__popc",
            UnaryArithmeticKind.CLZ => "__clz",
            UnaryArithmeticKind.CTZ => "__ctz",
            _ => null
        };

        if (operation is not null)
            return new Emitted($"{operation}({operand.Text})", Prec.Postfix);

        return new Emitted($"/* Unknown unary op: {kind} */", Prec.Atom);
    }

    #endregion

    #region Ternary Arithmetic

    /// <summary>
    /// Emits a ternary arithmetic operation (e.g., multiply-add / FMA).
    /// </summary>
    private Emitted EmitTernaryArithmetic(TernaryArithmeticValue ternary)
    {
        var first = Emit(ternary.First);
        var second = Emit(ternary.Second);
        var third = Emit(ternary.Third);

        if (ternary.Kind == TernaryArithmeticKind.MultiplyAdd)
        {
            var text = IntrinsicEmitter.EmitMultiplyAdd(
                first.Text, second.Text, third.Text,
                ternary.ArithmeticBasicValueType);
            return new Emitted(text, Prec.Postfix);
        }

        return new Emitted($"/* Unknown ternary op: {ternary.Kind} */", Prec.Atom);
    }

    #endregion

    #region Comparison

    /// <summary>
    /// Emits a comparison operation with proper unsigned casting when needed.
    /// </summary>
    private Emitted EmitCompare(CompareValue compare)
    {
        var left = Emit(compare.Left);
        var right = Emit(compare.Right);
        var op = GetCompareOperator(compare.Kind);
        var prec = GetOperatorPrecedence(op);

        // For unsigned/unordered comparisons with ordering operators,
        // cast operands to the unsigned type so <, >, <=, >= work correctly
        if (compare.IsUnsignedOrUnordered && IsOrderingCompare(compare.Kind))
        {
            left = EmitUnsignedCast(left, compare.CompareType);
            right = EmitUnsignedCast(right, compare.CompareType);
        }

        var l = Parenthesize(left, prec);
        var r = Parenthesize(right, prec, isRightChild: true);
        return new Emitted($"{l} {op} {r}", prec);
    }

    /// <summary>
    /// Returns true for comparison operators where signedness matters.
    /// </summary>
    private static bool IsOrderingCompare(CompareKind kind) => kind switch
    {
        CompareKind.LessThan or CompareKind.LessEqual or
        CompareKind.GreaterThan or CompareKind.GreaterEqual => true,
        _ => false
    };

    /// <summary>
    /// Returns the comparison operator string for a given compare kind.
    /// </summary>
    private static string GetCompareOperator(CompareKind kind) => kind switch
    {
        CompareKind.Equal => "==",
        CompareKind.NotEqual => "!=",
        CompareKind.LessThan => "<",
        CompareKind.LessEqual => "<=",
        CompareKind.GreaterThan => ">",
        CompareKind.GreaterEqual => ">=",
        _ => "?"
    };

    #endregion

    #region Convert / Cast

    /// <summary>
    /// Emits a type conversion as a C-style cast expression.
    /// </summary>
    private Emitted EmitConvert(ConvertValue convert)
    {
        var operand = Emit(convert.Value);
        var targetType = typeEmitter.GetTypeName(convert.TargetType);
        var inner = Parenthesize(operand, Prec.Unary);

        // For sign-extending casts where the C# source type is unsigned
        // (e.g., byte) but the IR source semantics are signed (Int8),
        // insert an intermediate cast through the signed type so that
        // (int)(sbyte)byteVal sign-extends instead of zero-extending.
        if (!convert.IsSourceUnsigned
            && convert.Value.Type is PrimitiveType srcPrimSigned)
        {
            var signedSrc = context.LanguageConfig.GetPrimitiveTypeName(
                convert.SourceType);
            var unsignedSrc = context.LanguageConfig.GetPrimitiveTypeName(
                srcPrimSigned.BasicValueType);
            if (signedSrc != unsignedSrc)
                return new Emitted(
                    $"({targetType})({signedSrc}){inner}", Prec.Unary);
        }

        // For zero-extending casts where the source is unsigned but the
        // declared type is signed (e.g., byte param declared as char in Metal),
        // insert an intermediate cast through the unsigned type so that
        // (int)(uchar)charVal zero-extends instead of sign-extending.
        if (convert.IsSourceUnsigned
            && convert.Value.Type is PrimitiveType srcPrimUnsigned)
        {
            var unsignedSrc = context.LanguageConfig.GetPrimitiveTypeName(
                convert.SourceType);
            var defaultSrc = context.LanguageConfig.GetPrimitiveTypeName(
                srcPrimUnsigned.BasicValueType);
            if (unsignedSrc != defaultSrc)
                return new Emitted(
                    $"({targetType})({unsignedSrc}){inner}", Prec.Unary);
        }

        return new Emitted($"({targetType}){inner}", Prec.Unary);
    }

    /// <summary>
    /// Emits a pointer-to-integer cast.
    /// GPU backends: <c>(long)ptr</c>. CPU backend: the pointer value is
    /// already in an <c>nint[]</c> or <c>long[]</c> variable.
    /// </summary>
    private Emitted EmitPointerAsInt(PointerAsIntCast cast)
    {
        var source = Emit(cast.Source);
        var targetType = typeEmitter.GetTypeName(cast.TargetType);
        var inner = Parenthesize(source, Prec.Unary);
        return new Emitted($"({targetType}){inner}", Prec.Unary);
    }

    /// <summary>
    /// Emits an integer-to-pointer cast.
    /// </summary>
    private Emitted EmitIntAsPointer(IntAsPointerCast cast)
    {
        var source = Emit(cast.Source);
        var inner = Parenthesize(source, Prec.Unary);
        return new Emitted($"(nint){inner}", Prec.Unary);
    }

    #endregion

    #region View Properties

    /// <summary>
    /// Emits a view length access. After view lowering, the view is a struct
    /// where <c>Field1</c> is the length (a <c>long</c>).
    /// </summary>
    private Emitted EmitGetViewLength(GetViewLength viewLen)
    {
        var source = Emit(viewLen.Source);
        var inner = Parenthesize(source, Prec.Atom);
        var fieldName = context.LanguageConfig.ViewLengthFieldName;
        return new Emitted($"{inner}.{fieldName}", Prec.Atom);
    }

    /// <summary>
    /// Emits a sub-view operation. For the CPU backend this produces
    /// <c>view.SubView(offset, length)</c>; for GPU backends the view
    /// is already lowered to a struct, so this path is only reached
    /// when <c>SupportsViews</c> is true.
    /// </summary>
    private Emitted EmitSubView(SubView subView)
    {
        var source = Emit(subView.Source);
        var offset = Emit(subView.Offset);
        var length = Emit(subView.Length);
        var inner = Parenthesize(source, Prec.Atom);
        return new Emitted(
            $"{inner}.SubView({offset.Text}, {length.Text})", Prec.Atom);
    }

    #endregion

    #region Fields and Structures

    /// <summary>
    /// Emits a struct field access as a dot expression (e.g., "source.field_0").
    /// </summary>
    private Emitted EmitGetField(GetField getField)
    {
        // Fold: GetField(StructureValue{a, b, ...}, N) → element N.
        // This avoids chained field accesses like `param.Field1.Field1`
        // when a struct-returning method was inlined but the optimizer
        // didn't simplify the GetField+StructureValue pair.
        if (getField.Source is StructureValue sv
            && getField.FieldSpan.Index < sv.Count)
        {
            return Emit(sv.Values[getField.FieldSpan.Index]);
        }

        // Fold: GetField(GetField(source, i, Span: N), j) → GetField(source, i+j)
        // The IR uses field spans to extract sub-structs from flat structs.
        // E.g., aView[1, Span:2] extracts {Extent.X, Extent.Y} as a sub-struct,
        // then [1] on that gets Extent.Y. Flatten to aView[2] = Field2.
        if (getField.Source is GetField innerGf
            && innerGf.FieldSpan.Span > 1)
        {
            var flatSpan = innerGf.FieldSpan.Narrow(getField.FieldSpan);
            var flatSource = Emit(innerGf.Source);
            var flatFieldName = StructureType.GetFieldName(flatSpan.Index);
            var flatTarget = Parenthesize(flatSource, Prec.Postfix);
            return new Emitted($"{flatTarget}.{flatFieldName}", Prec.Postfix);
        }

        var source = Emit(getField.Source);
        var fieldName = StructureType.GetFieldName(getField.FieldSpan.Index);
        var target = Parenthesize(source, Prec.Postfix);
        return new Emitted($"{target}.{fieldName}", Prec.Postfix);
    }

    /// <summary>
    /// Emits a load field address as a pointer to a struct field.
    /// For GPU backends: <c>(addrspace FieldType*)(source) + fieldIndex</c>.
    /// Uses the source pointer's address space (not the IR result type's)
    /// to avoid cross-address-space casts that Metal rejects.
    /// For CPU: falls back to variable name (CPU handles LFA separately).
    /// </summary>
    private Emitted EmitLoadFieldAddress(LoadFieldAddress lfa)
    {
        // CPU emits LoadFieldAddress as a pre-declared variable
        // (CPUMethodEmitter.cs: vectorized via OffsetPointers, scalar via
        // a regular pointer var). All GPU backends inline as a C-style
        // pointer cast.
        if (context.LanguageConfig.LoadFieldAddressUsesVariable)
            return new Emitted(context.GetValueName(lfa), Prec.Atom);

        // Determine the effective source address space by looking through
        // AddressSpaceCast chains to find the original allocation space.
        var addrSpace = GetEffectiveAddressSpace(lfa.Source);
        var addrKeyword = context.LanguageConfig.GetAddressSpaceKeyword(addrSpace);

        var source = Emit(lfa.Source);
        // Build cast type using source address space + field element type.
        // CUDA omits the address-space qualifier (pointer types in CUDA C++
        // don't carry one); Metal/OpenCL prepend their qualifier.
        var elemTypeName = typeEmitter.GetTypeName(lfa.FieldType);
        var castType = string.IsNullOrEmpty(addrKeyword)
            ? $"{elemTypeName}*"
            : $"{addrKeyword} {elemTypeName}*";

        var inner = Parenthesize(source, Prec.Unary);
        if (lfa.FieldSpan.Index == 0)
            return new Emitted($"({castType}){inner}", Prec.Unary);
        return new Emitted(
            $"({castType}){inner} + {lfa.FieldSpan.Index}", Prec.Additive);
    }

    /// <summary>
    /// Emits a struct literal. GPU backends use C-style compound literals
    /// <c>(type){ a, b, c }</c>; CPU uses C# object initializer syntax
    /// <c>new type { Field0 = a, Field1 = b }</c>.
    /// </summary>
    private Emitted EmitStructure(StructureValue structure)
    {
        var sb = new StringBuilder();
        var typeName = typeEmitter.GetTypeName(structure.Type);

        if (context.LanguageConfig.UsesCStyleStructLiterals)
        {
            // C-style compound literal: (TypeName){ val0, val1, ... }
            sb.Append($"({typeName}){{ ");
            for (int i = 0; i < structure.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(Emit(structure.Values[i]).Text);
            }
            sb.Append(" }");
        }
        else
        {
            // C# object initializer: new TypeName { Field0 = val0, ... }
            sb.Append($"new {typeName} {{ ");
            for (int i = 0; i < structure.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                var fieldName = StructureType.GetFieldName(i);
                sb.Append($"{fieldName} = ");
                sb.Append(Emit(structure.Values[i]).Text);
            }
            sb.Append(" }");
        }

        return new Emitted(sb.ToString(), Prec.Postfix);
    }

    #endregion

    #region Memory Operations

    /// <summary>
    /// Emits a load-element-address as pointer arithmetic (e.g., "source + index").
    /// LEA computes the *address* of an element, not the element value itself.
    /// A subsequent Load will dereference with *, giving *(ptr + idx) = ptr[idx].
    /// </summary>
    private Emitted EmitLoadElementAddress(LoadElementAddress lea)
    {
        var source = Emit(lea.Source);
        var index = Emit(lea.Offset);
        var left = Parenthesize(source, Prec.Additive);
        var right = Parenthesize(index, Prec.Additive, isRightChild: true);
        return new Emitted($"{left} + {right}", Prec.Additive);
    }

    /// <summary>
    /// Emits a conditional predicate as a ternary expression
    /// (e.g., "condition ? trueValue : falseValue").
    /// </summary>
    private Emitted EmitPredicate(Predicate predicate)
    {
        var condition = Emit(predicate.Condition);
        var trueValue = Emit(predicate.TrueValue);
        var falseValue = Emit(predicate.FalseValue);
        var condText = Parenthesize(condition, Prec.Ternary);
        var trueText = Parenthesize(trueValue, Prec.Ternary);
        var falseText = Parenthesize(falseValue, Prec.Ternary, isRightChild: true);
        return new Emitted($"{condText} ? {trueText} : {falseText}", Prec.Ternary);
    }

    /// <summary>
    /// Emits a method call expression with all arguments.
    /// </summary>
    private Emitted EmitMethodCall(MethodCall call)
    {
        var funcName = context.GetValueName(call.Target);
        var sb = new StringBuilder();
        sb.Append(funcName);
        sb.Append('(');
        var arguments = call.Arguments;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(Emit(arguments[i]).Text);
        }
        sb.Append(')');
        return new Emitted(sb.ToString(), Prec.Postfix);
    }

    /// <summary>
    /// Emits a pointer dereference (e.g., "*address").
    /// </summary>
    private Emitted EmitLoad(Load load)
    {
        var address = Emit(load.Source);
        var inner = Parenthesize(address, Prec.Unary);
        return new Emitted($"*{inner}", Prec.Unary);
    }

    /// <summary>
    /// Emits a store statement as a pointer dereference assignment
    /// (e.g., "*target = value;").
    /// </summary>
    private string EmitStore(Store store)
    {
        var target = Emit(store.Target);
        var value = Emit(store.Value);
        var deref = Parenthesize(target, Prec.Unary);
        return $"*{deref} = {value.Text};";
    }

    #endregion

    #region Atomic Operations

    /// <summary>
    /// Emits a generic atomic operation via language-specific intrinsics.
    /// </summary>
    private Emitted EmitAtomicOperation(GenericAtomic atomic)
    {
        var ptr = Emit(atomic.Target).Text;
        var value = Emit(atomic.Value).Text;
        var type = atomic.ArithmeticBasicValueType;

        var text = atomic.Kind switch
        {
            GenericAtomicKind.Add =>
                IntrinsicEmitter.EmitAtomicAdd(ptr, value, type),
            GenericAtomicKind.Exchange =>
                IntrinsicEmitter.EmitAtomicExchange(ptr, value, type),
            GenericAtomicKind.Min =>
                IntrinsicEmitter.EmitAtomicMin(ptr, value, type),
            GenericAtomicKind.Max =>
                IntrinsicEmitter.EmitAtomicMax(ptr, value, type),
            GenericAtomicKind.And =>
                IntrinsicEmitter.EmitAtomicAnd(ptr, value, type),
            GenericAtomicKind.Or =>
                IntrinsicEmitter.EmitAtomicOr(ptr, value, type),
            GenericAtomicKind.Xor =>
                IntrinsicEmitter.EmitAtomicXor(ptr, value, type),
            _ => $"/* Unknown atomic: {atomic.Kind} */"
        };
        return new Emitted(text, Prec.Postfix);
    }

    /// <summary>
    /// Emits an atomic compare-and-swap operation via language-specific intrinsics.
    /// </summary>
    private Emitted EmitAtomicCAS(AtomicCAS cas)
    {
        var ptr = Emit(cas.Target).Text;
        var compare = Emit(cas.Compare).Text;
        var value = Emit(cas.Value).Text;

        var text = IntrinsicEmitter.EmitAtomicCAS(
            ptr, compare, value, cas.ArithmeticBasicValueType);
        return new Emitted(text, Prec.Postfix);
    }

    #endregion

    #region Shuffle and Broadcast

    /// <summary>
    /// Emits a warp shuffle operation via language-specific intrinsics.
    /// </summary>
    private Emitted EmitShuffle(Shuffle shuffle)
    {
        var variable = Emit(shuffle.Variable).Text;
        var origin = Emit(shuffle.Origin).Text;

        var text = IntrinsicEmitter.EmitShuffle(
            variable, origin, shuffle.Kind,
            shuffle.BasicValueType.GetArithmeticBasicValueType(false));
        return new Emitted(text, Prec.Postfix);
    }

    /// <summary>
    /// Emits a warp broadcast operation via language-specific intrinsics.
    /// </summary>
    private Emitted EmitBroadcast(Broadcast broadcast)
    {
        var variable = Emit(broadcast.Variable).Text;
        var origin = Emit(broadcast.Origin).Text;

        var text = IntrinsicEmitter.EmitBroadcast(
            variable, origin, broadcast.Kind,
            broadcast.BasicValueType.GetArithmeticBasicValueType(false));
        return new Emitted(text, Prec.Postfix);
    }

    /// <summary>
    /// Emits a warp reduce operation via language-specific intrinsics.
    /// Only called for intrinsic operations on backends with native support;
    /// custom operations are lowered to shuffles before reaching codegen.
    /// </summary>
    private Emitted EmitWarpReduce(WarpReduce reduce)
    {
        var variable = Emit(reduce.Variable).Text;
        var text = IntrinsicEmitter.EmitWarpReduce(
            variable,
            reduce.IntrinsicOp
                ?? throw new NotSupportedException(
                    "Custom warp reduce operations must be lowered before codegen"),
            reduce.Kind,
            reduce.BasicValueType.GetArithmeticBasicValueType(false));
        return new Emitted(text, Prec.Postfix);
    }

    /// <summary>
    /// Emits a warp scan operation via language-specific intrinsics.
    /// Only called for intrinsic operations on backends with native support;
    /// custom operations are lowered to shuffles before reaching codegen.
    /// </summary>
    private Emitted EmitWarpScan(WarpScan scan)
    {
        var variable = Emit(scan.Variable).Text;
        var text = IntrinsicEmitter.EmitWarpScan(
            variable,
            scan.IntrinsicOp
                ?? throw new NotSupportedException(
                    "Custom warp scan operations must be lowered before codegen"),
            scan.Kind,
            scan.BasicValueType.GetArithmeticBasicValueType(false));
        return new Emitted(text, Prec.Postfix);
    }

    #endregion

    #region Statement Helpers

    #endregion
}
