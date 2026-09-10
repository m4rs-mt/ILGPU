// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Evaluation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Values;
using System.Reflection;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Evaluates a call of a helper predicate at compile time if all its arguments
    /// are constants, and pushes the constant result instead of emitting a call.
    /// </summary>
    /// <remarks>
    /// Only calls whose result can depend on the closed type arguments of this
    /// instantiation are attempted: calls of generic methods or members of generic
    /// types, calls with a <c>typeof(T)</c> argument, and constrained calls (static
    /// abstract members and struct instance members such as
    /// <c>default(TOp).IsFloat()</c>). Other calls with constant arguments are left
    /// to the optimizer, as before.
    /// </remarks>
    /// <param name="method">The called method.</param>
    /// <param name="arguments">The call arguments.</param>
    /// <param name="isConstrainedCall">
    /// True if the call comes from a constrained call instruction.
    /// </param>
    /// <returns>True if the call was evaluated.</returns>
    private bool TryEvaluateCall(
        MethodBase method,
        ref ValueBuilderList arguments,
        bool isConstrainedCall)
    {
        if (ConstantEvaluator is null ||
            method is not MethodInfo methodInfo ||
            !IsEvaluableReturnType(methodInfo.ReturnType))
        {
            return false;
        }

        bool dependsOnTypes =
            method.IsGenericMethod ||
            method.DeclaringType?.IsGenericType == true ||
            isConstrainedCall;
        int offset = method.IsStatic ? 0 : 1;
        var values = arguments.AsReadOnlySpan();
        var operands = new object?[values.Length];
        for (int i = 0; i < values.Length; ++i)
        {
            var argument = values[i];
            if (i < offset)
            {
                // Only struct instances of constrained calls are supported; they
                // are opaque to the evaluator
                if (!isConstrainedCall || method.DeclaringType?.IsValueType != true)
                    return false;
                operands[i] = null;
            }
            else if (TryGetConstantType(argument, out var type))
            {
                operands[i] = type;
                dependsOnTypes = true;
            }
            else if (argument is PrimitiveValue primitive &&
                TryGetEvaluatorValue(primitive, out var value))
            {
                operands[i] = value;
            }
            else
            {
                return false;
            }
        }
        if (!dependsOnTypes)
            return false;

        var result = ConstantEvaluator.Evaluate(method, operands);
        if (!result.IsConstant)
        {
            _evaluationFailures[method] = result.Reason ?? string.Empty;
            return false;
        }

        var constant = result.Value switch
        {
            System.Type type => Builder.CreateRuntimeHandle(Location, type),
            _ when methodInfo.ReturnType.IsEnum =>
                Builder.CreateEnumValue(Location, result.Value!),
            _ => Builder.CreatePrimitiveValue(Location, result.Value!),
        };
        MakeCallReturnValue(method, constant);
        return true;
    }

    /// <summary>
    /// Returns true if calls returning <paramref name="type"/> can be evaluated:
    /// booleans, integers, floats and enums.
    /// </summary>
    private static bool IsEvaluableReturnType(System.Type type) =>
        type == typeof(System.Type) ||
        type.IsEnum ||
        type.IsPrimitive &&
        type != typeof(char) &&
        type != typeof(nint) &&
        type != typeof(nuint);

    /// <summary>
    /// Converts an IR constant into the evaluator's representation.
    /// </summary>
    private static bool TryGetEvaluatorValue(PrimitiveValue primitive, out object? value)
    {
        value = primitive.BasicValueType switch
        {
            BasicValueType.Int1 => primitive.Int1Value ? 1 : 0,
            BasicValueType.Int8 => (int)primitive.Int8Value,
            BasicValueType.Int16 => (int)primitive.Int16Value,
            BasicValueType.Int32 => primitive.Int32Value,
            BasicValueType.Int64 => primitive.Int64Value,
            BasicValueType.Float32 => primitive.Float32Value,
            BasicValueType.Float64 => primitive.Float64Value,
            _ => null,
        };
        return value is not null;
    }
}
