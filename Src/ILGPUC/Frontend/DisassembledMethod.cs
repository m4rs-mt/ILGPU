// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DisassembledMethod.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System;
using System.Reflection;

namespace ILGPUC.Frontend;

record CatchClause(
    int TryStart,
    int TryLength,
    int HandlerOffset,
    int HandlerLength,
    int? FilterBlock = null,
    Type? CatchType = null)
{
    public bool IsFilter => FilterBlock.HasValue;
}

record FinallyClause(
    int TryStart,
    int TryLength,
    int FinallyOffset,
    int FinallyLength);

[Flags]
enum DisassembledMethodFlags
{
    /// <summary>
    /// No flags
    /// </summary>
    None = 0,

    /// <summary>
    /// Delay code generation for this method.
    /// </summary>
    DelayCodeGeneration = 1 << 0,

    /// <summary>
    /// Marks methods to be replaced with a launcher.
    /// </summary>
    ReplaceWithLauncher = 1 << 1,

    /// <summary>
    /// Marks methods acting as real launchers.
    /// </summary>
    IsLauncher = 1 << 2,
}

/// <summary>
/// Represents a disassembled method.
/// </summary>
/// <remarks>Members of this class are not thread safe.</remarks>
sealed class DisassembledMethod(
    MethodBase method,
    DisassembledMethodFlags flags,
    ReadOnlyMemory<ILInstruction> instructions,
    ReadOnlyMemory<CatchClause> catchClauses,
    ReadOnlyMemory<FinallyClause> finallyClauses,
    int maxStackSize)
{
    private readonly ReadOnlyMemory<ILInstruction> _instructions = instructions;

    /// <summary>
    /// Returns method that was disassembled.
    /// </summary>
    public MethodBase Method { get; } = method;

    /// <summary>
    /// Returns associated flags.
    /// </summary>
    public DisassembledMethodFlags Flags { get; } = flags;

    /// <summary>
    /// Returns the first disassembled instruction.
    /// </summary>
    public ILInstruction FirstInstruction => Instructions[0];

    /// <summary>
    /// Returns the first location of this function.
    /// </summary>
    public Location FirstLocation => FirstInstruction.Location;

    /// <summary>
    /// Returns the disassembled instructions.
    /// </summary>
    public ReadOnlySpan<ILInstruction> Instructions => _instructions.Span;

    /// <summary>
    /// Returns all catch clauses.
    /// </summary>
    public ReadOnlyMemory<CatchClause> CatchClauses { get; } = catchClauses;

    /// <summary>
    /// Returns all finally clauses.
    /// </summary>
    public ReadOnlyMemory<FinallyClause> FinallyClauses { get; } = finallyClauses;

    /// <summary>
    /// Returns the maximum stack size.
    /// </summary>
    public int MaxStackSize { get; } = maxStackSize;

    /// <summary>
    /// Returns the number of instructions.
    /// </summary>
    public int Count => Instructions.Length;

    /// <summary>
    /// Returns the instruction at the given index.
    /// </summary>
    /// <param name="index">The instruction index.</param>
    /// <returns>The instruction at the given index.</returns>
    public ILInstruction this[int index] => Instructions[index];

    /// <summary>
    /// Returns true if this is a low-level launcher.
    /// </summary>
    public bool IsLauncher =>
        (Flags & DisassembledMethodFlags.IsLauncher) != DisassembledMethodFlags.None;

    /// <summary>
    /// Returns an instruction enumerator.
    /// </summary>
    /// <returns>An instruction enumerator.</returns>
    public ReadOnlySpan<ILInstruction>.Enumerator GetEnumerator() =>
        Instructions.GetEnumerator();
}
