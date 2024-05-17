// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRMethod.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Immutable;

namespace ILGPU.IR
{
    /// <summary>
    /// A uniform type representing an exported
    /// <see cref="Method"/> from an <see cref="IRContext"/>.
    /// </summary>
    /// <param name="Id">Corresponds to <see cref="Node.Id"/></param>
    /// <param name="Name">Corresponds to <see cref="Method.Name"/></param>
    /// <param name="ReturnType">Corresponds to <see cref="Method.ReturnType"/></param>
    /// <param name="Blocks">Corresponds to <see cref="Method.Blocks"/></param>
    public record struct IRMethod(long Id, string Name,
        long ReturnType, ImmutableArray<long> Blocks)
    {
    }
}
