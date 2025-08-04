// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvvmEnums.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Backends.PTX.API;

enum NvvmResult
{
    NVVM_SUCCESS = 0,
    NVVM_ERROR_OUT_OF_MEMORY = 1,
    NVVM_ERROR_PROGRAM_CREATION_FAILURE = 2,
    NVVM_ERROR_IR_VERSION_MISMATCH = 3,
    NVVM_ERROR_INVALID_INPUT = 4,
    NVVM_ERROR_INVALID_PROGRAM = 5,
    NVVM_ERROR_INVALID_IR = 6,
    NVVM_ERROR_INVALID_OPTION = 7,
    NVVM_ERROR_NO_MODULE_IN_PROGRAM = 8,
    NVVM_ERROR_COMPILATION = 9
}
