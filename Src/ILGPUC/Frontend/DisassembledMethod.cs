// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
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

/// <summary>
/// Represents an exception catch clause.
/// </summary>
/// <param name="TryStart">The start index of the try block.</param>
/// <param name="TryLength">The length of the try block in instructions.</param>
/// <param name="HandlerStart">The start index of the handler block.</param>
/// <param name="HandlerLength">The length of the handler block in instructions.</param>
/// <param name="FilterBlock">The optional filter block clause index.</param>
/// <param name="CatchType">The optional exception catch type.</param>
sealed record CatchClause(
    int TryStart,
    int TryLength,
    int HandlerStart,
    int HandlerLength,
    int? FilterBlock = null,
    Type? CatchType = null)
{
    public bool IsFilter => FilterBlock.HasValue;
}

/// <summary>
/// Represents a finally clause.
/// </summary>
/// <param name="TryStart">The start index of the try block.</param>
/// <param name="TryLength">The length of the try block in instructions.</param>
/// <param name="FinallyStart">The start index of the finally block.</param>
/// <param name="FinallyLength">The length of the finally block.</param>
sealed record FinallyClause(
    int TryStart,
    int TryLength,
    int FinallyStart,
    int FinallyLength);

/// <summary>
/// The disassembled method flags.
/// </summary>
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

    /// <summary>
    /// Marks methods as intrinsics.
    /// </summary>
    InIntrinsic = 1 << 3,
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

    // 0 = sequence points not yet attached; 1 = attached.
    // Flipped exactly once by the first ILFrontend that successfully runs
    // sequence-point attachment for this body; later frontends sharing the
    // same cached body see 1 and skip the redundant per-instruction merges.
    private int _locationsAttached;

    /// <summary>
    /// Atomically claims the right to attach sequence-point locations to
    /// this body's instructions. Returns <see langword="true"/> only on the
    /// first call across all threads; subsequent calls return
    /// <see langword="false"/> and the caller must skip attachment.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="ILFrontend"/> when a <see cref="DisassembledMethod"/>
    /// is reused across multiple compilations through
    /// <see cref="ILFrontendCache"/>: location merging is value-deterministic
    /// for the same PDB so re-running it would be wasted work, and it would
    /// also race other frontends concurrently mutating the same instructions.
    /// </remarks>
    public bool TryClaimLocationAttachment() =>
        System.Threading.Interlocked.CompareExchange(
            ref _locationsAttached, 1, 0) == 0;

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
    /// Returns true if this is a compiler internal intrinsic.
    /// </summary>
    public bool IsIntrinsic =>
        (Flags & DisassembledMethodFlags.InIntrinsic) != DisassembledMethodFlags.None;

    /// <summary>
    /// Returns true if this is a function that is known by the compiler in general.
    /// </summary>
    public bool IsBuiltIn => IsLauncher || IsIntrinsic;

    /// <summary>
    /// Returns an instruction enumerator.
    /// </summary>
    /// <returns>An instruction enumerator.</returns>
    public ReadOnlySpan<ILInstruction>.Enumerator GetEnumerator() =>
        Instructions.GetEnumerator();
}
