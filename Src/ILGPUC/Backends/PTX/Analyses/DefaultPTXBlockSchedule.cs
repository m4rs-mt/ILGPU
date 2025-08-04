// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DefaultPTXBlockSchedule.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses.ControlFlowDirection;
using ILGPUC.IR.Analyses.TraversalOrders;

namespace ILGPUC.Backends.PTX.Analyses;

/// <summary>
/// Represents a default PTX-specific block schedule.
/// </summary>
/// <remarks>
/// Constructs a new PTX block schedule.
/// </remarks>
/// <param name="blocks">The underlying block collection.</param>
sealed class DefaultPTXBlockSchedule(
    in BasicBlockCollection<ReversePostOrder, Forwards> blocks) :
    PTXBlockSchedule<ReversePostOrder, Forwards>(blocks)
{
    #region Methods

    /// <summary>
    /// Returns false to ensure that all branches will be generated.
    /// </summary>
    /// <returns>False.</returns>
    public override bool IsImplicitSuccessor(
        BasicBlock source,
        BasicBlock successor) => false;

    /// <summary>
    /// Returns true to ensure that all blocks will receive a branch target.
    /// </summary>
    /// <returns>True.</returns>
    public override bool NeedBranchTarget(BasicBlock block) => true;

    #endregion
}

partial class PTXBlockScheduleExtensions
{
    /// <summary>
    /// Creates a new default block schedule using the given blocks.
    /// </summary>
    /// <param name="blocks">The input blocks.</param>
    /// <returns>The created block schedule.</returns>
    public static PTXBlockSchedule CreateDefaultPTXSchedule(
        this BasicBlockCollection<ReversePostOrder, Forwards> blocks) =>
        new DefaultPTXBlockSchedule(blocks);
}
