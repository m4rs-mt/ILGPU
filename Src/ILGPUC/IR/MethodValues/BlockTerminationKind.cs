// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BlockTerminationKind.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.MethodValues;

/// <summary>
/// Represents the termination kind of a basic block.
/// </summary>
enum BlockTerminationKind
{
    /// <summary>
    /// Block is not yet finalized (during building phase).
    /// </summary>
    Pending,

    /// <summary>
    /// Unconditional branch to a single successor.
    /// LastValue is unused (UndefinedValue).
    /// Successors contains exactly one target block.
    /// </summary>
    Unconditional,

    /// <summary>
    /// Conditional branch based on a boolean condition.
    /// LastValue is the condition (must be Int1/bool type).
    /// Successors[0] = TrueTarget, Successors[1] = FalseTarget.
    /// </summary>
    Conditional,

    /// <summary>
    /// Switch branch based on an integer condition.
    /// LastValue is the switch value (must be integer type).
    /// Successors[0] = DefaultTarget, Successors[1..N] = CaseTargets.
    /// </summary>
    Switch,

    /// <summary>
    /// Return from the method.
    /// LastValue is the return value (or UndefinedValue for void methods).
    /// Successors is empty.
    /// </summary>
    Return
}
