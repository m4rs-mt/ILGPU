// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Backends.Velocity.Analyses;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Reflection.Emit;
using Loop = ILGPU.IR.Analyses.Loops<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>.Node;

namespace ILGPU.Backends.Velocity
{
    partial class VelocityCodeGenerator<TILEmitter>
    {
        #region Branch Builder

        /// <summary>
        /// Helps building branches by taking branch targets and masks into account.
        /// </summary>
        protected sealed class BranchBuilder
        {
            private readonly VelocityCodeGenerator<TILEmitter> codeGenerator;

            private readonly BasicBlock current;
            private readonly ILLocal currentMask;

            private readonly bool isBackEdgeBlock;

            private readonly Loops<ReversePostOrder, Forwards>.Node? currentLoop;
            private readonly PhiBindings.PhiBindingCollection? phiBindings;
            private InlineList<(BasicBlock Target, Action Condition)> targets;

            public BranchBuilder(
                VelocityCodeGenerator<TILEmitter> parent,
                BasicBlock currentBlock)
            {
                codeGenerator = parent;
                current = currentBlock;
                currentMask = parent.GetBlockMask(current);

                isBackEdgeBlock = Masks.IsBackEdgeBlock(currentBlock);

                Masks.TryGetLoop(currentBlock, out currentLoop);
                phiBindings = codeGenerator.phiBindings.TryGetBindings(
                    current,
                    out var bindings) ? bindings : null;
                targets = InlineList<(BasicBlock, Action)>
                    .Create(currentBlock.Successors.Length);
            }

            public VelocityMasks<TILEmitter> Masks => codeGenerator.masks;
            public TILEmitter Emitter => codeGenerator.Emitter;
            public VelocityTargetSpecializer Specializer => codeGenerator.Specializer;

            /// <summary>
            /// Records a branch target.
            /// </summary>
            /// <param name="target">The target block to branch to.</param>
            /// <param name="passMask">The pass mask action.</param>
            public void RecordBranchTarget(BasicBlock target, Action passMask)
            {
                // Check for a jump into a (possibly different) loop
                if (Masks.IsHeader(target, out var targetLoop))
                {
                    // Check for a jump backwards
                    if (isBackEdgeBlock)
                    {
                        // Pass the current mask
                        passMask();

                        if (target != current)
                        {
                            // We are branching to our loop header and need to update our
                            // loop mask of the header we are branching back to
                            codeGenerator.UnifyWithMaskOf(target, keepOnStack: false);
                        }
                        else // Store the mask directly
                        {
                            Emitter.Emit(LocalOperation.Store, currentMask);
                        }

                        targets.Add((target, () =>
                        {
                            Emitter.Emit(LocalOperation.Load, currentMask);
                            var loopMask = Masks.GetLoopMask(targetLoop);
                            Emitter.Emit(LocalOperation.Load, loopMask);
                            Specializer.IntersectMask32(Emitter);

                            // Check for active masks of the target block to test whether
                            // we actually branch back to the loop header
                            Specializer.CheckForAnyActiveLaneMask(Emitter);
                        }));
                    }
                    else
                    {
                        // Pass the current mask
                        passMask();

                        if (target != current)
                        {
                            // We are branching forwards and need to pass the mask while
                            // unifying all lanes
                            codeGenerator.UnifyWithMaskOf(target, keepOnStack: true);
                        }
                        else // Store the mask directly
                        {
                            Emitter.Emit(OpCodes.Dup);
                            Emitter.Emit(LocalOperation.Store, currentMask);
                        }

                        // Set the actual loop mask
                        var loopMask = Masks.GetLoopMask(targetLoop);
                        Emitter.Emit(LocalOperation.Load, loopMask);
                        Specializer.UnifyMask32(Emitter);
                        Emitter.Emit(LocalOperation.Store, loopMask);

                        // Disable all loop body blocks
                        TryResetLoopBody(targetLoop);
                    }
                }
                else if (Masks.IsExit(target, out var isExitFor))
                {
                    // Check whether we leaving our loop at the moment
                    if (currentLoop is not null && isExitFor(currentLoop))
                    {
                        // Based on the ordering ensured by the VelocityBlockScheduling
                        // transformation, we know that the exit block cannot be reached
                        // from within the loop implicitly. Therefore, it is sufficient to
                        // check whether the unified header masks are equal to the current
                        // mask of the target loop. This means that all lanes have reached
                        // this point and we can branch to the exit block.

                        // Notify the loop mask that some lanes passed to this block left
                        var loopMask = Masks.GetLoopMask(currentLoop.AsNotNull());
                        passMask();
                        Emitter.Emit(OpCodes.Dup);
                        codeGenerator.DisableSpecificLanes(loopMask);

                        // Unify with the target mask to cause the lanes to be ready when
                        // we continue processing the exit block
                        codeGenerator.UnifyWithMaskOf(target, keepOnStack: false);

                        targets.Add((target, () =>
                        {
                            // Load loop mask to see whether we have any lanes left
                            Emitter.Emit(LocalOperation.Load, loopMask);

                            // Check whether all lane masks have been disabled
                            Specializer.CheckForNoActiveLaneMask(Emitter);
                        }));
                    }
                    else
                    {
                        // We are just branching forwards
                        passMask();
                        codeGenerator.UnifyWithMaskOf(target, keepOnStack: false);
                    }
                }
                else // Default case in which we do not change any loop state
                {
                    // Pass the current mask
                    passMask();

                    // We are branching forwards and need to pass the mask while unifying
                    // all lanes
                    codeGenerator.UnifyWithMaskOf(target, keepOnStack: false);
                }

                // Bind all phi values on this edge
                BindPhis(target, passMask);
            }

