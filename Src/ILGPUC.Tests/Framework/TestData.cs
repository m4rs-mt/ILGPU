// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TestData.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Shared test struct matching old framework patterns.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TestStruct
{
    public int X;
    public long Y;
    public short Z;
    public int W;
}

/// <summary>
/// A pair of unmanaged values for parameterized tests.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PairStruct<T> where T : unmanaged
{
    public T First;
    public T Second;
}
