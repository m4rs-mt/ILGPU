// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DisassemblerException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPUC.Frontend;

/// <summary>
/// Reports IL code that the <see cref="Disassembler"/> cannot disassemble, together
/// with the offset of the instruction that the frontend reports the error at.
/// </summary>
sealed class DisassemblerException : NotSupportedException
{
    /// <summary>
    /// Creates a new exception.
    /// </summary>
    public DisassemblerException() { }

    /// <summary>
    /// Creates a new exception with the given message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DisassemblerException(string message)
        : base(message)
    { }

    /// <summary>
    /// Creates a new exception with the given message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DisassemblerException(string message, Exception innerException)
        : base(message, innerException)
    { }

    /// <summary>
    /// Creates a new exception at the given IL offset.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="ilOffset">The offset of the instruction in bytes.</param>
    /// <param name="innerException">The inner exception (if any).</param>
    public DisassemblerException(
        string message,
        int ilOffset,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ILOffset = ilOffset;
    }

    /// <summary>
    /// Returns the offset in bytes of the instruction that cannot be disassembled.
    /// </summary>
    public int ILOffset { get; }
}
