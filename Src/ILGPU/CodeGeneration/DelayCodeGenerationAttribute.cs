// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelayCodeGenerationAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.CodeGeneration;

/// <summary>
/// Delays code generation until a caller to the current method is found. The caller will
/// then be compiled during runs of ILGPUC.
/// </summary>
/// <remarks>
/// This attribute is useful to reduce compile time during kernel-launcher discovery in
/// ILGPUC. Note that using this attribute does not change compilation semantics in cases
/// a kernel depends on a non-specified generic type argument or an unknown lambda
/// function to be invoked inside the kernel.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DelayCodeGenerationAttribute : Attribute;

