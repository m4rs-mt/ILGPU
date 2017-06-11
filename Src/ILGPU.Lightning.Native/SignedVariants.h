// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: SignedVariants.h
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

#ifndef SPECIALIZATION_SIGNED
#error SPECIALIZATION_SIGNED not defined
#endif

SPECIALIZATION_SIGNED( Int8,  int8_t)
SPECIALIZATION_SIGNED(Int16, int16_t)
SPECIALIZATION_SIGNED(Int32, int32_t)
SPECIALIZATION_SIGNED(Int64, int64_t)

#undef SPECIALIZATION_SIGNED

