// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System.Collections.Generic;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a function generator for main kernel functions.
    /// </summary>
    sealed class PTXKernelFunctionGenerator : PTXCodeGenerator
    {
        #region Nested Types

        private readonly struct KernelParameterSetupLogic : IParameterSetupLogic
        {
            public KernelParameterSetupLogic(
                EntryPoint entryPoint,
                PTXKernelFunctionGenerator parent)
            {
                EntryPoint = entryPoint;
                Parent = parent;
                IndexParameters = new (Parameter, PTXRegister?)[
                    entryPoint.NumFlattendedIndexParameters];
            }

            /// <summary>
            /// Returns the associated entry point.
            /// </summary>
            public EntryPoint EntryPoint { get; }

            /// <summary>
            /// Returns an array containing all index parameters.
            /// </summary>
            public (Parameter, PTXRegister?)[] IndexParameters { get; }

            /// <summary>
            /// Returns the associated register allocator.
            /// </summary>
            public PTXKernelFunctionGenerator Parent { get; }

            /// <summary cref="PTXCodeGenerator.IParameterSetupLogic.HandleIntrinsicParameters(int, Parameter, PTXType)"/>
            public PTXRegister? HandleIntrinsicParameters(
                int parameterOffset,
                Parameter parameter,
                PTXType paramType)
            {
                PTXRegister? register = null;
                if (!EntryPoint.IsGroupedIndexEntry)
                {
                    // This is an implicitly grouped kernel that needs
                    // boundary information to avoid out-of-bounds dispatches
                    register = Parent.AllocateRegister(paramType.RegisterKind);
                }
                IndexParameters[parameterOffset] = (parameter, register);
                return register;
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// Uses this function generator to emit PTX code.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="scope">The current scope.</param>
        /// <param name="sharedAllocations">Alloa information about shared memory.</param>
        /// <param name="allocas">Alloca information about the given scope.</param>
        /// <param name="constantOffset">The constant offset inside in the PTX code.</param>
        public static void Generate(
            in GeneratorArgs args,
            Scope scope,
            Allocas allocas,
            in AllocaKindInformation sharedAllocations,
            ref int constantOffset)
        {
            var generator = new PTXKernelFunctionGenerator(args, scope);
            generator.GenerateCode(
                args.EntryPoint,
                allocas,
                sharedAllocations,
                ref constantOffset);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Creates a new PTX kernel generator.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="scope">The current scope.</param>
        private PTXKernelFunctionGenerator(in GeneratorArgs args, Scope scope)
            : base(args, scope)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Generates PTX code.
        /// </summary>
        private void GenerateCode(
            EntryPoint entryPoint,
            Allocas allocas,
            in AllocaKindInformation sharedAllocations,
            ref int constantOffset)
        {
            Builder.AppendLine();
            Builder.Append(".visible .entry ");
            Builder.Append(PTXCompiledKernel.EntryName);
            Builder.AppendLine("(");

            var parameterLogic = new KernelParameterSetupLogic(
                entryPoint,
                this);
            var parameters = SetupParameters(ref parameterLogic, parameterLogic.IndexParameters.Length);
            Builder.AppendLine();
            Builder.AppendLine(")");
            SetupKernelSpecialization(entryPoint.Specialization);
            Builder.AppendLine("{");

            // Build memory allocations

            var allocations = SetupLocalAllocations(allocas);
            SetupSharedAllocations(sharedAllocations, allocations);
            var registerOffset = Builder.Length;

            // Build param bindings and local memory variables
            BindAllocations(allocations);
            BindParameters(parameters);

            // Setup kernel indices
            SetupKernelIndex(entryPoint, parameterLogic.IndexParameters);

            GenerateCode(registerOffset, ref constantOffset);
        }

        /// <summary>
        /// Setups shared-memory allocations.
        /// </summary>
        /// <param name="allocas">The allocations to setup.</param>
        /// <param name="result">A list of pairs associating alloca nodes with thei local variable names.</param>
        private void SetupSharedAllocations(
            in AllocaKindInformation allocas,
            List<(Alloca, string)> result)
        {
            var offset = 0;
            foreach (var allocaInfo in allocas)
            {
                Builder.Append('\t');
                Builder.Append(".shared ");
                var elementType = allocaInfo.ElementType;
                var elementSize = ABI.GetSizeOf(elementType);

                Builder.Append(".align ");
                Builder.Append(elementSize);
                Builder.Append(" .b8 ");

                var name = "__shared_alloca" + offset++;
                Builder.Append(name);
                Builder.Append('[');
                Builder.Append(allocaInfo.ArraySize * elementSize);
                Builder.AppendLine("];");

                result.Add((allocaInfo.Alloca, name));
            }
            Builder.AppendLine();
        }

        /// <summary>
        /// Setups kernel specialization hints.
        /// </summary>
        /// <param name="specialization">The kernel specialization.</param>
        private void SetupKernelSpecialization(in KernelSpecialization specialization)
        {
            if (specialization.MaxNumThreadsPerGroup.HasValue)
            {
                Builder.Append(".maxntid ");
                Builder.Append(specialization.MaxNumThreadsPerGroup);
                Builder.AppendLine(", 1, 1");
            }

            if (specialization.MinNumGroupsPerMultiprocessor.HasValue)
            {
                Builder.Append(".minnctapersm ");
                Builder.Append(specialization.MinNumGroupsPerMultiprocessor);
                Builder.AppendLine();
            }
        }

        /// <summary>
        /// Emits an implicit kernel index computation.
        /// </summary>
        /// <param name="parameter">The index parameter.</param>
        /// <param name="dimension">The parameter dimension.</param>
        /// <param name="boundsRegister">The associated bounds register.</param>
        private void EmitImplicitKernelIndex(
            Parameter parameter,
            int dimension,
            PTXRegister boundsRegister)
        {
            var ctaid = new PTXRegister(PTXRegisterKind.Ctaid, dimension);
            var ntid = new PTXRegister(PTXRegisterKind.NtId, dimension);
            var tid = new PTXRegister(PTXRegisterKind.Tid, dimension);

            var aReg = AllocateRegister(PTXRegisterKind.Int32);
            var bReg = AllocateRegister(PTXRegisterKind.Int32);
            var cReg = AllocateRegister(PTXRegisterKind.Int32);

            Move(ctaid, aReg);
            Move(ntid, bReg);
            Move(tid, cReg);

            var targetRegister = Allocate(parameter, PTXRegisterKind.Int32);
            using (var command = BeginCommand(
                Instructions.FMAOperationLo,
                PTXType.GetPTXType(ArithmeticBasicValueType.Int32)))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(aReg);
                command.AppendArgument(bReg);
                command.AppendArgument(cReg);
            }

            FreeRegister(aReg);
            FreeRegister(bReg);
            FreeRegister(cReg);

            var predicateRegister = AllocateRegister(PTXRegisterKind.Predicate);
            using (var command = BeginCommand(
                Instructions.GetCompareOperation(
                    CompareKind.GreaterEqual,
                    ArithmeticBasicValueType.Int32)))
            {
                command.AppendArgument(predicateRegister);
                command.AppendArgument(targetRegister);
                command.AppendArgument(boundsRegister);
            }

            using (var command = BeginCommand(
                Instructions.ReturnOperation,
                null,
                new PredicateConfiguration(predicateRegister, true)))
            { }

            FreeRegister(predicateRegister);
        }

        /// <summary>
        /// Emits an explicit kernel index computation.
        /// </summary>
        /// <param name="gridIdx">The grid index parameter.</param>
        /// <param name="groupIdx">The group index parameter.</param>
        /// <param name="dimension">The dimension index.</param>
        private void EmitExplicitKernelIndex(
            Parameter gridIdx,
            Parameter groupIdx,
            int dimension)
        {
            var ctaid = new PTXRegister(PTXRegisterKind.Ctaid, dimension);
            var tid = new PTXRegister(PTXRegisterKind.Tid, dimension);

            var gridIdxReg = Allocate(gridIdx, PTXRegisterKind.Int32);
            var groupIdxReg = Allocate(groupIdx, PTXRegisterKind.Int32);

            Move(ctaid, gridIdxReg);
            Move(tid, groupIdxReg);
        }

        /// <summary>
        /// Setups the current kernel indices.
        /// </summary>
        /// <param name="entryPoint">The current entry point.</param>
        /// <param name="indexParameters">The index parameters to setup.</param>
        private void SetupKernelIndex(
            EntryPoint entryPoint,
            (Parameter, PTXRegister?)[] indexParameters)
        {
            if (entryPoint.IsGroupedIndexEntry)
            {
                for (int i = 0, e = (int)entryPoint.IndexType - (int)IndexType.Index3D; i < e; ++i)
                {
                    EmitExplicitKernelIndex(
                        indexParameters[i].Item1,
                        indexParameters[i + e].Item1,
                        i);
                }
            }
            else
            {
                for (int i = 0, e = (int)entryPoint.IndexType; i < e; ++i)
                {
                    EmitImplicitKernelIndex(
                        indexParameters[i].Item1,
                        i,
                        indexParameters[i].Item2.Value);
                }
            }
        }

        #endregion
    }
}
