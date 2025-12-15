// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueUtil.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR;

/// <summary>
/// A static helper class for values.
/// </summary>
/// <typeparam name="TValue">The value type.</typeparam>
static class ValueUtil<TValue> where TValue : IValue
{
    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a parameter.
    /// </summary>
    public static readonly bool IsParameter =
        typeof(TValue).IsAssignableTo(typeof(Parameter));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a method.
    /// </summary>
    public static readonly bool IsMethod =
        typeof(TValue).IsAssignableTo(typeof(Method));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a global value.
    /// </summary>
    public static readonly bool IsGlobal =
        typeof(TValue).IsAssignableTo(typeof(Global));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a type.
    /// </summary>
    public static readonly bool IsType =
        typeof(TValue).IsAssignableTo(typeof(TypeValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a basic block.
    /// </summary>
    public static readonly bool IsBasicBlock =
        typeof(TValue).IsAssignableTo(typeof(BasicBlock));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a pure value.
    /// </summary>
    public static readonly bool IsPureValue =
        typeof(TValue).IsAssignableTo(typeof(PureValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a basic block value.
    /// </summary>
    public static readonly bool IsBasicBlockValue =
        typeof(TValue).IsAssignableTo(typeof(BasicBlockValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a module value.
    /// </summary>
    public static readonly bool IsModuleValue =
        typeof(TValue).IsAssignableTo(typeof(Module));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a parameter or a base type of parameter.
    /// </summary>
    public static readonly bool IsParameterOrBase =
        typeof(TValue).IsAssignableTo(typeof(Parameter)) ||
        typeof(Parameter).IsAssignableTo(typeof(TValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a method or a base type of method.
    /// </summary>
    public static readonly bool IsMethodOrBase =
        typeof(TValue).IsAssignableTo(typeof(Method)) ||
        typeof(Method).IsAssignableTo(typeof(TValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a global value or a base type of global.
    /// </summary>
    public static readonly bool IsGlobalOrBase =
        typeof(TValue).IsAssignableTo(typeof(Global)) ||
        typeof(Global).IsAssignableTo(typeof(TValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a type or a base type of type.
    /// </summary>
    public static readonly bool IsTypeOrBase =
        typeof(TValue).IsAssignableTo(typeof(TypeValue)) ||
        typeof(TypeValue).IsAssignableTo(typeof(TValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a basic block or a base type of basic block.
    /// </summary>
    public static readonly bool IsBasicBlockOrBase =
        typeof(TValue).IsAssignableTo(typeof(BasicBlock)) ||
        typeof(BasicBlock).IsAssignableTo(typeof(TValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a pure value or a base type of pure value.
    /// </summary>
    public static readonly bool IsPureValueOrBase =
        typeof(TValue).IsAssignableTo(typeof(PureValue)) ||
        typeof(PureValue).IsAssignableTo(typeof(TValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a basic block value or a base type of basic block value.
    /// </summary>
    public static readonly bool IsBasicBlockValueOrBase =
        typeof(TValue).IsAssignableTo(typeof(BasicBlockValue)) ||
        typeof(BasicBlockValue).IsAssignableTo(typeof(TValue));

    /// <summary>
    /// Returns true if the parent <typeparamref name="TValue" /> is a module value or a base type of module value.
    /// </summary>
    public static readonly bool IsModuleValueOrBase =
        typeof(TValue).IsAssignableTo(typeof(Module)) ||
        typeof(Module).IsAssignableTo(typeof(TValue));
}
