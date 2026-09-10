// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ReflectionFolding.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

/// <summary>
/// The reflection members that are evaluated at compile time: <c>typeof(T)</c>, type
/// comparisons, a few type properties and a few generic helpers whose result only
/// depends on closed type arguments.
/// </summary>
/// <remarks>
/// Each compilation of a method sees closed type arguments, so these members have a
/// constant result per instantiation. The frontend folds them while generating IR
/// (<see cref="CodeGenerator"/>), and the constant evaluator uses the same table to
/// evaluate helper predicates such as <c>IsFloat&lt;T&gt;()</c>. Types are compared
/// with <see cref="Type.Equals(Type)"/> on the runtime types, never on IR types,
/// because the IR unifies <c>int</c> and <c>uint</c>, and enums with their
/// underlying types.
/// </remarks>
static class ReflectionFolding
{
    /// <summary>
    /// The folded members.
    /// </summary>
    private enum Member
    {
        GetTypeFromHandle,
        OpEquality,
        OpInequality,
        EqualsType,
        EqualsObject,
        IsValueType,
        IsPrimitive,
        IsEnum,
        IsGenericType,
        IsAssignableFrom,
        IsAssignableTo,
        IsReferenceOrContainsReferences,
        SizeOf,
    }

    /// <summary>
    /// Maps the definitions of all folded members to their kind.
    /// </summary>
    private static readonly Dictionary<(Module, int), Member> Members = CreateMembers();

    /// <summary>
    /// Creates the member table.
    /// </summary>
    private static Dictionary<(Module, int), Member> CreateMembers()
    {
        var result = new Dictionary<(Module, int), Member>();
        void Add(MethodBase? method, Member member)
        {
            var definition = method.ThrowIfNull();
            result.Add((definition.Module, definition.MetadataToken), member);
        }
        MethodInfo? Getter(string name) =>
            typeof(Type).GetProperty(name)?.GetMethod;

        var type = typeof(Type);
        Add(type.GetMethod(nameof(Type.GetTypeFromHandle)), Member.GetTypeFromHandle);
        Add(type.GetMethod("op_Equality"), Member.OpEquality);
        Add(type.GetMethod("op_Inequality"), Member.OpInequality);
        Add(type.GetMethod(nameof(Type.Equals), [typeof(Type)]), Member.EqualsType);
        Add(type.GetMethod(nameof(Type.Equals), [typeof(object)]), Member.EqualsObject);
        Add(Getter(nameof(Type.IsValueType)), Member.IsValueType);
        Add(Getter(nameof(Type.IsPrimitive)), Member.IsPrimitive);
        Add(Getter(nameof(Type.IsEnum)), Member.IsEnum);
        Add(Getter(nameof(Type.IsGenericType)), Member.IsGenericType);
        Add(type.GetMethod(nameof(Type.IsAssignableFrom)), Member.IsAssignableFrom);
        Add(type.GetMethod(nameof(Type.IsAssignableTo)), Member.IsAssignableTo);
        Add(
            typeof(RuntimeHelpers).GetMethod(
                nameof(RuntimeHelpers.IsReferenceOrContainsReferences)),
            Member.IsReferenceOrContainsReferences);
        Add(typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf)), Member.SizeOf);
        return result;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="method"/> is evaluated at
    /// compile time.
    /// </summary>
    public static bool IsFolded(MethodBase method) =>
        Members.ContainsKey((method.Module, method.MetadataToken));

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="method"/> is a reflection
    /// member that kernels cannot use: a member of <see cref="Type"/>,
    /// <see cref="MemberInfo"/> or a runtime handle type that is not folded.
    /// </summary>
    public static bool IsUnsupportedReflection(MethodBase method) =>
        !IsFolded(method) &&
        method.DeclaringType is { } declaringType &&
        (typeof(MemberInfo).IsAssignableFrom(declaringType) ||
         declaringType == typeof(RuntimeTypeHandle) ||
         declaringType == typeof(RuntimeMethodHandle) ||
         declaringType == typeof(RuntimeFieldHandle));

    /// <summary>
    /// Returns the number of operands a folded member takes from the evaluation
    /// stack, including the instance for instance members.
    /// </summary>
    public static int GetOperandCount(MethodBase method) =>
        method.GetParameters().Length + (method.IsStatic ? 0 : 1);

    /// <summary>
    /// Returns <see langword="true"/> if the operand at <paramref name="index"/> of
    /// a folded member must be a type.
    /// </summary>
    /// <param name="method">A folded member.</param>
    /// <param name="index">The operand index, counting the instance first.</param>
    public static bool IsTypeOperand(MethodBase method, int index) =>
        Members[(method.Module, method.MetadataToken)] switch
        {
            Member.EqualsObject => index == 0,
            _ => true,
        };

    /// <summary>
    /// Evaluates a folded member.
    /// </summary>
    /// <param name="method">
    /// A folded member, closed over its generic arguments.
    /// </param>
    /// <param name="operands">
    /// The operands, the instance first. Type operands are <see cref="Type"/>
    /// objects; the second operand of <c>Equals(object)</c> may be any constant.
    /// </param>
    /// <param name="is64Bit">
    /// Whether the target has 64-bit pointers; determines the size of references.
    /// </param>
    /// <returns>
    /// The result: a <see cref="Type"/> for <c>GetTypeFromHandle</c>, an
    /// <see cref="int"/> for <c>SizeOf</c> and a <see cref="bool"/> otherwise.
    /// </returns>
    public static object Evaluate(
        MethodBase method,
        object?[] operands,
        bool is64Bit)
    {
        Type GetType(int index) => (Type)operands[index]!;

        return Members[(method.Module, method.MetadataToken)] switch
        {
            Member.GetTypeFromHandle => GetType(0),
            Member.OpEquality => GetType(0).Equals(GetType(1)),
            Member.OpInequality => !GetType(0).Equals(GetType(1)),
            Member.EqualsType => GetType(0).Equals(GetType(1)),
            Member.EqualsObject =>
                operands[1] is Type other && GetType(0).Equals(other),
            Member.IsValueType => GetType(0).IsValueType,
            Member.IsPrimitive => GetType(0).IsPrimitive,
            Member.IsEnum => GetType(0).IsEnum,
            Member.IsGenericType => GetType(0).IsGenericType,
            Member.IsAssignableFrom => GetType(0).IsAssignableFrom(GetType(1)),
            Member.IsAssignableTo => GetType(0).IsAssignableTo(GetType(1)),
            Member.IsReferenceOrContainsReferences =>
                (bool)((MethodInfo)method).Invoke(null, null)!,
            Member.SizeOf => GetManagedSize(method.GetGenericArguments()[0], is64Bit),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// Returns the managed size of <paramref name="type"/>, as the <c>sizeof</c>
    /// IL instruction and <c>Unsafe.SizeOf</c> define it: the size of a reference
    /// for reference types.
    /// </summary>
    public static int GetManagedSize(Type type, bool is64Bit) =>
        type.IsValueType ? type.SizeOf() : is64Bit ? 8 : 4;
}
