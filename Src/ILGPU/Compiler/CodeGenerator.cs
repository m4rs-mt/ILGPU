// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CodeGenerator.cs
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
    /// Represents a LLVM code generator for .Net methods.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    sealed partial class CodeGenerator : DisposeBase, IBasicBlockHost
    {
        #region Instance

        /// <summary>
        /// The disassembled method for the current code generator.
        /// </summary>
        private readonly DisassembledMethod disassembledMethod;

        /// <summary>
        /// Stores processed basic blocks.
        /// </summary>
        private readonly HashSet<BasicBlock> processedBasicBlocks = new HashSet<BasicBlock>();

        /// <summary>
        /// Constructs a new code generator that targets the given unit.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="method">The source method for code generation.</param>
        /// <param name="disassembledMethod">The disassembled method for code generation.</param>
        public CodeGenerator(
            CompileUnit unit,
            Method method,
            DisassembledMethod disassembledMethod = null)
        {
            Debug.Assert(unit != null, "Invalid unit");
            Debug.Assert(method != null, "Invalid method");

            Unit = unit;
            Method = method;

            disassembledMethod = disassembledMethod ??
                DisassembledMethod.Disassemble(method.MethodBase, CompilationContext.NotSupportedILInstructionHandler);
            if (disassembledMethod.Method.GetMethodBody().ExceptionHandlingClauses.Count > 0)
                throw CompilationContext.GetNotSupportedException(
                    ErrorMessages.CustomExceptionSemantics, method.Name);
            Debug.Assert(
                method.MethodBase == disassembledMethod.Method,
                "The provided disassembled method does not match the given method for code generation");

            CompilationContext.VerifyEnteredMethod(method.MethodBase);

            this.disassembledMethod = disassembledMethod;
            Builder = CreateBuilderInContext(unit.LLVMContext);

            InitCFG();
            InitArgsAndLocals();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the assigned compile unit.
        /// </summary>
        public CompileUnit Unit { get; }

        /// <summary>
        /// Returns the source method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Returns the assigned instruction builder.
        /// </summary>
        public LLVMBuilderRef Builder { get; private set; }

        /// <summary>
        /// Returns the current compilation context.
        /// </summary>
        public CompilationContext CompilationContext => Unit.CompilationContext;

        #endregion

        #region Private Properties

        /// <summary>
        /// Returns the current method base.
        /// </summary>
        private MethodBase MethodBase => Method.MethodBase;

        /// <summary>
        /// Returns the associated LLVM context.
        /// </summary>
        private LLVMContextRef LLVMContext => Unit.LLVMContext;

        /// <summary>
        /// Returns the associated LLVM function.
        /// </summary>
        private LLVMValueRef Function => Method.LLVMFunction;

        /// <summary>
        /// Returns the entry block of the function.
        /// </summary>
        private BasicBlock EntryBlock { get; set; }

        /// <summary>
        /// Returns the current block.
        /// </summary>
        private BasicBlock CurrentBlock { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates LLVM code for the current method.
        /// </summary>
        public void GenerateCode()
        {
            // Seal the entry block
            EntryBlock.Seal();

            // Generate code for all blocks
            processedBasicBlocks.Clear();
            for (int blockIdx = postOrder.Count - 1; blockIdx >= 0; --blockIdx)
                GenerateCodeForBlock(postOrder[blockIdx]);
        }

        /// <summary>
        /// Returns true iff the given block was already preprocessed.
        /// </summary>
        /// <param name="block">The block to check.</param>
        /// <returns>True, iff the given block was already preprocessed.</returns>
        public bool IsProcessed(BasicBlock block)
        {
            return processedBasicBlocks.Contains(block);
        }

        /// <summary>
        /// Generates code for the given basic block.
        /// </summary>
        /// <param name="block">The block to generate code for.</param>
        private void GenerateCodeForBlock(BasicBlock block)
        {
            if (!block.IsSealed && block.Predecesors.All(processedBasicBlocks.Contains))
                block.Seal();

            if (processedBasicBlocks.Contains(block))
                return;
            processedBasicBlocks.Add(block);

            CurrentBlock = block;
            PositionBuilderAtEnd(Builder, CurrentBlock.LLVMBlock);
            for (int i = block.InstructionOffset, e = block.InstructionOffset + block.InstructionCount; i < e; ++i)
            {
                var instruction = disassembledMethod[i];
                if (!InstructionHandlers.TryGetValue(instruction.InstructionType, out InstructionHandler instructionHandler))
                    throw CompilationContext.GetNotSupportedException(
                        ErrorMessages.NotSupportedInstruction, Method.Name, instruction);
                instructionHandler(this, instruction);
            }

            // Handle implicit cases in which there is no explicit jump operation to a single successor
            if (block.Successors.Count == 1 && block.InstructionCount > 0)
            {
                var successor = block.Successors.First();
                var lastInstruction = disassembledMethod[block.InstructionOffset + block.InstructionCount - 1];
                if (!lastInstruction.IsTerminator)
                    BuildBr(Builder, successor.LLVMBlock);
            }
        }

        /// <summary>
        /// Creates a temporary alloca instruction that allocates a temp
        /// storage of the given type in the entry block.
        /// </summary>
        /// <param name="llvmType">The type of the temporary to allocate.</param>
        /// <returns>The allocated alloca instruction.</returns>
        private LLVMValueRef CreateTempAlloca(LLVMTypeRef llvmType)
        {
            var currentBlock = GetInsertBlock(Builder);
            var firstInstruction = GetFirstInstruction(EntryBlock.LLVMBlock);
            if (firstInstruction.Pointer != IntPtr.Zero)
                PositionBuilderBefore(Builder, firstInstruction);
            else
                PositionBuilderAtEnd(Builder, EntryBlock.LLVMBlock);
            var alloca = BuildAlloca(Builder, llvmType, string.Empty);
            PositionBuilderAtEnd(Builder, currentBlock);
            return alloca;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (Builder.Pointer != IntPtr.Zero)
            {
                DisposeBuilder(Builder);
                Builder = default(LLVMBuilderRef);
            }
        }

        #endregion
    }
}
