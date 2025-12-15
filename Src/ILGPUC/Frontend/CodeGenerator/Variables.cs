// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Variables.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System.Diagnostics;

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

        // Propagate delegate tracking before the value is converted/popped
        if (Block.StackCounter > 0)
        {
            var rawTop = Block.GetValue(
                new VariableRef(Block.StackCounter - 1, VariableRefType.Stack));
            if (_delegateTable.TryGetValue(rawTop, out var delegateMethod))
            {
                _delegateLocalTable[var] = delegateMethod;
            }
            else if (_pendingDelegateForLocal is not null)
            {
                // Fallback: the stack top is a phi-merged value from a
                // control flow merge (static lambda caching pattern).
                // Use the most recent delegate from MakeNewDelegate.
                _delegateLocalTable[var] = _pendingDelegateForLocal;
                _pendingDelegateForLocal = null;
            }
        }

        var storeValue = Block.Pop(type, flags);
        if (_variables.Contains(var))
        {
            var address = Block.GetValue(var);
            Builder.CreateStore(Location, address, storeValue);
        }
        else
        {
            Block.SetValue(var, storeValue);
        }
    }
}
