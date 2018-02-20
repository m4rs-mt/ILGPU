// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: UnsignedVariants.h
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

#ifndef SPECIALIZATION_UNSIGNED
#error SPECIALIZATION_UNSIGNED not defined
#endif

SPECIALIZATION_UNSIGNED( UInt8,  uint8_t)
SPECIALIZATION_UNSIGNED(UInt16, uint16_t)
SPECIALIZATION_UNSIGNED(UInt32, uint32_t)
SPECIALIZATION_UNSIGNED(UInt64, uint64_t)

#undef SPECIALIZATION_UNSIGNED

