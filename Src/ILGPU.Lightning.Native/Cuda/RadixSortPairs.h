/// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: RadixSortPairs.h
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------
//
// This file uses CUB includes. See README.md and LICENSE-3RD-PARTY.txt for
// details about the CUB license.
//
// -----------------------------------------------------------------------------

#ifndef _LIGHTNING_RADIXSORT_PAIRS_H_
#define _LIGHTNING_RADIXSORT_PAIRS_H_

#include <stdint.h>
#include <cuda.h>
#include <cuda_runtime.h>
#include <cub/device/device_radix_sort.cuh>

#include "../ILGPU.Lightning.h"
#include "../Index.h"

#define MAKE_RADIXSORT_GEN(prefix, variant, cubName, typeName, cType) \
    cudaError_t ILGPULIGHTNING_API Cuda##prefix##variant##RadixSort##typeName##IndexPairs( \
        void *tempStorage, \
        size_t *tempStorageSize, \
        cType *source, \
        cType *target, \
        Index *valuesSource, \
        Index *valuesTarget, \
        int32_t numElements, \
        int32_t beginBit, \
        int32_t endBit, \
        cudaStream_t stream) \
    { \
        return cub::DeviceRadixSort::cubName##prefix( \
            tempStorage, \
            *tempStorageSize, \
            source, \
            target, \
            valuesSource, \
            valuesTarget, \
            (int)numElements, \
            (int)beginBit, \
            (int)endBit, \
            stream); \
    } \

#define MAKE_RADIXSORT(variant, cubName, typeName, cType) \
    MAKE_RADIXSORT_GEN(, variant, cubName, typeName, cType) \
    MAKE_RADIXSORT_GEN(Descending, variant, cubName, typeName, cType)

#endif // _LIGHTNING_RADIXSORT_PAIRS_H_
