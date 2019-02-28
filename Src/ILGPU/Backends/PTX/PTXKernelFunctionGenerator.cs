// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a function generator for main kernel functions.
    /// </summary>
    sealed class PTXKernelFunctionGenerator : PTXCodeGenerator
    {
        #region Nested Types

        private struct KernelParameterSetupLogic : IParameterSetupLogic
        {
            public KernelParameterSetupLogic(
                EntryPoint entryPoint,
                PTXKernelFunctionGenerator parent)
            {
                EntryPoint = entryPoint;
                Parent = parent;

                IndexParameter = null;
                IndexRegister = null;
                LengthRegister = null;
            }

            /// <summary>
            /// Returns the associated entry point.
            /// </summary>
            public EntryPoint EntryPoint { get; }

            /// <summary>
            /// Returns an the main index parameter.
            /// </summary>
            public Parameter IndexParameter { get; private set; }

            /// <summary>
            /// Returns the main index register.
            /// </summary>
            public StructureRegister IndexRegister { get; private set; }

            /// <summary>
            /// Returns the length register of implicitly grouped kernels.
            /// </summary>
            public StructureRegister LengthRegister { get; private set; }

            /// <summary>
            /// Returns the associated register allocator.
            /// </summary>
            public PTXKernelFunctionGenerator Parent { get; }

            /// <summary cref="PTXCodeGenerator.IParameterSetupLogic.HandleIntrinsicParameter(int, Parameter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Register HandleIntrinsicParameter(int parameterOffset, Parameter parameter)
            {
                IndexRegister = Parent.Allocate(parameter) as StructureRegister;
                IndexParameter = parameter;

                if (!EntryPoint.IsGroupedIndexEntry)
                {
                    // This is an implicitly grouped kernel that needs
                    // boundary information to avoid out-of-bounds dispatches
                    LengthRegister = Parent.AllocateType(parameter.ParameterType) as StructureRegister;
                    Debug.Assert(LengthRegister != null, "Invalid length register");
                }
                return LengthRegister;
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

            var parameterLogic = new KernelParameterSetupLogic(entryPoint, this);
            var parameters = SetupParameters(ref parameterLogic, 1);
            Builder.AppendLine();
            Builder.AppendLine(")");
            SetupKernelSpecialization(entryPoint.Specialization);
            Builder.AppendLine("{");

            // Build memory allocations
            PrepareCodeGeneration();
            var allocations = SetupLocalAllocations(allocas);
            SetupSharedAllocations(sharedAllocations, allocations);
            var registerOffset = Builder.Length;

            // Build param bindings and local memory variables
            BindAllocations(allocations);
            BindParameters(parameters);

            // Setup kernel indices
            SetupKernelIndex(
                entryPoint,
                parameterLogic.IndexParameter,
                parameterLogic.IndexRegister,
                parameterLogic.LengthRegister);

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
        /// <param name="dimension">The parameter dimension.</param>
        /// <param name="targetRegister">The primitive target register to write to.</param>
        /// <param name="boundsRegister">The associated bounds register.</param>
        private void EmitImplicitKernelIndex(
            int dimension,
            PrimitiveRegister targetRegister,
            PrimitiveRegister boundsRegister)
        {
            var aReg = MoveFromIntrinsicRegister(PTXRegisterKind.Ctaid, dimension);
            var bReg = MoveFromIntrinsicRegister(PTXRegisterKind.NtId, dimension);
            var cReg = MoveFromIntrinsicRegister(PTXRegisterKind.Tid, dimension);

            using (var command = BeginCommand(Instructions.IndexFMAOperationLo))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(aReg);
                command.AppendArgument(bReg);
                command.AppendArgument(cReg);
            }

            FreeRegister(aReg);
            FreeRegister(bReg);
            FreeRegister(cReg);

            using (var predicateScope = new PredicateScope(this))
            {
                using (var command = BeginCommand(
                    Instructions.GetCompareOperation(
                        CompareKind.GreaterEqual,
                        ArithmeticBasicValueType.Int32)))
                {
                    command.AppendArgument(predicateScope.PredicateRegister);
                    command.AppendArgument(targetRegister);
                    command.AppendArgument(boundsRegister);
                }

                Command(
                    Instructions.ReturnOperation,
                    predicateScope.GetConfiguration(true));
            }
        }

        /// <summary>
        /// Emits an explicit kernel index computation.
        /// </summary>
        /// <param name="targetGridIdx">The target grid index register.</param>
        /// <param name="targetGroupIdx">The targhet group index register.</param>
        /// <param name="dimension">The dimension index.</param>
        private void EmitExplicitKernelIndex(
            PrimitiveRegister targetGridIdx,
            PrimitiveRegister targetGroupIdx,
            int dimension)
        {
            MoveFromIntrinsicRegister(targetGridIdx, PTXRegisterKind.Ctaid, dimension);
            MoveFromIntrinsicRegister(targetGroupIdx, PTXRegisterKind.Tid, dimension);
        }

        /// <summary>
        /// Setups the current kernel indices.
        /// </summary>
        /// <param name="entryPoint">The current entry point.</param>
        /// <param name="indexParameter">The main kernel index parameter.</param>
        /// <param name="indexRegister">The main kernel index register.</param>
        /// <param name="lengthRegister">The length register of implicitly grouped kernels.</param>
        private void SetupKernelIndex(
            EntryPoint entryPoint,
            Parameter indexParameter,
            StructureRegister indexRegister,
            StructureRegister lengthRegister)
        {
            if (entryPoint.IsGroupedIndexEntry)
            {
                var gridRegisters = indexRegister.Children[0] as StructureRegister;
                var groupRegisters = indexRegister.Children[1] as StructureRegister;

                for (int i = 0, e = (int)entryPoint.IndexType - (int)IndexType.Index3D; i < e; ++i)
                {
                    EmitExplicitKernelIndex(
                        gridRegisters.Children[i] as PrimitiveRegister,
                        groupRegisters.Children[i] as PrimitiveRegister,
                        i);
                }
            }
            else
            {
                for (int i = 0, e = (int)entryPoint.IndexType; i < e; ++i)
                {
                    EmitImplicitKernelIndex(
                        i,
                        indexRegister.Children[i] as PrimitiveRegister,
                        lengthRegister.Children[i] as PrimitiveRegister);
                }
            }
        }

        #endregion
    }
}
