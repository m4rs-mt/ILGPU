// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ModuleInitializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// [ModuleInitializer] is only available from net5.0 and later.
#if NET5_0_OR_GREATER

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

#endif
