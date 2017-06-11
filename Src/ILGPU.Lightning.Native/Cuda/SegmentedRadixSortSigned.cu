/// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: SegmentedRadixSortSigned.cu
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------
//
// This file uses CUB includes. See README.md and LICENSE-3RD-PARTY.txt for
// details about the CUB license.
//
// -----------------------------------------------------------------------------

#include "SegmentedRadixSort.h"

extern "C"
{
#define SPECIALIZATION_SIGNED(typeName, cType) MAKE_SEGMENTED_RADIXSORT(, SortKeys, typeName, cType)
#include "../RadixSortSignedVariants.h"
}