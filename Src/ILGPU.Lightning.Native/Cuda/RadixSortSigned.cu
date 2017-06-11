/// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: RadixSortSigned.cu
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------
//
// This file uses CUB includes. See README.md and LICENSE-3RD-PARTY.txt for
// details about the CUB license.
//
// -----------------------------------------------------------------------------

#include "RadixSort.h"

extern "C"
{
#define SPECIALIZATION_SIGNED(typeName, cType) MAKE_RADIXSORT(, SortKeys, typeName, cType)
#include "../RadixSortSignedVariants.h"
}