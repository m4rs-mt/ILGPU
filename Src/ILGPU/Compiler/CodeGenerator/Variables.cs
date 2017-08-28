// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Variables.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        #region Instance

        private readonly Dictionary<VariableRef, Value> variables = new Dictionary<VariableRef, Value>();
        private readonly Dictionary<VariableRef, Type> variableTypes = new Dictionary<VariableRef, Type>();

        /// <summary>
        /// Initializes args and locals for the current method.
        /// </summary>
        private void InitArgsAndLocals()
        {
            // Check for ssa variables
            for (int i = 0, e = disassembledMethod.Count; i < e; ++i)
            {
                var instruction = disassembledMethod[i];
                switch (instruction.InstructionType)
                {
                    case ILInstructionType.Ldarga:
                        variables[new VariableRef(instruction.GetArgumentAs<int>(), VariableRefType.Argument)] =
                            default(Value);
                        break;
                    case ILInstructionType.Ldloca:
                        variables[new VariableRef(instruction.GetArgumentAs<int>(), VariableRefType.Local)] =
                            default(Value);
                        break;
                }
            }

            CurrentBlock = EntryBlock;
            PositionBuilderAtEnd(Builder, CurrentBlock.LLVMBlock);

            // Init params
            var @params = GetParams(Function);
            if (Method.IsInstance)
            {
                // Broken, wegen ldarga.0 bei instance methods?
                // Da muss noch eine adresse her... nur woher genau?
                EntryBlock.SetValue(
                    new VariableRef(0, VariableRefType.Argument),
                    new Value(MethodBase.DeclaringType.MakePointerType(), @params[0]));
            }
            var methodParams = Method.MethodBase.GetParameters();
            for (int i = Method.ParameterOffset, e = @params.Length; i < e; ++i)
            {
                var param = @params[i];
                var methodParam = methodParams[i - Method.ParameterOffset];
                var paramType = methodParam.ParameterType.GetLLVMTypeRepresentation();
                var argRef = new VariableRef(i, VariableRefType.Argument);
                if (variables.ContainsKey(argRef))
                {
                    // Address was taken... emit a temporary alloca and store the arg value to it
                    var alloca = BuildAlloca(Builder, TypeOf(param), methodParam.Name);
                    BuildStore(Builder, param, alloca);
                    variables[argRef] = new Value(paramType, alloca);
                }
                else
                {
                    // Use the current arg value to set the value in the first block
                    EntryBlock.SetValue(argRef, new Value(paramType, param));
                }
                variableTypes[argRef] = paramType;
            }

            // Init locals
            var localVariables = MethodBase.GetMethodBody().LocalVariables;
            for (int i = 0, e = localVariables.Count; i < e; ++i)
            {
                var variable = localVariables[i];
                var variableType = variable.LocalType.GetLLVMTypeRepresentation();
                var localType = Unit.GetType(variableType);
                var localRef = new VariableRef(i, VariableRefType.Local);
                var initValue = ConstNull(localType);
                if (variables.ContainsKey(localRef))
                {
                    // Address was taken... emit a temporary alloca and store empty value to it
                    var alloca = BuildAlloca(Builder, localType, string.Empty);
                    BuildStore(Builder, initValue, alloca);
                    variables[localRef] = new Value(variableType, alloca);
                }
                else
                    EntryBlock.SetValue(localRef, new Value(variableType, initValue));
                variableTypes[localRef] = variableType;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads a variable. This can be an argument or a local reference.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        private void LoadVariable(VariableRef var)
        {
            Debug.Assert(var.RefType == VariableRefType.Argument || var.RefType == VariableRefType.Local);
            if (variables.TryGetValue(var, out Value nonSSAValue))
            {
                var load = BuildLoad(Builder, nonSSAValue.LLVMValue, string.Empty);
                CurrentBlock.Push(nonSSAValue.ValueType, load);
            }
            else
                CurrentBlock.Push(CurrentBlock.GetValue(var));
        }

        /// <summary>
        /// Loads a variable address. This can be an argument or a local reference.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        private void LoadVariableAddress(VariableRef var)
        {
            Debug.Assert(var.RefType == VariableRefType.Argument || var.RefType == VariableRefType.Local);
            var value = variables[var];
            CurrentBlock.Push(value.ValueType.MakePointerType(), value.LLVMValue);
        }

        /// <summary>
        /// Stores a value to the argument with index idx.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        private void StoreVariable(VariableRef var)
        {
            Debug.Assert(var.RefType == VariableRefType.Argument || var.RefType == VariableRefType.Local);
            var variableType = variableTypes[var];
            var value = CurrentBlock.Pop(variableType);
            if (variables.TryGetValue(var, out Value nonSSAValue))
                BuildStore(Builder, value.LLVMValue, nonSSAValue.LLVMValue);
            else
                CurrentBlock.SetValue(var, value);
        }

        /// <summary>
        /// Loads a value of the given type from an unsafe memory address.
        /// </summary>
        /// <param name="type">The type of the value to load.</param>
        private void LoadIndirect(Type type)
        {
            var address = CurrentBlock.Pop(type.MakePointerType());
            var load = BuildLoad(Builder, address.LLVMValue, "ldind");
            CurrentBlock.Push(type, load);
        }

        /// <summary>
        /// Stores a value to an unsafe address.
        /// </summary>
        /// <param name="type">The type of the value to store.</param>
        private void StoreIndirect(Type type)
        {
            var value = CurrentBlock.Pop(type);
            var address = CurrentBlock.Pop(type.MakePointerType());
            BuildStore(Builder, value.LLVMValue, address.LLVMValue);
        }

        #endregion
    }
}
