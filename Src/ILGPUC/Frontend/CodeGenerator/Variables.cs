// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Variables.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using System.Diagnostics;

namespace ILGPU.Frontend
{
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
            var type = variableTypes[var];
            if (variables.Contains(var))
            {
                Block.Push(CreateLoad(
                    addressOrValue,
                    type.Item1,
                    type.Item2));
            }
            else
            {
                Block.Push(LoadOntoEvaluationStack(
                    addressOrValue,
                    type.Item2));
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
            Location.Assert(variables.Contains(var));
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
            var variableType = variableTypes[var];
            var storeValue = Block.Pop(
                variableType.Item1,
                variableType.Item2);
            if (variables.Contains(var))
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
}
