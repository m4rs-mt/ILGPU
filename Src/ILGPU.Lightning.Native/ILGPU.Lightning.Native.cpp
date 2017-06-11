// ILGPU.Lightning.Native.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "ILGPU.Lightning.Native.h"


// This is an example of an exported variable
ILGPULIGHTNINGNATIVE_API int nILGPULightningNative=0;

// This is an example of an exported function.
ILGPULIGHTNINGNATIVE_API int fnILGPULightningNative(void)
{
    return 42;
}

// This is the constructor of a class that has been exported.
// see ILGPU.Lightning.Native.h for the class definition
CILGPULightningNative::CILGPULightningNative()
{
    return;
}
