// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ClosureCaptures.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.Roslyn.Analysis;
using System.Collections.Generic;

namespace ILGPUC.Frontend;

/// <summary>
/// Collects the values that kernel code stores into the fields of closures
/// (the compiler-generated classes and structures holding the variables
/// captured by lambdas and local functions) and rejects closures capturing
/// views or pointers of shared or local memory. Closure fields hold generic
/// addresses, through which backends cannot access these address spaces.
/// </summary>
/// <remarks>
/// The check runs once all methods of a module have been generated, as the
/// captured values are traced through calls: a view passed to a parameter
/// that a called method captures refers to the memory of the caller.
/// </remarks>
/// <param name="moduleBuilder">The parent module builder.</param>
sealed class ClosureCaptures(ModuleBuilder moduleBuilder)
{
    #region Nested Types

    /// <summary>
    /// A value stored into a field of a closure.
    /// </summary>
    /// <param name="Value">The captured value.</param>
    /// <param name="MethodName">The name of the capturing method.</param>
    /// <param name="FieldName">The name of the field (the captured variable).</param>
    /// <param name="Location">The location of the store.</param>
    private sealed record Capture(
        Value Value,
        string MethodName,
        string FieldName,
        Location Location);

    #endregion

    #region Instance

    private readonly List<Capture> _captures = [];
    private readonly Dictionary<Method, List<MethodCall>> _calls = [];
    private readonly Dictionary<Value, List<Value>> _stores = [];
    private readonly Dictionary<Value, List<(MethodCall Call, int Index)>>
        _addressArguments = [];

    #endregion

    #region Methods

    /// <summary>
    /// Records a value stored into a field of a closure.
    /// </summary>
    /// <param name="value">The captured value.</param>
    /// <param name="methodName">The name of the capturing method.</param>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="location">The location of the store.</param>
    public void Add(
        Value value,
        string methodName,
        string fieldName,
        Location location) =>
        _captures.Add(new(value, methodName, fieldName, location));

    /// <summary>
    /// Rejects the first recorded capture of a view or a pointer of shared or
    /// local memory.
    /// </summary>
    public void Verify()
    {
        if (_captures.Count < 1)
            return;

        CollectCallsAndStores();
        foreach (var capture in _captures)
        {
            if (!PointsToGroupOrLocalMemory(capture.Value, [], []))
                continue;
            throw new KernelCompilationException(
                KernelDiagnostic.GroupMemoryCaptureId,
                string.Format(
                    ErrorMessages.KernelDiagnosticGroupMemoryCapture,
                    capture.MethodName,
                    capture.FieldName),
                capture.Location);
        }
    }

