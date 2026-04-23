// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueClass.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR;

/// <summary>
/// Represents the class of a single IR value.
/// </summary>
enum ValueClass : int
{
    /// <summary>
    /// An invalid value class.
    /// </summary>
    Invalid = -1,

    /// <summary>
    /// A pure value without side effects.
    /// </summary>
    Pure,

    /// <summary>
    /// A basic block value hosting lists of values to be executed sequentially.
    /// </summary>
    BasicBlock,

    /// <summary>
    /// A callable method (may even be an entry point of a module).
    /// </summary>
    Method,

    /// <summary>
    /// A type value.
    /// </summary>
    Type,

    /// <summary>
    /// A global value.
    /// </summary>
    Global,

    /// <summary>
    /// A whole module hosting types, methods, and globals.
    /// </summary>
    Module
}

static partial class ValueKinds
{
    /// <summary>
    /// Returns the number of different value classes.
    /// </summary>
    public const int NumValueClasses = 5;
}
