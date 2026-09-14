// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelCompilationWarning.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;

namespace ILGPUC;

/// <summary>
/// A warning about kernel code that compiles, which ilgpuc builds report in the
/// canonical <c>file(line,column): warning ID: message</c> form.
/// </summary>
/// <param name="Id">The diagnostic identifier (e.g. <c>ILGPU1210</c>).</param>
/// <param name="Message">The message that describes the warning.</param>
/// <param name="Location">
/// The source location of the warning, which is unknown if no debug information
/// is available.
/// </param>
sealed record KernelCompilationWarning(string Id, string Message, Location Location);
