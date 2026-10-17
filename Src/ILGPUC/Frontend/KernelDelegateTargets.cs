// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelDelegateTargets.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

/// <summary>
/// Resolves the targets of the delegates registered with
/// <see cref="KernelDelegates.Register{TDelegate}(TDelegate, string)"/> at the
/// call sites of methods launching kernels. The kernels compiled for each
/// target bind the delegate with
/// <see cref="KernelDelegates.Bind{TDelegate}(string)"/>, which is resolved by
/// locating the registration with the same key in the IL of the assembly.
/// </summary>
static class KernelDelegateTargets
{
    /// <summary>
    /// The registered targets of each assembly by key.
    /// </summary>
    private static readonly ConditionalWeakTable<
        Assembly,
        Dictionary<string, MethodBase>> _targets = [];

    /// <summary>
    /// Returns true if the given method is
    /// <see cref="KernelDelegates.Bind{TDelegate}(string)"/>.
    /// </summary>
    /// <param name="method">The method to check.</param>
    public static bool IsBind(MethodBase method) =>
        IsKernelDelegatesMethod(method, nameof(KernelDelegates.Bind));

    /// <summary>
    /// Resolves the target registered under the given key in the given
    /// assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing the registration.</param>
    /// <param name="key">The key of the registration.</param>
    /// <returns>The registered target, or null if there is none.</returns>
    public static MethodBase? Resolve(Assembly assembly, string key) =>
        _targets.GetValue(assembly, CollectTargets).GetValueOrDefault(key);

    /// <summary>
    /// Returns true if the given method is the generic method of
    /// <see cref="KernelDelegates"/> with the given name. The declaring type is
    /// compared by name, as the assembly kernels are compiled from may load its
    /// own copy of the runtime library.
    /// </summary>
    private static bool IsKernelDelegatesMethod(MethodBase method, string name) =>
        method is MethodInfo { IsGenericMethod: true } &&
        method.Name == name &&
        method.DeclaringType?.FullName == typeof(KernelDelegates).FullName;

    /// <summary>
    /// Collects all registrations in the given assembly. A registration loads
    /// the delegate followed by its key, and the delegate is a lambda or a
    /// method group, which is created from the function pointer of its target
    /// (<c>ldftn</c>) right before the key, possibly through a delegate cache.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The registered targets by key.</returns>
    private static Dictionary<string, MethodBase> CollectTargets(Assembly assembly)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance |
            BindingFlags.DeclaredOnly;

        var targets = new Dictionary<string, MethodBase>();
        foreach (var type in GetTypes(assembly))
        {
            var methods = type.GetMethods(Flags)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors(Flags));
            foreach (var method in methods)
            {
                DisassembledMethod? disassembled;
                try
                {
                    disassembled = Disassembler.TryDisassemble(method);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // Methods that cannot be disassembled cannot register
                    // delegates for kernels either
                    continue;
                }
                if (disassembled is not null)
                    CollectTargets(disassembled, targets);
            }
        }
        return targets;
    }

    /// <summary>
    /// Collects the registrations in the given method.
    /// </summary>
    /// <param name="method">The disassembled method.</param>
    /// <param name="targets">The registered targets by key.</param>
    private static void CollectTargets(
        DisassembledMethod method,
        Dictionary<string, MethodBase> targets)
    {
        var instructions = method.Instructions;
        for (int i = 1; i < instructions.Length; ++i)
        {
            var instruction = instructions[i];
            if (instruction.InstructionType != ILInstructionType.Call ||
                instruction.Argument is not MethodBase callee ||
                !IsKernelDelegatesMethod(callee, nameof(KernelDelegates.Register)) ||
                instructions[i - 1] is not
                {
                    InstructionType: ILInstructionType.Ldstr,
                    Argument: string key,
                })
            {
                continue;
            }

            for (int j = i - 2; j >= 0; --j)
            {
                if (instructions[j] is
                    {
                        InstructionType: ILInstructionType.LdFunction,
                        Argument: MethodBase target,
                    })
                {
                    targets[key] = target;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Returns all types of the given assembly that can be loaded.
    /// </summary>
    private static IEnumerable<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.OfType<Type>();
        }
    }
}
