// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Immutable;

namespace ILGPU.IR
{
    /// <summary>
    /// A uniform value type representing an exported <see cref="Value"/>
    /// from an <see cref="IRContext"/>.
    /// </summary>
    /// <param name="Method">Corresponds to <see cref="Value.Method"/></param>
    /// <param name="BasicBlock">Corresponds to <see cref="Value.BasicBlock"/></param>
    /// <param name="Id">Corresponds to <see cref="Node.Id"/></param>
    /// <param name="ValueKind">Corresponds to <see cref="Value.ValueKind"/></param>
    /// <param name="Type">Corresponds to <see cref="Value.Type"/></param>
    /// <param name="Nodes">Corresponds to <see cref="Value.Nodes"/></param>
    /// <param name="Data">Extra data specific to this value's kind or instance</param>
    /// <param name="Tag">Extra data specific to this value's kind or instance</param>
    public record struct IRValue(
        long Method,
        long BasicBlock,
        long Id,
        ValueKind ValueKind,
        long Type,
        ImmutableArray<long> Nodes,
        long Data, string? Tag)
    {
    }
}
