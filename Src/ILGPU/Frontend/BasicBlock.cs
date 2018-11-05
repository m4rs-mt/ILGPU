// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: BasicBlock.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Frontend
{
    /// <summary>
    /// A simple basic block in the scope of an IR code-generation process.
    /// </summary>
    sealed class BasicBlock : ICPSBuilderNode<BasicBlock, BasicBlock.Enumerator>
    {
        #region Nested Types

        /// <summary>
        /// Represents an empty enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<BasicBlock>
        {
            /// <summary cref="IEnumerator{T}.Current"/>
            public BasicBlock Current => throw new InvalidOperationException();

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => false;

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() { throw new InvalidOperationException(); }
        }

        #endregion

        #region Instance

        private int visitMarker = 0;

        /// <summary>
        /// Constructs a new basic block.
        /// </summary>
        /// <param name="codeGenerator">The parent code generator.</param>
        /// <param name="functionBuilder">The associated function builder.</param>
        /// <param name="nodeIndex">The current nodex index.</param>
        public BasicBlock(
            CodeGenerator codeGenerator,
            FunctionBuilder functionBuilder,
            int nodeIndex)
        {
            Debug.Assert(codeGenerator != null, "Invalid code generator");
            Debug.Assert(functionBuilder != null, "Invalid function builder");
            CodeGenerator = codeGenerator;
            FunctionBuilder = functionBuilder;
            NodeIndex = nodeIndex;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the code generator.
        /// </summary>
        public CodeGenerator CodeGenerator { get; }

        /// <summary>
        /// Returns the associated IR builder.
        /// </summary>
        public IRBuilder Builder => CodeGenerator.Builder;

        /// <summary>
        /// Returns the current stack counter.
        /// </summary>
        public int StackCounter { get; set; }

        /// <summary>
        /// Returns the instruction offset of this block.
        /// </summary>
        public int InstructionOffset { get; set; }

        /// <summary>
        /// Returns the number of instructions in this block.
        /// </summary>
        public int InstructionCount { get; set; }

        /// <summary>
        /// Returns the associated function builder.
        /// </summary>
        public FunctionBuilder FunctionBuilder { get; }

        /// <summary>
        /// Returns the associated function value.
        /// </summary>
        public FunctionValue FunctionValue => FunctionBuilder.FunctionValue;

        /// <summary>
        /// Returns the current node index.
        /// </summary>
        public int NodeIndex { get; }

        #endregion

        #region Methods

        /// <summary cref="ICPSBuilderNode{TNode, TEnumerator}.GetSuccessorEnumerator"/>
        Enumerator ICPSBuilderNode<BasicBlock, Enumerator>.GetSuccessorEnumerator() => new Enumerator();

        /// <summary>
        /// Returns true iff the block has been visited.
        /// </summary>
        /// <param name="marker">The visit marker.</param>
        /// <returns>True, iff the block has been visited.</returns>
        public bool IsVisited(int marker)
        {
            return visitMarker >= marker;
        }

        /// <summary>
        /// Visits the current block.
        /// </summary>
        /// <param name="marker">The visit marker.</param>
        /// <returns>True, iff the block has not been visited before.</returns>
        public bool Visit(int marker)
        {
            var result = IsVisited(marker);
            visitMarker = marker;
            return !result;
        }

        #endregion

        #region Variables

        /// <summary>
        /// Sets the given variable to the given value.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        public void SetValue(VariableRef var, Value value)
        {
            CodeGenerator.CPSBuilder.SetValue(this, var, value);
        }

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        public Value GetValue(VariableRef var)
        {
            return CodeGenerator.CPSBuilder.GetValue(this, var);
        }

        #endregion

        #region Memory

        /// <summary>
        /// Pushes a new memory reference to the given node.
        /// </summary>
        /// <param name="value">The node to push.</param>
        /// <returns>The created memory reference.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryRef PushMemory(Value value)
        {
            if (!(value is MemoryRef memoryRef))
                memoryRef = Builder.CreateMemoryReference(value);
            SetValue(VariableRef.Memory, memoryRef);
            return memoryRef;
        }

        /// <summary>
        /// Pops the current memory reference.
        /// </summary>
        /// <returns>The current memory reference.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryRef PopMemory()
        {
            var memoryValue = GetValue(VariableRef.Memory);
            Debug.Assert(memoryValue.Type.IsMemoryType, "Invalid memory reference");
            if (!(memoryValue is MemoryRef memoryRef))
            {
                memoryRef = Builder.CreateMemoryReference(memoryValue);
                SetValue(VariableRef.Memory, memoryRef);
            }
            return memoryRef;
        }

        #endregion

        #region Stack Methods

        /// <summary>
        /// Peeks the basic-value type of the element on the top of the stack.
        /// </summary>
        /// <returns>The peeked basic-value type.</returns>
        public BasicValueType PeekBasicValueType()
        {
            Debug.Assert(StackCounter > 0, "Stack empty");
            var value = GetValue(new VariableRef((StackCounter - 1), VariableRefType.Stack));
            return value.BasicValueType;
        }

        /// <summary>
        /// Duplicates the element at the top of the stack.
        /// </summary>
        public void Dup()
        {
            var var = new VariableRef(StackCounter - 1, VariableRefType.Stack);
            var targetVar = new VariableRef(StackCounter++, VariableRefType.Stack);
            SetValue(targetVar, GetValue(var));
        }

        /// <summary>
        /// Pops a value from the execution stack.
        /// </summary>
        /// <returns>The popped value.</returns>
        public Value Pop()
        {
            var var = new VariableRef(--StackCounter, VariableRefType.Stack);
            var value = GetValue(var);
            CodeGenerator.CPSBuilder.RemoveValue(this, var);
            return value;
        }

        /// <summary>
        /// Pops a value as the required type from the execution stack.
        /// </summary>
        /// <param name="targetType">The required targt type.</param>
        /// <param name="flags">The conversion flags.</param>
        public Value Pop(TypeNode targetType, ConvertFlags flags)
        {
            var op = Pop();
            return Convert(op, targetType, flags);
        }

        /// <summary>
        /// Converts a value to the required type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The required targt type.</param>
        /// <param name="flags">The conversion flags.</param>
        private Value Convert(Value value, TypeNode targetType, ConvertFlags flags)
        {
            if (value.Type == targetType)
                return value;
            return CodeGenerator.CreateConversion(
                value,
                targetType,
                flags);
        }

        /// <summary>
        /// Pops an element as integer from the stack.
        /// </summary>
        /// <param name="flags">The conversion flags.</param>
        /// <returns>The popped element as integer.</returns>
        public Value PopInt(ConvertFlags flags)
        {
            var type = PeekBasicValueType();
            switch (type)
            {
                case BasicValueType.Int32:
                case BasicValueType.Int64:
                    return Pop();
                case BasicValueType.Int1:
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                case BasicValueType.Float32:
                    return Pop(
                        Builder.CreatePrimitiveType(BasicValueType.Int32),
                        flags);
                case BasicValueType.Float64:
                    return Pop(
                        Builder.CreatePrimitiveType(BasicValueType.Int64),
                        flags);
                default:
                    throw CodeGenerator.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntOperand, type);
            }
        }

        /// <summary>
        /// Pops the required arguments from the stack.
        /// </summary>
        /// <param name="methodBase">The method to use for the argument types.</param>
        /// <param name="successorEnumerator">The successor enumerator of the current block.</param>
        /// <param name="instanceValue">The instance value (if available).</param>
        /// <param name="targetBlock">The resolved target block.</param>
        public ImmutableArray<ValueReference> PopMethodArgs<TEnumerator>(
            MethodBase methodBase,
            TEnumerator successorEnumerator,
            Value instanceValue,
            out BasicBlock targetBlock)
            where TEnumerator : IEnumerator<BasicBlock>
        {
            var moved = successorEnumerator.MoveNext();
            Debug.Assert(moved, "Invalid number of successors");
            targetBlock = successorEnumerator.Current;
            Debug.Assert(!successorEnumerator.MoveNext(), "Invalid number of successors");
            return PopMethodArgs(methodBase, targetBlock.FunctionValue, instanceValue);
        }

        /// <summary>
        /// Pops the required arguments from the stack.
        /// </summary>
        /// <param name="methodBase">The method to use for the argument types.</param>
        /// <param name="returnFunction">The jump target of the return function.</param>
        /// <param name="instanceValue">The instance value (if available).</param>
        public ImmutableArray<ValueReference> PopMethodArgs(
            MethodBase methodBase,
            FunctionValue returnFunction,
            Value instanceValue)
        {
            var parameters = methodBase.GetParameters();
            var parameterOffset = methodBase.GetParameterOffset();
            var result = ImmutableArray.CreateBuilder<ValueReference>(
                parameters.Length + parameterOffset + 2);

            // Handle main params
            for (int i = parameters.Length - 1; i >= 0; --i)
            {
                var param = parameters[i];
                var argument = Pop(
                    Builder.CreateType(
                        param.ParameterType),
                    param.ParameterType.IsUnsignedInt() ?
                        ConvertFlags.TargetUnsigned : ConvertFlags.None);
                result.Add(argument);
            }

            // Check instance value
            if (parameterOffset > 0 && instanceValue == null)
            {
                var declaringType = Builder.CreateType(methodBase.DeclaringType);
                declaringType = Builder.CreatePointerType(
                    declaringType,
                    MemoryAddressSpace.Generic);
                instanceValue = Pop(declaringType, ConvertFlags.None);
            }

            // Wire instance
            if (instanceValue != null)
                result.Add(instanceValue);
            // Wire the return param
            result.Add(returnFunction);
            // Wire the memory param
            result.Add(PopMemory());
            result.Reverse();

            return result.MoveToImmutable();
        }

        /// <summary>
        /// Pops a value from the stack that can be used in the context of
        /// compare operations.
        /// </summary>
        /// <param name="flags">The conversion flags.</param>
        /// <returns>
        /// The popped value from the stack that can be used in the
        /// context of compare and arithmetic operations.</returns>
        public Value PopCompareValue(ConvertFlags flags)
        {
            if (PeekBasicValueType() == BasicValueType.Int1)
                return Pop();
            return PopCompareOrArithmeticValue(flags);
        }

        /// <summary>
        /// Pops a value from the stack that can be used in the context of
        /// compare and arithmetic operations.
        /// </summary>
        /// <param name="flags">The conversion flags.</param>
        /// <returns>
        /// The popped value from the stack that can be used in the
        /// context of compare and arithmetic operations.</returns>
        public Value PopCompareOrArithmeticValue(ConvertFlags flags)
        {
            var type = PeekBasicValueType();
            switch (type)
            {
                case BasicValueType.Int32:
                case BasicValueType.Int64:
                case BasicValueType.Float32:
                case BasicValueType.Float64:
                    return Pop();
                case BasicValueType.Int1:
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                    return Pop(
                        Builder.CreatePrimitiveType(BasicValueType.Int32),
                        flags);
                default:
                    throw CodeGenerator.GetNotSupportedException(
                        ErrorMessages.NotSupportedCompareOrArithmeticValue, type);
            }
        }

        /// <summary>
        /// Pops two compatible arithmetic arguments from the execution stack.
        /// </summary>
        /// <param name="flags">The conversion flags.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The type of the two operands.</returns>
        public void PopArithmeticArgs(ConvertFlags flags, out Value left, out Value right)
        {
            right = PopCompareOrArithmeticValue(flags);
            left = PopCompareOrArithmeticValue(flags);

            Value result = null;
            bool swapped = Utilities.Swap(
                left.BasicValueType < right.BasicValueType,
                ref left,
                ref right);

            switch (left.BasicValueType)
            {
                case BasicValueType.Int32:
                case BasicValueType.Int64:
                case BasicValueType.Float32:
                case BasicValueType.Float64:
                    result = Convert(right, left.Type, flags);
                    break;
                default:
                    throw CodeGenerator.GetNotSupportedException(
                        ErrorMessages.NotSupportedArithmeticOperandTypes,
                        left.BasicValueType,
                        right.BasicValueType);
            }
            Debug.Assert(result != null);
            right = result;

            Utilities.Swap(swapped, ref left, ref right);
        }

        /// <summary>
        /// Pushes the value of the given type onto the execution stack.
        /// </summary>
        /// <param name="value">The value to push.</param>
        public void Push(Value value)
        {
            SetValue(new VariableRef(StackCounter++, VariableRefType.Stack), value);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this block.
        /// </summary>
        /// <returns>The string representation of this block.</returns>
        public override string ToString()
        {
            return FunctionValue.ToString();
        }

        #endregion
    }
}
