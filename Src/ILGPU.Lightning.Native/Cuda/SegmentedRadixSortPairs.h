// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: SegmentedRadixSortPairs.h
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------
//
// This file uses CUB includes. See README.md and LICENSE-3RD-PARTY.txt for
// details about the CUB license.
//
// -----------------------------------------------------------------------------

#ifndef _LIGHTNING_SEGMENTED_RADIXSORT_PAIRS_H_
#define _LIGHTNING_SEGMENTED_RADIXSORT_PAIRS_H_

#include <stdint.h>
#include <cuda.h>
#include <cuda_runtime.h>
#include <cub/device/device_segmented_radix_sort.cuh>

#include "../ILGPU.Lightning.h"
#include "../Index.h"

#define MAKE_SEGMENTED_RADIXSORT_GEN(prefix, postfix, variant, cubName, typeName, cType) \
    cudaError_t ILGPULIGHTNING_API Cuda##postfix##prefix##variant##RadixSort##typeName##IndexPairs( \
        void *tempStorage, \
        size_t *tempStorageSize, \
        cType *source, \
        cType *target, \
        Index *valuesSource, \
        Index *valuesTarget, \
        int32_t numElements, \
        int *beginOffsets, \
        int *endOffsets, \
        int32_t numSegments, \
        int32_t beginBit, \
        int32_t endBit, \
        cudaStream_t stream) \
    { \
        return cub::DeviceSegmentedRadixSort::cubName##prefix( \
            tempStorage, \
            *tempStorageSize, \
            source, \
            target, \
            valuesSource, \
            valuesTarget, \
            (int)numElements, \
            (int)numSegments, \
            beginOffsets, \
            endOffsets, \
            (int)beginBit, \
            (int)endBit, \
            stream); \
    } \

#define MAKE_SEGMENTED_RADIXSORT(variant, cubName, typeName, cType) \
    MAKE_SEGMENTED_RADIXSORT_GEN(, Segmented, variant, cubName, typeName, cType) \
    MAKE_SEGMENTED_RADIXSORT_GEN(Descending, Segmented, variant, cubName, typeName, cType)

#endif // _LIGHTNING_SEGMENTED_RADIXSORT_PAIRS_H_
