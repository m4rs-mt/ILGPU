// -----------------------------------------------------------------------------
//                                   ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: BasicBlock.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Compiler
{
    /// <summary>
    /// Represents a single basic block for code generation.
    /// </summary>
    sealed class BasicBlock
    {
        #region Nested Types

        /// <summary>
        /// Represents an incomplete phi node that has
        /// to be completed by adding its required operands
        /// later on.
        /// </summary>
        private struct IncompletePhi
        {
            /// <summary>
            /// Constructs an incomplete phi.
            /// </summary>
            /// <param name="variableRef">The referenced variable.</param>
            /// <param name="phi">The actual phi node.</param>
            /// <param name="phiType">The managed type of the node.</param>
            public IncompletePhi(
                VariableRef variableRef,
                LLVMValueRef phi,
                Type phiType)
            {
                VariableRef = variableRef;
                Phi = phi;
                PhiType = phiType;
            }

            /// <summary>
            /// Returns the associated variable ref.
            /// </summary>
            public VariableRef VariableRef { get; }

            /// <summary>
            /// Returns the actual phi node.
            /// </summary>
            public LLVMValueRef Phi { get; }

            /// <summary>
            /// Returns the type of the phi node.
            /// </summary>
            public Type PhiType { get; }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Value cache for SSA GetValue and SetValue functionality.
        /// </summary>
        private readonly Dictionary<VariableRef, Value> values = new Dictionary<VariableRef, Value>();

        /// <summary>
        /// Set of predecessors.
        /// </summary>
        private readonly HashSet<BasicBlock> predecessors = new HashSet<BasicBlock>();

        /// <summary>
        /// Set of successors.
        /// </summary>
        private readonly HashSet<BasicBlock> successors = new HashSet<BasicBlock>();

        /// <summary>
        /// Container for incomplete phis that have to be wired during block sealing.
        /// </summary>
        private readonly Dictionary<VariableRef, IncompletePhi> incompletePhis =
            new Dictionary<VariableRef, IncompletePhi>();

        /// <summary>
        /// Constructs a new basic block.
        /// </summary>
        /// <param name="blockHost">The block-host container.</param>
        /// <param name="basicBlock">The LLVM basic block.</param>
        public BasicBlock(IBasicBlockHost blockHost, LLVMBasicBlockRef basicBlock)
        {
            BlockHost = blockHost;
            LLVMBlock = basicBlock;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returnsd the current stack counter.
        /// </summary>
        public int StackCounter { get; set; }

        /// <summary>
        /// Returns the associated block host.
        /// </summary>
        public IBasicBlockHost BlockHost { get; }

        /// <summary>
        /// Returns the wrapped LLVM block.
        /// </summary>
        public LLVMBasicBlockRef LLVMBlock { get; }

        /// <summary>
        /// Returns the compilation unit.
        /// </summary>
        private CompileUnit Unit => BlockHost.Unit;

        /// <summary>
        /// Returns the used int-ptr type.
        /// </summary>
        private Type IntPtrType => Unit.IntPtrType;

        /// <summary>
        /// Returns the used builder.
        /// </summary>
        private LLVMBuilderRef Builder => BlockHost.Builder;

        /// <summary>
        /// Returns the current compilation context.
        /// </summary>
        private CompilationContext CompilationContext => BlockHost.CompilationContext;

        /// <summary>
        /// Returns the predecessors of this block.
        /// </summary>
        public IReadOnlyCollection<BasicBlock> Predecesors => predecessors;

        /// <summary>
        /// Returns the successors of this block.
        /// </summary>
        public IReadOnlyCollection<BasicBlock> Successors => successors;

        /// <summary>
        /// True, iff this block is sealed.
        /// </summary>
        public bool IsSealed { get; private set; }

        /// <summary>
        /// Returns the instruction offset of this block.
        /// </summary>
        public int InstructionOffset { get; set; }

        /// <summary>
        /// Returns the number of instructions in this block.
        /// </summary>
        public int InstructionCount { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given block as successor.
        /// </summary>
        /// <param name="successor">The successor to add.</param>
        public void AddSuccessor(BasicBlock successor)
        {
            successors.Add(successor);
            successor.predecessors.Add(this);
        }

        /// <summary>
        /// Sets the given variable to the given value.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <param name="value">The value to set.</param>
        public void SetValue(VariableRef var, Value value)
        {
            values[var] = value;
        }

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        public Value GetValue(VariableRef var)
        {
            if (values.TryGetValue(var, out Value value))
                return value;
            return GetValueRecursive(var);
        }

        /// <summary>
        /// Peeks a value recursively. This method only retrieves a value
        /// from a predecessor but does not build any phi nodes.
        /// </summary>
        /// <param name="var"></param>
        /// <returns></returns>
        private Value? PeekValue(VariableRef var)
        {
            if (values.TryGetValue(var, out Value value))
                return value;
            foreach (var preprocessor in predecessors)
            {
                Value? result;
                if (BlockHost.IsProcessed(preprocessor) &&
                    (result = preprocessor.PeekValue(var)) != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// Returns the value of the given variable by asking the predecessors.
        /// This method recursively constructs required phi nodes to break cycles.
        /// </summary>
        /// <param name="var">The variable reference.</param>
        /// <returns>The value of the given variable.</returns>
        private Value GetValueRecursive(VariableRef var)
        {
            Debug.Assert(predecessors.Count > 0);
            Value value;
            if (predecessors.Count == 1 && IsSealed)
                value = predecessors.First().GetValue(var);
            else
            {
                var peekedValue = PeekValue(var);
                Debug.Assert(peekedValue != null, "Invalid processed predecessors");
                value = peekedValue.Value;

                // Move to current block to place the phi node
                var currentBlock = GetInsertBlock(Builder);
                var firstInstruction = GetFirstInstruction(LLVMBlock);
                if (firstInstruction.Pointer != IntPtr.Zero)
                    PositionBuilderBefore(Builder, firstInstruction);
                else
                    PositionBuilderAtEnd(Builder, LLVMBlock);

                // Insert the actual phi node
                var phi = BuildPhi(Builder, TypeOf(value.LLVMValue), string.Empty);

                // Recover insert location
                PositionBuilderAtEnd(Builder, currentBlock);

                value = new Value(value.ValueType, phi);
                var incompletePhi = new IncompletePhi(
                    var,
                    phi,
                    value.ValueType);
                SetValue(var, value);
                if (IsSealed)
                    AddPhiOperands(incompletePhi);
                else
                    incompletePhis[var] = incompletePhi;
            }
            SetValue(var, value);
            return value;
        }

        /// <summary>
        /// Wires phi operands for the given variable reference and the given
        /// phi node. This method is invoked for sealed blocks during SSA
        /// construction or during the sealing process in the last step.
        /// </summary>
        /// <param name="incompletePhi">An incomplete phi node to complete.</param>
        private void AddPhiOperands(IncompletePhi incompletePhi)
        {
            LLVMValueRef[] phiValues = new LLVMValueRef[predecessors.Count];
            var phiBlocks = new LLVMBasicBlockRef[predecessors.Count];
            var phi = incompletePhi.Phi;
            int offset = 0;
            foreach (var pred in predecessors)
            {
                // Append possible conversion instructions to the right block
                var currentBlock = GetInsertBlock(Builder);

                // Get the related pred value
                var predValue = pred.GetValue(incompletePhi.VariableRef);

                // Position builder in the pred block
                // Ensure that we dont add an instruction behind the terminator (if it exists)
                var predBlock = pred.LLVMBlock;
                PositionBuilderAtEnd(Builder, predBlock);
                var predTerminator = GetBasicBlockTerminator(predBlock);
                if (predTerminator.Pointer == IntPtr.Zero)
                    PositionBuilderAtEnd(Builder, predBlock);
                else
                    PositionBuilderBefore(Builder, predTerminator);

                // Convert the value into the target type
                var convertedValue = Convert(predValue, incompletePhi.PhiType);

                // Position builder at the previous insert block
                PositionBuilderAtEnd(Builder, currentBlock);

                // Register the pred value
                phiValues[offset] = convertedValue.LLVMValue;
                phiBlocks[offset] = pred.LLVMBlock;
                ++offset;
            }
            AddIncoming(phi, out phiValues[0], out phiBlocks[0], phiValues.Length);
        }

        /// <summary>
        /// Seals this block (called when all predecessors have been seen) and
        /// wires all (previously unwired) phi nodes.
        /// </summary>
        public void Seal()
        {
            Debug.Assert(!IsSealed, "Cannot seal a sealed block");
            foreach (var var in incompletePhis.Values)
                AddPhiOperands(var);
            IsSealed = true;
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
            return GetValue(new VariableRef((StackCounter - 1), VariableRefType.Stack)).BasicValueType;
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
            values.Remove(var);
            return value;
        }

        /// <summary>
        /// Pops a value as the required type from the execution stack.
        /// </summary>
        /// <param name="targetType">The required targt type.</param>
        public Value Pop(Type targetType)
        {
            var op = Pop();
            return Convert(op, targetType);
        }

        /// <summary>
        /// Converts a value to the required type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The required targt type.</param>
        public Value Convert(Value value, Type targetType)
        {
            if (value.ValueType == targetType)
                return value;
            if (value.ValueType.IsValueType || value.ValueType.IsTreatedAsPtr())
                return BlockHost.CreateConversion(value, targetType);
            else
            {
                Debug.Assert(!targetType.IsValueType);
                return BlockHost.CreateCastClass(value, targetType);
            }
        }

        /// <summary>
        /// Pops an element as integer from the stack.
        /// </summary>
        /// <returns>The popped element as integer.</returns>
        public Value PopInt()
        {
            var type = PeekBasicValueType();
            switch (type)
            {
                case BasicValueType.Int32:
                case BasicValueType.Int64:
                case BasicValueType.UInt32:
                case BasicValueType.UInt64:
                    return Pop();
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                case BasicValueType.Single:
                    return Pop(typeof(int));
                case BasicValueType.UInt1:
                case BasicValueType.UInt8:
                case BasicValueType.UInt16:
                    return Pop(typeof(uint));
                case BasicValueType.Double:
                    return Pop(typeof(long));
                case BasicValueType.Ptr:
                    return Pop(IntPtrType);
                default:
                    throw CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedIntOperand, type);
            }
        }

        /// <summary>
        /// Pops the required arguments from the stack.
        /// </summary>
        /// <param name="methodBase">The method to use for the argument types.</param>
        /// <param name="methodValues">The target values.</param>
        /// <param name="valueOffset"></param>
        public void PopMethodArgs(
            MethodBase methodBase,
            Value[] methodValues,
            int valueOffset = 0)
        {
            var @params = methodBase.GetParameters();
            for (int i = methodValues.Length - 1; i >= valueOffset; --i)
            {
                var param = @params[i - valueOffset];
                var paramType = param.ParameterType.GetLLVMTypeRepresentation();
                methodValues[i] = Pop(paramType);
            }
            if (valueOffset > 0 && !methodValues[0].IsValid)
            {
                // This is an instance vall
                methodValues[0] = Pop(methodBase.DeclaringType.MakePointerType());
            }
        }

        /// <summary>
        /// Pops a value from the stack that can be used in the context of
        /// compare and arithmetic operations.
        /// </summary>
        /// <returns>
        /// The popped value from the stack that can be used in the
        /// context of compare and arithmetic operations.</returns>
        public Value PopCompareOrArithmeticValue()
        {
            var type = PeekBasicValueType();
            switch (type)
            {
                case BasicValueType.Int32:
                case BasicValueType.UInt32:
                case BasicValueType.Int64:
                case BasicValueType.UInt64:
                case BasicValueType.Single:
                case BasicValueType.Double:
                    return Pop();
                case BasicValueType.Ptr:
                    return Pop(IntPtrType);
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                    return Pop(typeof(int));
                case BasicValueType.UInt1:
                case BasicValueType.UInt8:
                case BasicValueType.UInt16:
                    return Pop(typeof(uint));
                default:
                    throw CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedCompareOrArithmeticValue, type);
            }
        }

        /// <summary>
        /// Pops two compatible arithmetic arguments from the execution stack.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The type of the two operands.</returns>
        public Type PopArithmeticArgs(out LLVMValueRef left, out LLVMValueRef right)
        {
            var rightVal = PopCompareOrArithmeticValue();
            var leftVal = PopCompareOrArithmeticValue();
            Value result = new Value();
            bool swapped = Utilities.Swap(
                leftVal.BasicValueType < rightVal.BasicValueType,
                ref leftVal,
                ref rightVal);

            // Convert void pointers (native ints) to a pointer to uint8 (uint8*)
            if (leftVal.ValueType.IsVoidPtr())
                leftVal = Convert(leftVal, typeof(byte).MakePointerType());
            left = leftVal.LLVMValue;
            switch (leftVal.BasicValueType)
            {
                case BasicValueType.Int32:
                case BasicValueType.Int64:
                case BasicValueType.UInt32:
                case BasicValueType.UInt64:
                case BasicValueType.Single:
                case BasicValueType.Double:
                    result = Convert(rightVal, leftVal.ValueType);
                    break;
                case BasicValueType.Ptr:
                    right = rightVal.LLVMValue;
                    switch (rightVal.BasicValueType)
                    {
                        case BasicValueType.Int32:
                        case BasicValueType.Int64:
                        case BasicValueType.UInt32:
                        case BasicValueType.UInt64:
                            result = rightVal;
                            break;
                        case BasicValueType.Ptr:
                            result = Convert(rightVal, IntPtrType);
                            break;
                        default:
                            throw CompilationContext.GetNotSupportedException(
                                ErrorMessages.NotSupportedArithmeticOperandTypes,
                                leftVal.BasicValueType,
                                rightVal.BasicValueType);
                    }
                    break;
                default:
                    throw CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedArithmeticOperandTypes,
                        leftVal.BasicValueType,
                        rightVal.BasicValueType);
            }
            Debug.Assert(result.IsValid);
            right = result.LLVMValue;

            Utilities.Swap(swapped, ref leftVal, ref rightVal);
            return leftVal.ValueType;
        }

        /// <summary>
        /// Pushes the value of the given type onto the execution stack.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <param name="value">The value.</param>
        public void Push(Type type, LLVMValueRef value)
        {
            Push(new Value(type, value));
        }

        /// <summary>
        /// Pushes the given stack value onto the execution stack.
        /// </summary>
        /// <param name="entry">The value to push.</param>
        public void Push(Value entry)
        {
            SetValue(new VariableRef(StackCounter++, VariableRefType.Stack), entry);
        }

        #endregion
    }
}
