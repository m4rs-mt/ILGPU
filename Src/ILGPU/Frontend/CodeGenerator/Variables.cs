// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Variables.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using System.Diagnostics;

namespace ILGPU.Frontend
{
    partial class CodeGenerator
    {
        /// <summary>
        /// Loads a variable. This can be an argument or a local reference.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="var">The variable reference.</param>
        private void LoadVariable(
            Block block,
            IRBuilder builder,
            VariableRef var)
        {
            Debug.Assert(
                var.RefType == VariableRefType.Argument ||
                var.RefType == VariableRefType.Local);
            var addressOrValue = block.GetValue(var);
            var type = variableTypes[var];
            if (variables.Contains(var))
            {
                block.Push(CreateLoad(
                    builder,
                    addressOrValue,
                    type.Item1,
                    type.Item2));
            }
            else
            {
                block.Push(LoadOntoEvaluationStack(
                    builder,
                    addressOrValue,
                    type.Item2));
            }
        }

        /// <summary>
        /// Loads a variable address. This can be an argument or a local reference.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="var">The variable reference.</param>
        private void LoadVariableAddress(Block block, VariableRef var)
        {
            Debug.Assert(
                var.RefType == VariableRefType.Argument ||
                var.RefType == VariableRefType.Local);
            Debug.Assert(variables.Contains(var), "Cannot load address of SSA value");
            var address = block.GetValue(var);
            block.Push(address);
        }

        /// <summary>
        /// Stores a value to the given variable slot.
        /// </summary>
        /// <param name="block">The current basic block.</param>
        /// <param name="builder">The current builder.</param>
        /// <param name="var">The variable reference.</param>
        private void StoreVariable(
            Block block,
            IRBuilder builder,
            VariableRef var)
        {
            Debug.Assert(
                var.RefType == VariableRefType.Argument ||
                var.RefType == VariableRefType.Local);
            var variableType = variableTypes[var];
            var storeValue = block.Pop(variableType.Item1, variableType.Item2);
            if (variables.Contains(var))
            {
                var address = block.GetValue(var);
                builder.CreateStore(address, storeValue);
            }
            else
            {
                block.SetValue(var, storeValue);
            }
        }
    }
}
