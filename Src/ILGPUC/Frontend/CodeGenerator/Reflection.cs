// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Reflection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Folds a call to a reflection member that can be evaluated at compile time
    /// (see <see cref="ReflectionFolding"/>), and rejects calls to all other
    /// reflection members with a clear error.
    /// </summary>
    /// <param name="method">The called method.</param>
    /// <returns>
    /// <see langword="true"/> if the call was folded and its result pushed.
    /// </returns>
    private bool TryMakeReflectionCall(MethodBase method)
    {
        if (!ReflectionFolding.IsFolded(method))
        {
            if (ReflectionFolding.IsUnsupportedReflection(method))
            {
                throw Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedReflectionMember,
                    FrontendDisplay.Format(method));
            }
            return false;
        }

        // Pop all operands, the instance first after reversal
        int count = ReflectionFolding.GetOperandCount(method);
        var operands = new object?[count];
        for (int i = count - 1; i >= 0; --i)
        {
            var value = Block.Pop();
            if (TryGetConstantType(value, out var type))
            {
                operands[i] = type;
            }
            else if (!ReflectionFolding.IsTypeOperand(method, i) &&
                value.Type is not HandleType)
            {
                // Equals(object) with a value that is not a type is false
                operands[i] = value;
            }
            else
            {
                throw Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedNonConstantType,
                    FrontendDisplay.Format(method));
            }
        }

        var result = ReflectionFolding.Evaluate(
            method,
            operands,
            ModuleBuilder.TargetPlatform.Is64Bit());
        switch (result)
        {
            case Type resultType:
                Block.Push(Builder.CreateRuntimeHandle(Location, resultType));
                break;
            case bool boolean:
                Block.Push(Builder.CreatePrimitiveValue(Location, boolean));
                break;
            case int integer:
                Block.Push(Builder.CreatePrimitiveValue(Location, integer));
                break;
            default:
                throw Location.GetInvalidOperationException();
        }
        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a type that is
    /// known at compile time: a <c>typeof(T)</c> handle, or a phi whose incoming
    /// values are all the same type (as Debug builds produce when a type travels
    /// through a local or across a merge).
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <param name="type">The type, if constant.</param>
    private static bool TryGetConstantType(
        Value value,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? type)
    {
        type = null;
        return TryGetConstantType(value, [], ref type) && type is not null;
    }

    /// <summary>
    /// Implements <see cref="TryGetConstantType(Value, out Type?)"/>.
    /// </summary>
    private static bool TryGetConstantType(
        Value value,
        HashSet<Value> visited,
        ref Type? type)
    {
        if (!visited.Add(value))
            return true;

        switch (value)
        {
            case HandleValue { Handle: Type handleType }:
                if (type is not null && !type.Equals(handleType))
                    return false;
                type = handleType;
                return true;
            case PhiValue { NumArguments: > 0 } phi:
                foreach (var argument in phi.Arguments)
                {
                    if (!TryGetConstantType(argument, visited, ref type))
                        return false;
                }
                return true;
            default:
                // A null Type (an unassigned local) or a value computed at run
                // time is never a compile-time type
                return false;
        }
    }
}
