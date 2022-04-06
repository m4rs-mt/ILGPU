# ILGPU Tutorials

## Primers (How a GPU works)

This series introduces how a GPU works and what ILGPU does. If you have programmed with CUDA or OpenCL 
before you can probably skip 01 and 02.

00 [Setting up ILGPU](Primer_00.md) (ILGPU version 1.0.0)

01 [A GPU is not a CPU](Primer_01.md) (ILGPU version 1.0.0)
> This page will provide a quick rundown the basics of how kernels (think GPU programs) run.

02 [Memory and bandwidth and threads. Oh my!](Primer_02.md)  
> This will hopefully give you a better understanding of how memory works in hardware and the performance
> implications.

## Beginner (How ILGPU works)

This series is meant to be a brief overview of ILGPU and how to use it. It assumes you have at least a little knowledge of how Cuda or OpenCL work. 
If you need a primer look to something like [this for Cuda](https://developer.nvidia.com/about-cuda) or [this for OpenCL](https://www.khronos.org/opencl/)

01 [Context and Accelerators](Tutorial_01.md)
> This tutorial covers creating the Context and Accelerator objects which setup ILGPU for use. 
> It's mostly boiler plate and does no computation but it does print info about your GPU if you have one.
> There is some advice about ILGPU in here that makes it worth the quick read. 
> 
> See Also:
> 
> [Device Info Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/DeviceInfo)

02 [MemoryBuffers and ArrayViews](Tutorial_02.md)
> This tutorial covers the basics for Host / Device memory management.
>
> See Also:
> 
> [Simple Allocation Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/SimpleAlloc)

03 [Kernels and Simple Programs](Tutorial_03.md)
> This is where it all comes together. This covers actual code, on the actual GPU (or the CPU if you are testing / dont have a GPU). 
> 
> See Also: 
> 
> [Simple Kernel Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/SimpleKernel) 
> 
> [Simple Math Sample](https://github.com/m4rs-mt/ILGPU/tree/master/Samples/SimpleMath)


04 [Structs and the N-body problem](Tutorial_04.md)
> This tutorial actually does something! We use computing the N-body problem as a sample of how to better manage Host / Device memory.

## Beginner II (Something more interesting)

Well at least I think. This is where I will put ILGPUView bitmap shader things I (or other people if they want to) eventually write. Below are the few I have planned / think would be easy.

1. Ray Tracing in One Weekend based raytracer
2. Cloud Simulation
3. 2D Physics Simulation
4. Other things I see on shadertoy

# Advanced Resources

## Samples
They cover a wide swath of uses for ILGPU including much of the more complex things that ILGPU is capable of.
[There are too many to list out so I will just link to the repository.](https://github.com/m4rs-mt/ILGPU/tree/master/Samples) 

## Overview

[Memory Buffers & Views](Memory-Buffers-and-Views.md)

[Kernels](Kernels.md)

[Shared Memory](Shared-Memory.md)

[Math Functions](Math-Functions.md)

[Dynamically Specialized Kernels](Dynamically-Specialized-Kernels.md)

[Debugging & Profiling](Debugging-and-Profiling.md)

[Inside ILGPU](Inside-ILGPU.md)

## Upgrade Guides

[Upgrade v0.1.X to v0.2.X](Upgrade-v0.1.X-to-v0.2.X.md)

[Upgrade v0.3.X to v0.5.X](Upgrade-v0.3.X-to-v0.5.X.md)

[Upgrade v0.6.X to v0.7.X](Upgrade-v0.6.X-to-v0.7.X.md)

[Upgrade v0.7.X to v0.8.X](Upgrade-v0.7.X-to-v0.8.X.md)

[Upgrade v0.8.0 to v0.8.1](Upgrade-v0.8.0-to-v0.8.1.md)

[Upgrade v0.8.X to v0.9.X](Upgrade-v0.8.X-to-v0.9.X.md)
