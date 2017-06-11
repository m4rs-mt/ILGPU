// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: ILGPU.LLVM.h
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

#ifndef _ILGPU_LLVM_H_
#define _ILGPU_LLVM_H_

#ifdef _WINDOWS

#ifdef ILGPULLVM_EXPORTS
#define ILGPULLVM_API __declspec(dllexport)
#else
#define ILGPULLVM_API __declspec(dllimport)
#endif

#else
#define ILGPULLVM_API
#endif

#endif // !_ILGPU_LLVM_H_
