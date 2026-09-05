// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Variables.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using System.Diagnostics;
using System.Reflection;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Loads a variable. This can be an argument or a local reference.
    /// </summary>
    /// <param name="var">The variable reference.</param>
    private void LoadVariable(VariableRef var)
    {
        Debug.Assert(
            var.RefType == VariableRefType.Argument ||
            var.RefType == VariableRefType.Local);
        var addressOrValue = Block.GetValue(var);
        var (type, flags) = _variableTypes[var];
        if (_variables.Contains(var))
            Block.Push(CreateLoad(addressOrValue, type, flags));
        else
            Block.Push(LoadOntoEvaluationStack(addressOrValue, flags));

        // Propagate delegate method from local table to the newly pushed stack value
        if (_delegateLocalTable.TryGetValue(var, out var delegateMethod))
        {
            var topValue = Block.GetValue(
                new VariableRef(Block.StackCounter - 1, VariableRefType.Stack));
            _delegateTable[topValue] = delegateMethod;
        }
        LoadDelegateTag(
            var,
            Block.GetValue(
                new VariableRef(Block.StackCounter - 1, VariableRefType.Stack)));
    }

    /// <summary>
    /// Loads a variable address. This can be an argument or a local reference.
    /// </summary>
    /// <param name="var">The variable reference.</param>
    private void LoadVariableAddress(VariableRef var)
    {
        Location.Assert(
            var.RefType == VariableRefType.Argument ||
            var.RefType == VariableRefType.Local);
        // Check whether we can load the address of a non-SSA value
        Location.Assert(_variables.Contains(var));
        var address = Block.GetValue(var);
        Block.Push(address);
    }

    /// <summary>
    /// Stores a value to the given variable slot.
    /// </summary>
    /// <param name="var">The variable reference.</param>
    private void StoreVariable(VariableRef var)
    {
        Location.Assert(
            var.RefType == VariableRefType.Argument ||
            var.RefType == VariableRefType.Local);
        var (type, flags) = _variableTypes[var];

        // Resolve a delegate handle before the value is converted/popped: the
        // handle is either registered or a phi-merged value of a control flow
        // merge whose paths agree on a single lambda body (e.g. the static
        // lambda caching pattern)
        MethodBase? delegateMethod = null;
        var rawValue = Block.StackCounter > 0
            ? Block.GetValue(
                new VariableRef(Block.StackCounter - 1, VariableRefType.Stack))
            : null;
        if (rawValue is not null &&
            !_delegateTable.TryGetValue(rawValue, out delegateMethod) &&
            (rawValue is not PhiValue ||
            !TryTraceDelegateHandle(rawValue, out delegateMethod)))
        {
            delegateMethod = null;
        }

        var storeValue = Block.Pop(type, flags);
        if (_variables.Contains(var))
        {
            var address = Block.GetValue(var);
            Builder.CreateStore(Location, address, storeValue);

            // Track the delegate of the variable, as loads create new values
            if (delegateMethod is not null)
                _delegateLocalTable[var] = delegateMethod;
            else
                _delegateLocalTable.Remove(var);
        }
        else
        {
            Block.SetValue(var, storeValue);
            StoreDelegateTag(var, rawValue, delegateMethod);

            // Register the converted value, so that loads in other blocks
            // resolve the delegate through their SSA values, and trace values
            // selecting different delegates back to their sources
            if (delegateMethod is not null)
                _delegateTable[storeValue] = delegateMethod;
            else if (rawValue is not null && rawValue != storeValue)
                _delegateAliases[storeValue] = rawValue;
        }
    }
}
