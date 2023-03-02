// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR.Values;
using ILGPU.Runtime.Velocity;

namespace ILGPU.Backends.Velocity
{
    partial class VelocityCodeGenerator<TILEmitter, TVerifier>
    {
        /// <inheritdoc />
        public abstract void GenerateCode(ReturnTerminator returnTerminator);

        /// <inheritdoc />
        public void GenerateCode(UnconditionalBranch branch)
        {
            // Create a branch if required
            var branchBuilder = CreateBranchBuilder(branch.BasicBlock);
            branchBuilder.RecordBranchTarget(branch.Target, () =>
            {
                // Pass the current mask
                Emitter.Emit(LocalOperation.Load, GetBlockMask(branch.BasicBlock));
            });
            branchBuilder.EmitBranch();
        }

        /// <inheritdoc />
        public void GenerateCode(IfBranch branch)
        {
            // Get current mask
            var currentMask = GetBlockMask(branch.BasicBlock);

            // Load condition and convert it into a lane mask
            Load(branch.Condition);
            Emitter.EmitCall(Instructions.ToMaskOperation32);

            var tempMask = Emitter.DeclareLocal(typeof(VelocityLaneMask));
            Emitter.Emit(LocalOperation.Store, tempMask);

            // Create a new branch builder
            var branchBuilder = CreateBranchBuilder(branch.BasicBlock);

            // Adjust the true mask
            branchBuilder.RecordBranchTarget(branch.TrueTarget, () =>
            {
                // Intersect with the current mask
                Emitter.Emit(LocalOperation.Load, tempMask);
                IntersectWithMask(currentMask);
            });

            // Intersect negated with the current mask
            branchBuilder.RecordBranchTarget(branch.FalseTarget, () =>
            {
                // Adjust the current mask
                Emitter.Emit(LocalOperation.Load, tempMask);
                Emitter.EmitCall(Instructions.NegateLaneMask);
                IntersectWithMask(currentMask);
            });

            // Emit branch (if required)
            branchBuilder.EmitBranch();
        }

        /// <inheritdoc />
        public void GenerateCode(SwitchBranch branch)
        {
            // Get current mask
            var currentMask = GetBlockMask(branch.BasicBlock);

            // Create a new branch builder
            var branchBuilder = CreateBranchBuilder(branch.BasicBlock);

            // Check lower bounds: case < 0
            Load(branch.Condition);
            Emitter.EmitConstant(0);
            Emitter.EmitCall(Instructions.GetConstValueOperation32(
                VelocityWarpOperationMode.I));
            Emitter.EmitCall(Instructions.GetCompareOperation32(
                CompareKind.LessThan,
                VelocityWarpOperationMode.I));
            Emitter.EmitCall(Instructions.ToMaskOperation32);

            // Check upper bounds: case >= num cases
            Load(branch.Condition);
            Emitter.EmitConstant(branch.NumCasesWithoutDefault);
            Emitter.EmitCall(Instructions.GetConstValueOperation32(
                VelocityWarpOperationMode.I));
            Emitter.EmitCall(Instructions.GetCompareOperation32(
                CompareKind.GreaterEqual,
                VelocityWarpOperationMode.I));
            Emitter.EmitCall(Instructions.ToMaskOperation32);

            // Store unified branch mask
            Emitter.EmitCall(Instructions.UnifyLanesMask);
            IntersectWithMask(currentMask);

            var outOfBoundsMask = Emitter.DeclareLocal(typeof(VelocityLaneMask));
            Emitter.Emit(LocalOperation.Store, outOfBoundsMask);

            // Record branch to the default block
            branchBuilder.RecordBranchTarget(branch.DefaultBlock, () =>
            {
                Emitter.Emit(LocalOperation.Load, outOfBoundsMask);
            });

            // Adjust masks for each target
            for (int i = 0; i < branch.NumCasesWithoutDefault; ++i)
            {
                // Check whether the conditional selector is equal to the current case
                Load(branch.Condition);
                Emitter.EmitConstant(i);
                Emitter.EmitCall(Instructions.GetConstValueOperation32(
                    VelocityWarpOperationMode.I));
                Emitter.EmitCall(Instructions.GetCompareOperation32(
                    CompareKind.Equal,
                    VelocityWarpOperationMode.I));
                Emitter.EmitCall(Instructions.ToMaskOperation32);

                // Store the current mask
                var currentCaseMask = Emitter.DeclareLocal(typeof(VelocityLaneMask));
                IntersectWithMask(currentMask);
                Emitter.Emit(LocalOperation.Store, currentCaseMask);

                // Record branch
                branchBuilder.RecordBranchTarget(branch.GetCaseTarget(i), () =>
                {
                    Emitter.Emit(LocalOperation.Load, currentCaseMask);
                });
            }

            // Emit branch if necessary
            branchBuilder.EmitBranch();
        }
    }
}
