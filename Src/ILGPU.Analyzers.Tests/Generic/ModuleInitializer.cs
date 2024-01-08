// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ModuleInitializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;
using VerifyTests;

namespace ILGPU.Analyzers.Tests.Generic
{
    public static class ModuleInitializer
    {
        [ModuleInitializer]
        public static void Init() =>
            VerifySourceGenerators.Initialize();
    }
}
