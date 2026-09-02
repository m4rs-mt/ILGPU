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

using ILGPUC.Frontend.DebugInformation;
using ILGPUC.IR;
using System;
using System.Reflection;
using System.Threading;

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
/// <remarks>
/// Instances are shared across frontends through <see cref="ILFrontendCache"/>.
/// Apart from <see cref="EnsureLocationsAttached"/>, members of this class are not
/// thread safe.
/// </remarks>
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
    /// Serializes the one-time attachment of sequence-point locations.
    /// </summary>
    private readonly Lock _locationLock = new();

    /// <summary>
    /// Set once all sequence-point locations have been attached. Written inside
    /// <see cref="_locationLock"/> after the last location update, so any reader
    /// observing <see langword="true"/> also observes every attached location.
    /// </summary>
    private volatile bool _locationsAttached;

    /// <summary>
    /// Returns true if all sequence-point locations have been attached to the
    /// instructions of this method.
    /// </summary>
    public bool LocationsAttached => _locationsAttached;

    /// <summary>
    /// Attaches the sequence-point locations of <see cref="Method"/> to all
    /// instructions exactly once. Callers racing with the first attachment block
    /// until it has completed, so every caller observes the complete set of
    /// locations once this method returns.
    /// </summary>
    /// <param name="loadSequencePoints">
    /// Loads the sequence points of the given method. Invoked at most once per
    /// successful attachment.
    /// </param>
    public void EnsureLocationsAttached(
        Func<MethodBase, SequencePointEnumerator> loadSequencePoints)
    {
        if (_locationsAttached) return;

        lock (_locationLock)
        {
            if (_locationsAttached) return;

            var sequencePoints = loadSequencePoints(Method);
            if (sequencePoints.IsValid)
            {
                foreach (var instruction in Instructions)
                    instruction.UpdateLocation(sequencePoints);
            }
            _locationsAttached = true;
        }
    }

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
    /// Returns an instruction enumerator.
    /// </summary>
    /// <returns>An instruction enumerator.</returns>
    public ReadOnlySpan<ILInstruction>.Enumerator GetEnumerator() =>
        Instructions.GetEnumerator();
}
