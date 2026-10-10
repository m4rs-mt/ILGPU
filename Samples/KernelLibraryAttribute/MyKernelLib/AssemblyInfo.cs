// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AssemblyInfo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;

// Marks this assembly as a kernel-callable library so the ILGPUC frontend
// will eagerly walk methods inside it during the BFS disassembly pass.
//
// You only *need* this attribute when the library is reached transitively —
// for example, when shipped as a NuGet package and the consumer references
// you through another wrapper library rather than directly. Direct references
// of the entry kernel's project are walked automatically (rule b in the
// round-3 walkability classifier; see Src/ILGPUC/Frontend/ILFrontendCache.cs).
//
// Marking your library anyway is recommended: it documents the intent and
// keeps the eager-walk fast path available regardless of how downstream
// consumers wire up their dependency graph. Compilation remains correct
// either way — non-walkable libraries fall back to lazy on-the-fly
// disassembly during codegen, just slower on cold first-kernel compile.
[assembly: KernelLibrary]
