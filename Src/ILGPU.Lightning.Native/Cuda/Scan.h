// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: Scan.h
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------
//
// This file uses CUB includes. See README.md and LICENSE-3RD-PARTY.txt for
// details about the CUB license.
//
// -----------------------------------------------------------------------------

#ifndef _LIGHTNING_SCAN_H_
#define _LIGHTNING_SCAN_H_

#include <stdint.h>
#include <cuda.h>
#include <cuda_runtime.h>
#include <cub/device/device_scan.cuh>

#include "../ILGPU.Lightning.h"

#define MAKE_SCAN(variant, typeName, cType) \
    cudaError_t ILGPULIGHTNING_API Cuda##variant##Scan##typeName( \
        void *tempStorage, \
        size_t *tempStorageSize, \
        cType *source, \
        cType *target, \
        uint32_t numElements, \
        cudaStream_t stream) \
    { \
        return cub::DeviceScan::variant##Sum( \
            tempStorage, \
            *tempStorageSize, \
            source, \
            target, \
            (int)numElements, \
            stream); \
    }

#endif // _LIGHTNING_SCAN_H_
