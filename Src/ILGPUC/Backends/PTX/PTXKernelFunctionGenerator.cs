// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using ILGPUC.Backends.EntryPoints;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.Values;
using System;
using System.Text;

namespace ILGPUC.Backends.PTX;

/// <summary>
/// Represents a function generator for main kernel functions.
/// </summary>
/// <param name="args">The generation arguments.</param>
/// <param name="method">The current method.</param>
sealed partial class PTXKernelFunctionGenerator(
    PTXCodeGenerator.GeneratorArgs args,
    Method method) : PTXCodeGenerator(args, method)
{
    #region Nested Types

    private struct KernelParameterSetupLogic(
        EntryPoint entryPoint,
        PTXKernelFunctionGenerator parent) : IParameterSetupLogic
    {
        /// <summary>
        /// Returns the main index register.
        /// </summary>
        public Register? IndexRegister { get; private set; } = null;

        /// <summary>
        /// Returns the length register of implicitly grouped kernels.
        /// </summary>
        public Register? LengthRegister { get; private set; } = null;

        /// <summary>
        /// Updates index and length registers.
        /// </summary>
        public Register? HandleIntrinsicParameter(
            int parameterOffset,
            Parameter parameter)
        {
            IndexRegister = parent.Allocate(parameter);

            if (!entryPoint.IsExplicitlyGrouped)
            {
                // This is an implicitly grouped kernel that needs
                // boundary information to avoid out-of-bounds dispatches
                LengthRegister = parent.AllocateType(parameter.ParameterType);
                parameter.AssertNotNull(LengthRegister);
            }
            return LengthRegister;
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated entry point.
    /// </summary>
    public EntryPoint EntryPoint { get; } = args.EntryPoint;

    /// <summary>
    /// Returns program allocations.
    /// </summary>
    public Backend.Allocations Allocations { get; } = args.Allocations;

    #endregion

    #region Methods

    /// <summary>
    /// Generates a function declaration in PTX code.
    /// </summary>
    public override void GenerateHeader(StringBuilder builder)
    {
        // Generate global dynamic shared memory allocation information
        if (Allocations.SharedMemoryMode < CompiledKernelSharedMemoryMode.Dynamic)
            return;

        // Determine program wide allocation information of all dynamic allocations

        // Get global alignment information
        int sharedAlignmentInBytes = PTXBackend.DefaultSharedMemoryAlignment;
        foreach (var alloca in Allocations.DynamicAllocations)
        {
            sharedAlignmentInBytes = Math.Max(
                sharedAlignmentInBytes,
                PointerAlignments.GetAllocaAlignment(alloca.Alloca));
        }
        sharedAlignmentInBytes = Math.Min(
            sharedAlignmentInBytes,
            PTXBackend.DefaultGlobalMemoryAlignment);

        // Use the proper alignment that is compatible with all types
        builder.Append(".extern .shared .align ");
        builder.Append(sharedAlignmentInBytes);
        builder.Append(" .b8 ");
        builder.Append(DynamicSharedMemoryAllocationName);
        builder.AppendLine("[];");
    }

    /// <summary>
    /// Generates PTX code.
    /// </summary>
    public override void GenerateCode()
    {
        Builder.AppendLine();
        Builder.Append(".visible .entry ");
        Builder.Append(EntryPoint.Name);
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
    /// Setups the current kernel indices.
    /// </summary>
    /// <param name="indexRegister">The main kernel index register.</param>
    /// <param name="lengthRegister">
    /// The length register of implicitly grouped kernels.
    /// </param>
    private void SetupKernelIndex(Register? indexRegister, Register? lengthRegister)
    {
        // Skip this step for grouped kernels
        if (EntryPoint.IsExplicitlyGrouped)
            return;

        var boundsRegister = indexRegister.AsNotNullCast<PrimitiveRegister>();
        var targetRegister = lengthRegister.AsNotNullCast<PrimitiveRegister>();

        var aReg = MoveFromIntrinsicRegister(PTXRegisterKind.Ctaid, 0);
        var bReg = MoveFromIntrinsicRegister(PTXRegisterKind.NtId, 0);
        var cReg = MoveFromIntrinsicRegister(PTXRegisterKind.Tid, 0);

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

    #endregion
}
