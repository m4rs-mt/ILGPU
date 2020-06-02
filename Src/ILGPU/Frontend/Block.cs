// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Block.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.Frontend
{
    /// <summary>
    /// A simple basic block in the scope of an IR code-generation process.
    /// </summary>
    sealed partial class Block
    {
        #region Instance

        /// <summary>
        /// Constructs a new basic block.
        /// </summary>
        /// <param name="codeGenerator">The parent code generator.</param>
        /// <param name="builder">The current basic block builder.</param>
        private Block(CodeGenerator codeGenerator, BasicBlock.Builder builder)
        {
            CodeGenerator = codeGenerator;
            Builder = builder;
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
        public BasicBlock.Builder Builder { get; }

        /// <summary>
        /// Returns the underlying basic block.
        /// </summary>
        public BasicBlock BasicBlock => Builder.BasicBlock;

        /// <summary>
        /// Returns the current terminator.
        /// </summary>
        public TerminatorValue Terminator => BasicBlock.Terminator;

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

        #endregion

        #region Variables

        /// <summary>
        /// Sets the given variable to the given value.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(VariableRef var, Value value) =>
            CodeGenerator.SSABuilder.SetValue(BasicBlock, var, value);

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Value GetValue(VariableRef var) =>
            CodeGenerator.SSABuilder.GetValue(BasicBlock, var);

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the current terminator as builder terminator.
        /// </summary>
        /// <param name="count">The number of expected branch targets.</param>
        /// <returns>The resolved branch targets.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<BasicBlock> GetBuilderTerminator(int count)
        {
            var targets = ((BuilderTerminator)Terminator).Targets;
            Terminator.Assert(targets.Length == count);
            return targets;
        }

        #endregion

        #region Stack Methods

        /// <summary>
        /// Peeks the basic-value type of the element on the top of the stack.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <returns>The peeked basic-value type.</returns>
        public BasicValueType PeekBasicValueType(Location location)
        {
            location.Assert(StackCounter > 0);
            var value = GetValue(
                new VariableRef(StackCounter - 1, VariableRefType.Stack));
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
            CodeGenerator.SSABuilder.RemoveValue(BasicBlock, var);
            return value;
        }

        /// <summary>
        /// Pops a value as the required type from the execution stack.
        /// </summary>
        /// <param name="targetType">The required target type.</param>
        /// <param name="flags">The conversion flags.</param>
        public Value Pop(
            TypeNode targetType,
            ConvertFlags flags)
        {
            var op = Pop();
            return Convert(op, targetType, flags);
        }

        /// <summary>
        /// Converts a value to the required type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The required target type.</param>
        /// <param name="flags">The conversion flags.</param>
        private Value Convert(
            Value value,
            TypeNode targetType,
            ConvertFlags flags) =>
            value.Type == targetType || targetType.IsRootType
            ? value
            : CodeGenerator.CreateConversion(
                value,
                targetType,
                flags);

        /// <summary>
        /// Pops an element as integer from the stack.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="flags">The conversion flags.</param>
        /// <returns>The popped element as integer.</returns>
        public Value PopInt(Location location, ConvertFlags flags)
        {
            var type = PeekBasicValueType(location);
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
                        Builder.GetPrimitiveType(BasicValueType.Int32),
                        flags);
                case BasicValueType.Float64:
                    return Pop(
                        Builder.GetPrimitiveType(BasicValueType.Int64),
                        flags);
                default:
                    throw location.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntOperand,
                        type);
            }
        }

        /// <summary>
        /// Pops the required arguments from the stack.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="methodBase">The method to use for the argument types.</param>
        /// <param name="instanceValue">The instance value (if available).</param>
        public ValueList PopMethodArgs(
            Location location,
            MethodBase methodBase,
            Value instanceValue)
        {
            var parameters = methodBase.GetParameters();
            var parameterOffset = methodBase.GetParameterOffset();
            var result = ValueList.Create(parameters.Length + parameterOffset);

            // Handle main params
            for (int i = parameters.Length - 1; i >= 0; --i)
            {
                var param = parameters[i];
                var argument = Pop(
                    Builder.CreateType(param.ParameterType),
                    param.ParameterType.IsUnsignedInt() ?
                        ConvertFlags.TargetUnsigned : ConvertFlags.None);
                result.Add(argument);
            }

            // Check instance value
            if (parameterOffset > 0)
            {
                if (instanceValue == null)
                {
                    var declaringType = Builder.CreateType(methodBase.DeclaringType);
                    if (!Intrinsics.IsIntrinsicArrayType(methodBase.DeclaringType))
                    {
                        declaringType = Builder.CreatePointerType(
                            declaringType,
                            MemoryAddressSpace.Generic);
                    }
                    instanceValue = Pop(
                        declaringType,
                        ConvertFlags.None);
                }
                else
                {
                    // Wire instance
                    instanceValue = Builder.CreateAddressSpaceCast(
                        location,
                        instanceValue,
                        MemoryAddressSpace.Generic);
                }

                result.Add(instanceValue);
            }

            result.Reverse();
            return result;
        }

        /// <summary>
        /// Pops a value from the stack that can be used in the context of
        /// compare operations.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="flags">The conversion flags.</param>
        /// <returns>
        /// The popped value from the stack that can be used in the
        /// context of compare and arithmetic operations.</returns>
        public Value PopCompareValue(Location location, ConvertFlags flags) =>
            PeekBasicValueType(location) == BasicValueType.Int1
            ? Pop()
            : PopCompareOrArithmeticValue(location, flags);

        /// <summary>
        /// Pops a value from the stack that can be used in the context of
        /// compare and arithmetic operations.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="flags">The conversion flags.</param>
        /// <returns>
        /// The popped value from the stack that can be used in the
        /// context of compare and arithmetic operations.</returns>
        public Value PopCompareOrArithmeticValue(Location location, ConvertFlags flags)
        {
            var type = PeekBasicValueType(location);
            switch (type)
            {
                case BasicValueType.Int1:
                case BasicValueType.Int32:
                case BasicValueType.Int64:
                case BasicValueType.Float32:
                case BasicValueType.Float64:
                    return Pop();
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                    return Pop(
                        Builder.GetPrimitiveType(BasicValueType.Int32),
                        flags);
                default:
                    throw location.GetNotSupportedException(
                        ErrorMessages.NotSupportedCompareOrArithmeticValue, type);
            }
        }

        /// <summary>
        /// Pops two compatible arithmetic arguments from the execution stack.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="flags">The conversion flags.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The type of the two operands.</returns>
        public void PopArithmeticArgs(
            Location location,
            ConvertFlags flags,
            out Value left,
            out Value right)
        {
            right = PopCompareOrArithmeticValue(location, flags);
            left = PopCompareOrArithmeticValue(location, flags);

            Value result;
            bool swapped = Utilities.Swap(
                left.BasicValueType < right.BasicValueType,
                ref left,
                ref right);

            switch (left.BasicValueType)
            {
                case BasicValueType.Int1:
                case BasicValueType.Int32:
                case BasicValueType.Int64:
                case BasicValueType.Float32:
                case BasicValueType.Float64:
                    result = Convert(right, left.Type, flags);
                    break;
                default:
                    throw location.GetNotSupportedException(
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
            var newStackSlot = new VariableRef(StackCounter++, VariableRefType.Stack);
            SetValue(newStackSlot, value);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this block.
        /// </summary>
        /// <returns>The string representation of this block.</returns>
        public override string ToString() => BasicBlock.ToString();

        #endregion
    }
}
