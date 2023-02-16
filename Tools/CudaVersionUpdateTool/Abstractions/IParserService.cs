﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IParserService.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace CudaVersionUpdateTool.Abstractions
{
    internal interface IParserService
    {
        Task<IEnumerable<CudaVersionSet>> GetVersionSetsAsync();
    }
}
