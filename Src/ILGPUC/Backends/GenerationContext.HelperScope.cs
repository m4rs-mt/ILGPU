// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenerationContext.HelperScope.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;
using System.Linq;

namespace ILGPUC.Backends;

/// <summary>
/// A parameter that passes a name from the scope of the kernel function on to a
/// helper method: a built-in parameter of the kernel or a global declared in the
/// kernel function.
/// </summary>
/// <param name="Declaration">The parameter declaration in the helper.</param>
/// <param name="Argument">The argument passed by callers.</param>
readonly record struct HelperScopeParameter(string Declaration, string Argument);

partial class GenerationContext
{
    private Dictionary<Method, HelperScopeParameter[]>? _helperScopeParameters;
    private Dictionary<Method, Global[]>? _helperScopeGlobals;

    /// <summary>
    /// Returns the globals declared in the kernel function that a helper method
    /// or one of its callees uses, ordered by their ids. Globals of address
    /// spaces the backend declares at file scope are not included.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>The globals, empty for the entry point.</returns>
    public IReadOnlyList<Global> GetHelperScopeGlobals(Method method)
    {
        _helperScopeGlobals ??= ComputeHelperScopeGlobals();
        return _helperScopeGlobals.TryGetValue(method, out var globals)
            ? globals
            : [];
    }

    /// <summary>
    /// Returns the parameters a helper method takes in addition to its own to
    /// see the names that only exist in the kernel function: the built-in
    /// parameters of the kernel (Metal) and the globals the backend declares in
    /// the kernel function (e.g. threadgroup and thread memory on Metal, local
    /// memory on OpenCL), which the helper or one of its callees uses. Every
    /// caller has these names in scope and passes them on.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>The parameters, empty for the entry point.</returns>
    public IReadOnlyList<HelperScopeParameter> GetHelperScopeParameters(Method method)
    {
        _helperScopeParameters ??= ComputeHelperScopeParameters();
        return _helperScopeParameters.TryGetValue(method, out var parameters)
            ? parameters
            : [];
    }

    /// <summary>
    /// Computes the kernel-scope globals of all helper methods.
    /// </summary>
    private Dictionary<Method, Global[]> ComputeHelperScopeGlobals()
    {
        var entryPoint = Module.EntryPoint;

        // Collect the function-scoped globals each method uses and its callees
        var globals = new Dictionary<Method, HashSet<Global>>();
        var callees = new Dictionary<Method, HashSet<Method>>();
        foreach (var method in Module.Methods)
        {
            if (method == entryPoint || !method.HasImplementation)
                continue;
            var used = new HashSet<Global>();
            var called = new HashSet<Method>();
            method.ForEachValue<Value<Method>>(value =>
            {
                if (value is MethodCall call && call.Target.HasImplementation)
                    called.Add(call.Target);
                foreach (var operand in value.Values)
                {
                    if (operand is Global global &&
                        !LanguageConfig.IsFileScopeAddressSpace(global.AddressSpace))
                    {
                        used.Add(global);
                    }
                }
            });
            globals[method] = used;
            callees[method] = called;
        }

        // Propagate the globals of callees to their callers
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (method, called) in callees)
            {
                var used = globals[method];
                int count = used.Count;
                foreach (var callee in called)
                {
                    if (globals.TryGetValue(callee, out var calleeGlobals))
                        used.UnionWith(calleeGlobals);
                }
                changed |= used.Count != count;
            }
        }

        var result = new Dictionary<Method, Global[]>();
        foreach (var (method, used) in globals)
            result[method] = [.. used.OrderBy(g => g.Id)];
        return result;
    }

    /// <summary>
    /// Computes the helper scope parameters of all helper methods.
    /// </summary>
    private Dictionary<Method, HelperScopeParameter[]> ComputeHelperScopeParameters()
    {
        var entryPoint = Module.EntryPoint;
        var builtIns = LanguageConfig.GetHelperBuiltInParameters().ToArray();

        var typeEmitter = new TypeEmitter(this);
        var result = new Dictionary<Method, HelperScopeParameter[]>();
        foreach (var method in Module.Methods)
        {
            if (method == entryPoint || !method.HasImplementation)
                continue;
            var parameters = new List<HelperScopeParameter>(builtIns);
            foreach (var global in GetHelperScopeGlobals(method))
            {
                var keyword = LanguageConfig.GetAddressSpaceKeyword(global.AddressSpace);
                var typeName = LanguageConfig.GetQualifiedTypeName(
                    typeEmitter.GetTypeName(global.AllocType),
                    keyword);
                var name = GetValueName(global);
                parameters.Add(new(
                    $"{typeName}* {name}",
                    global.ArrayLength.HasValue ? name : "&" + name));
            }
            if (parameters.Count > 0)
                result[method] = [.. parameters];
        }
        return result;
    }
}
