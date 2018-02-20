// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ILGPU.Lightning.h
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

#ifndef _ILGPU_LIGHTNING_H_
#define _ILGPU_LIGHTNING_H_

#ifdef _WINDOWS

#ifdef ILGPULIGHTNING_EXPORTS
#define ILGPULIGHTNING_API __declspec(dllexport)
#else
#define ILGPULIGHTNING_API __declspec(dllimport)
#endif

#else
#define ILGPULIGHTNING_API
#endif // ILGPULIGHTNING_EXPORTS

#endif // _ILGPU_LIGHTNING_H_