            /// <summary>
            /// Tries to reset masks for all loop members.
            /// </summary>
            /// <param name="targetLoop">The target loop to use.</param>
            private void TryResetLoopBody(Loop targetLoop)
            {
                foreach (var block in targetLoop.AllMembers)
                {
                    if (!targetLoop.ContainsExclusively(block))
                        continue;
                    codeGenerator.TryResetBlockLanes(block);
                }
            }

            /// <summary>
            /// Binds phi values flowing about a particular edge
            /// </summary>
            private void BindPhis(BasicBlock target, Action passMask)
            {
                // Check whether we have any bindings for this block
                if (!phiBindings.HasValue)
                    return;

                // Filter all phis flowing through this edge
                codeGenerator.BindPhis(phiBindings.Value, target, passMask);
            }

            /// <summary>
            /// Emits a branch if required.
            /// </summary>
            public void EmitBranch()
            {
                // Check for required branches
                if (targets.Count < 1)
                {
                    // Disable our lanes as we passed this block
                    codeGenerator.DisableLanesOf(current);

                    // Leave here as we do not require a single branch
                    return;
                }

                // Optimize for the most trivial case in which we have a single branch
                if (targets.Count == 1)
                {
                    var (target, condition) = targets[0];

                    // Emit our condition checks
                    condition();

                    if (target != current)
                    {
                        // Disable all lanes at this point
                        codeGenerator.DisableLanesOf(current);
                    }

                    // Jump to our target block
                    Emitter.Emit(OpCodes.Brtrue, codeGenerator.blockLookup[target]);
                }
                else
                {
                    // We have reached the most difficult case, in which we have to find
                    // the right block to jump to. However, we do not need any sorting
                    // of the targets as the basic block scheduling transformation took
                    // care of that -> targets are in the right order (descending)
                    for (int i = targets.Count - 1; i >= 0; --i)
                    {
                        var (target, condition) = targets[i];

                        // Declare temp label to branch to in case we need to branch
                        var tempLabel = Emitter.DeclareLabel();

                        // Emit our condition
                        condition();

                        // Skip the following branches in case of a failed check
                        Emitter.Emit(OpCodes.Brfalse, tempLabel);

                        if (target != current)
                        {
                            // Disable all lanes at this point
                            codeGenerator.DisableLanesOf(current);
                        }

                        // Jump to our target block
                        Emitter.Emit(OpCodes.Br, codeGenerator.blockLookup[target]);

                        // Emit the actual temp label to branch to in case to continue
                        // processing
                        Emitter.MarkLabel(tempLabel);
                    }

                    // Disable all lanes at this point before (potentially) leaving
                    codeGenerator.DisableLanesOf(current);
                }

            }
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        public abstract void GenerateCode(ReturnTerminator returnTerminator);

        /// <inheritdoc />
        public void GenerateCode(UnconditionalBranch branch)
        {
            // Create a branch if required
            var branchBuilder = CreateBranchBuilder(branch.BasicBlock);

            // Cache temp local
            var tempLocal = Emitter.DeclareLocal(Specializer.WarpType32);
            Emitter.Emit(LocalOperation.Load, GetBlockMask(branch.BasicBlock));
            Emitter.Emit(LocalOperation.Store, tempLocal);

            branchBuilder.RecordBranchTarget(branch.Target, () =>
            {
                // Pass the current mask
                Emitter.Emit(LocalOperation.Load, tempLocal);
            });
            branchBuilder.EmitBranch();
        }

        /// <inheritdoc />
        public void GenerateCode(IfBranch branch)
        {
            // Get current mask
            var currentMask = GetBlockMask(branch.BasicBlock);

            // Create a new branch builder
            var branchBuilder = CreateBranchBuilder(branch.BasicBlock);

            // Load condition
            Load(branch.Condition);
            Emitter.Emit(OpCodes.Dup);

            // Adjust the true mask
            var trueMask = Emitter.DeclareLocal(Specializer.WarpType32);
            IntersectWithMask(currentMask);
            Emitter.Emit(LocalOperation.Store, trueMask);

            // Intersect negated with the current mask
            var falseMask = Emitter.DeclareLocal(Specializer.WarpType32);
            Specializer.NegateMask32(Emitter);
            IntersectWithMask(currentMask);
            Emitter.Emit(LocalOperation.Store, falseMask);

            branchBuilder.RecordBranchTarget(branch.TrueTarget, () =>
            {
                Emitter.Emit(LocalOperation.Load, trueMask);
            });

            branchBuilder.RecordBranchTarget(branch.FalseTarget, () =>
            {
                Emitter.Emit(LocalOperation.Load, falseMask);
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
            Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
            Specializer.Compare32(
                Emitter,
                CompareKind.LessThan,
                VelocityWarpOperationMode.I);

            // Check upper bounds: case >= num cases
            Load(branch.Condition);
            Emitter.EmitConstant(branch.NumCasesWithoutDefault);
            Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
            Specializer.Compare32(
                Emitter,
                CompareKind.GreaterEqual,
                VelocityWarpOperationMode.I);

            // Store unified branch mask
            Specializer.UnifyMask32(Emitter);
            IntersectWithMask(currentMask);

            var outOfBoundsMask = Emitter.DeclareLocal(Specializer.WarpType32);
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
                Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
                Specializer.Compare32(
                    Emitter,
                    CompareKind.Equal,
                    VelocityWarpOperationMode.I);

                // Store the current mask
                var currentCaseMask = Emitter.DeclareLocal(Specializer.WarpType32);
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

        #endregion
    }
}