    /// <summary>
    /// Collects all calls, the values stored to each address, and the addresses
    /// passed to calls, of all methods generated so far.
    /// </summary>
    private void CollectCallsAndStores()
    {
        foreach (var method in moduleBuilder.GetMethods())
        {
            if (!method.HasImplementation || !method.IsSealed)
                continue;
            foreach (var block in method.Blocks)
            {
                foreach (var (value, _) in block.BasicBlockValues)
                {
                    switch (value)
                    {
                        case Store store:
                            GetList(_stores, GetAddressBase(store.Target))
                                .Add(store.Value);
                            break;
                        case MethodCall call:
                            GetList(_calls, call.Target).Add(call);
                            var arguments = call.Arguments;
                            for (int i = 0; i < arguments.Length; ++i)
                            {
                                if (arguments[i].Type is not PointerType)
                                    continue;
                                GetList(_addressArguments, GetAddressBase(arguments[i]))
                                    .Add((call, i));
                            }
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns the list stored under the given key, creating it if necessary.
    /// </summary>
    private static List<TValue> GetList<TKey, TValue>(
        Dictionary<TKey, List<TValue>> dictionary,
        TKey key)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var list))
        {
            list = [];
            dictionary.Add(key, list);
        }
        return list;
    }

    /// <summary>
    /// Returns true if the given value is (or contains) a view or a pointer of
    /// shared or local memory, tracing it through casts, views, structures,
    /// phi values, memory and calls.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <param name="visited">The values visited so far.</param>
    /// <param name="visitedMemory">The addresses visited so far.</param>
    private bool PointsToGroupOrLocalMemory(
        Value value,
        HashSet<Value> visited,
        HashSet<Value> visitedMemory)
    {
        if (!visited.Add(value))
            return false;
        switch (value)
        {
            case Parameter parameter:
                // Parameters refer to the arguments of all calls
                foreach (var call in _calls.GetValueOrDefault(parameter.Method) ?? [])
                {
                    if (parameter.Index < call.Arguments.Length &&
                        PointsToGroupOrLocalMemory(
                            call.Arguments[parameter.Index],
                            visited,
                            visitedMemory))
                    {
                        return true;
                    }
                }
                return false;
            case Load load:
                return StoresGroupOrLocalMemory(
                    GetAddressBase(load.Source),
                    visited,
                    visitedMemory);
            case PhiValue phi:
                foreach (var argument in phi.Arguments)
                {
                    if (PointsToGroupOrLocalMemory(argument, visited, visitedMemory))
                        return true;
                }
                return false;
            case PureValue:
                if (IsGroupOrLocalAddress(value))
                    return true;
                foreach (var operand in value.Values)
                {
                    if (operand.Type is AddressSpaceType or StructureType &&
                        PointsToGroupOrLocalMemory(operand, visited, visitedMemory))
                    {
                        return true;
                    }
                }
                return false;
            default:
                return IsGroupOrLocalAddress(value);
        }
    }

    /// <summary>
    /// Returns true if a view or a pointer of shared or local memory is stored
    /// to the memory at the given address: by a store in kernel code, by a
    /// called method the address is passed to, or, for addresses passed as
    /// parameters, by the callers.
    /// </summary>
    /// <param name="address">The base address of the memory.</param>
    /// <param name="visited">The values visited so far.</param>
    /// <param name="visitedMemory">The addresses visited so far.</param>
    private bool StoresGroupOrLocalMemory(
        Value address,
        HashSet<Value> visited,
        HashSet<Value> visitedMemory)
    {
        if (!visitedMemory.Add(address))
            return false;
        foreach (var stored in _stores.GetValueOrDefault(address) ?? [])
        {
            if (PointsToGroupOrLocalMemory(stored, visited, visitedMemory))
                return true;
        }

        // Called methods store through their parameters
        foreach (var (call, index) in _addressArguments.GetValueOrDefault(address) ?? [])
        {
            var parameters = call.Target.Parameters;
            if (index < parameters.Count &&
                StoresGroupOrLocalMemory(parameters[index], visited, visitedMemory))
            {
                return true;
            }
        }

        // Callers store to the memory passed to parameters
        if (address is Parameter parameter)
        {
            foreach (var call in _calls.GetValueOrDefault(parameter.Method) ?? [])
            {
                if (parameter.Index < call.Arguments.Length &&
                    StoresGroupOrLocalMemory(
                        GetAddressBase(call.Arguments[parameter.Index]),
                        visited,
                        visitedMemory))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the given value is a view or a pointer of shared or
    /// local memory.
    /// </summary>
    private static bool IsGroupOrLocalAddress(Value value) =>
        value.Type is AddressSpaceType
        {
            AddressSpace: MemoryAddressSpace.Shared or MemoryAddressSpace.Local,
        };

    /// <summary>
    /// Returns the allocation or value the given address is derived from.
    /// </summary>
    private static Value GetAddressBase(Value address)
    {
        while (address is PureValue { Count: > 0 } pure &&
            pure.Values[0].Type is AddressSpaceType)
        {
            address = pure.Values[0];
        }
        return address;
    }

    #endregion
}
