﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System.Diagnostics;
using System.Text;

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

                IndexRegister = null;
                LengthRegister = null;
            }

            /// <summary>
            /// Returns the associated entry point.
            /// </summary>
            public EntryPoint EntryPoint { get; }

            /// <summary>
            /// Returns the main index register.
            /// </summary>
            public Register IndexRegister { get; private set; }

            /// <summary>
            /// Returns the length register of implicitly grouped kernels.
            /// </summary>
            public Register LengthRegister { get; private set; }

            /// <summary>
            /// Returns the associated register allocator.
            /// </summary>
            public PTXKernelFunctionGenerator Parent { get; }

            /// <summary>
            /// Updates index and length registers.
            /// </summary>
            public Register HandleIntrinsicParameter(
                int parameterOffset,
                Parameter parameter)
            {
                IndexRegister = Parent.Allocate(parameter);

                if (!EntryPoint.IsExplicitlyGrouped)
                {
                    // This is an implicitly grouped kernel that needs
                    // boundary information to avoid out-of-bounds dispatches
                    LengthRegister = Parent.AllocateType(parameter.ParameterType);
                    parameter.AssertNotNull(LengthRegister);
                }
                return LengthRegister;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Creates a new PTX kernel generator.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="method">The current method.</param>
        /// <param name="allocas">All local allocas.</param>
        public PTXKernelFunctionGenerator(
            in GeneratorArgs args,
            Method method,
            Allocas allocas)
            : base(args, method, allocas)
        {
            EntryPoint = args.EntryPoint;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated entry point.
        /// </summary>
        public EntryPoint EntryPoint { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a function declaration in PTX code.
        /// </summary>
        public override void GenerateHeader(StringBuilder builder)
        {
            // Generate global dynamic shared memory allocation information
            if (EntryPoint.SharedMemory.HasDynamicMemory)
            {
                builder.Append(".extern .shared .align 1 .b8 ");
                builder.Append(DynamicSharedMemoryAllocationName);
                builder.AppendLine("[];");
            }
        }

        /// <summary>
        /// Generates PTX code.
        /// </summary>
        public override void GenerateCode()
        {
            Builder.AppendLine();
            Builder.Append(".visible .entry ");
            Builder.Append(PTXCompiledKernel.EntryName);
            Builder.AppendLine("(");

            var parameterLogic = new KernelParameterSetupLogic(EntryPoint, this);
            var parameters = SetupParameters(
                Builder,
                ref parameterLogic,
                EntryPoint.IsExplicitlyGrouped ? 0 : 1);
            Builder.AppendLine();
            Builder.AppendLine(")");
            SetupKernelSpecialization(EntryPoint.Specialization);
            Builder.AppendLine("{");

            // Build memory allocations
            PrepareCodeGeneration();
            var allocations = SetupAllocations();
            var registerOffset = Builder.Length;

            // Build param bindings and local memory variables
            BindAllocations(allocations);
            BindParameters(parameters);

            // Setup kernel indices
            SetupKernelIndex(
                parameterLogic.IndexRegister,
                parameterLogic.LengthRegister);

            GenerateCodeInternal(registerOffset);
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
        /// <param name="targetRegister">
        /// The primitive target register to write to.
        /// </param>
        /// <param name="boundsRegister">The associated bounds register.</param>
        private void EmitImplicitKernelIndex(
            int dimension,
            PrimitiveRegister targetRegister,
            PrimitiveRegister boundsRegister)
        {
            var aReg = MoveFromIntrinsicRegister(PTXRegisterKind.Ctaid, dimension);
            var bReg = MoveFromIntrinsicRegister(PTXRegisterKind.NtId, dimension);
            var cReg = MoveFromIntrinsicRegister(PTXRegisterKind.Tid, dimension);

            using (var command = BeginCommand(PTXInstructions.IndexFMAOperationLo))
            {
                command.AppendArgument(targetRegister);
                command.AppendArgument(aReg);
                command.AppendArgument(bReg);
                command.AppendArgument(cReg);
            }

            FreeRegister(aReg);
            FreeRegister(bReg);
            FreeRegister(cReg);

            using var predicateScope = new PredicateScope(this);
            using (var command = BeginCommand(
                PTXInstructions.GetCompareOperation(
                CompareKind.GreaterEqual,
                CompareFlags.None,
                ArithmeticBasicValueType.Int32)))
            {
                command.AppendArgument(predicateScope.PredicateRegister);
                command.AppendArgument(targetRegister);
                command.AppendArgument(boundsRegister);
            }

            Command(
                PTXInstructions.ReturnOperation,
                predicateScope.GetConfiguration(true));
        }

        /// <summary>
        /// Setups the current kernel indices.
        /// </summary>
        /// <param name="indexRegister">The main kernel index register.</param>
        /// <param name="lengthRegister">
        /// The length register of implicitly grouped kernels.
        /// </param>
        private void SetupKernelIndex(Register indexRegister, Register lengthRegister)
        {
            // Skip this step for grouped kernels
            if (EntryPoint.IsExplicitlyGrouped)
                return;
            if (EntryPoint.IndexType == IndexType.Index1D)
            {
                EmitImplicitKernelIndex(
                    0,
                    indexRegister as PrimitiveRegister,
                    lengthRegister as PrimitiveRegister);
            }
            else
            {
                var compoundIndex = indexRegister as CompoundRegister;
                var compoundLength = lengthRegister as CompoundRegister;
                for (int i = 0, e = (int)EntryPoint.IndexType; i < e; ++i)
                {
                    EmitImplicitKernelIndex(
                        i,
                        compoundIndex.Children[i] as PrimitiveRegister,
                        compoundLength.Children[i] as PrimitiveRegister);
                }
            }
        }

        #endregion
    }
}
