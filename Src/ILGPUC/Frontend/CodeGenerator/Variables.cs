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

using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Util;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        VerifyNoAddressTakenDelegate(var);
        var addressOrValue = Block.GetValue(var);
        var (type, flags) = _variableTypes[var];
        if (_variables.Contains(var))
            Block.Push(CreateLoad(addressOrValue, type, flags));
        else
            Block.Push(LoadOntoEvaluationStack(addressOrValue, flags));
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
        VerifyNoAddressTakenDelegate(var);
        var address = Block.GetValue(var);
        Block.Push(address);
    }

    /// <summary>
    /// Rejects uses of delegate variables whose addresses are taken, such as
    /// variables passed as <c>ref</c> or <c>out</c> arguments: delegates are
    /// bound to their targets through the SSA values of their variables,
    /// which do not see stores through references.
    /// </summary>
    /// <param name="var">The variable reference.</param>
    private void VerifyNoAddressTakenDelegate(VariableRef var)
    {
        if (!_variables.Contains(var) ||
            _variableTypes[var].Type is not PointerType
            {
                ElementType: DelegateHandleType,
            })
        {
            return;
        }

        string? name;
        if (var.RefType == VariableRefType.Argument)
        {
            int index = var.Index - Method.GetParameterOffset();
            var parameters = Method.GetParameters();
            name = index >= 0 && index < parameters.Length
                ? parameters[index].Name
                : null;
        }
        else
        {
            name = LocalVariableNames?.Invoke(var.Index);
        }
        name ??= var.Index.ToString(CultureInfo.InvariantCulture);

        throw new KernelCompilationException(
            KernelDiagnostic.AddressTakenDelegateId,
            string.Format(
                ErrorMessages.KernelDiagnosticAddressTakenDelegate,
                name,
                Method.GetDisplayName()),
            _addressLocations.GetValueOrDefault(var, Location));
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
        VerifyNoAddressTakenDelegate(var);
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
            // Address-taken variables never hold delegates (see above)
            var address = Block.GetValue(var);
            Builder.CreateStore(Location, address, storeValue);
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
