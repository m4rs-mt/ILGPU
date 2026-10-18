// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CollectiveDiagnostics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Roslyn.Analysis;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Creates the errors with which the lowering passes reject a collective that
/// kernel code cannot use: they are reported as
/// <see cref="KernelDiagnostic.UnsupportedConstructId"/> at the location of the
/// collective, under the name of the <c>Warp</c> or <c>Group</c> method that
/// kernel code calls.
/// </summary>
static class CollectiveDiagnostics
{
    /// <summary>
    /// Returns the name of the <c>Warp</c> or <c>Group</c> method that kernel
    /// code calls for the given collective.
    /// </summary>
    /// <param name="collective">The collective.</param>
    /// <returns>The name of the method, such as <c>Warp.Reduce</c>.</returns>
    public static string GetName(ThreadValue collective) =>
        collective switch
        {
            Broadcast broadcast => broadcast.Kind == BroadcastKind.GroupLevel
                ? "Group.Broadcast"
                : "Warp.Broadcast",
            Shuffle shuffle => shuffle.Kind == ShuffleKind.Generic
                ? "Warp.Shuffle"
                : $"Warp.Shuffle{shuffle.Kind}",
            WarpReduce reduce => $"Warp.{reduce.Kind}",
            WarpScan scan => $"Warp.{scan.Kind}Scan",
            GroupReduce reduce => $"Group.{reduce.Kind}",
            GroupScan scan => $"Group.{scan.Kind}Scan",
            _ => collective.ToString(),
        };

    /// <summary>
    /// Creates the error for a collective whose operation is not one that the
    /// collective can compute, such as a custom operation or an operation on
    /// structures.
    /// </summary>
    /// <param name="collective">The collective.</param>
    /// <returns>The error to throw.</returns>
    public static KernelCompilationException UnsupportedOperation(
        ThreadValue collective) =>
        new(
            KernelDiagnostic.UnsupportedConstructId,
            string.Format(
                ErrorMessages.KernelDiagnosticUnsupportedCollectiveOperation,
                GetName(collective)),
            collective.Location);

    /// <summary>
    /// Creates the error for a collective on a value of a type that it cannot
    /// exchange or combine, such as a pointer or a structure with a pointer field.
    /// </summary>
    /// <param name="collective">The collective.</param>
    /// <param name="type">The unsupported type of the value or of its field.</param>
    /// <returns>The error to throw.</returns>
    public static KernelCompilationException UnsupportedType(
        ThreadValue collective,
        TypeValue? type) =>
        new(
            KernelDiagnostic.UnsupportedConstructId,
            string.Format(
                ErrorMessages.KernelDiagnosticUnsupportedCollectiveType,
                GetName(collective),
                TypeDisplay.Format(type)),
            collective.Location);
}
