# What is ILGPU

ILGPU provides an interface for programming GPUs that uses a sane programming language, C#.
ILGPU takes your normal C# code (perhaps with a few small changes) and transforms it into either
OpenCL or PTX (think CUDA assembly). This combines all the power, flexibility, and performance of
CUDA / OpenCL with the ease of use of C#.

# Setting up ILGPU.

This tutorial is a little different now because we are going to be looking at the ILGPU 1.0.0.

ILGPU should work on any 64-bit platform that .Net supports. I have even used it on the inexpensive nvidia jetson nano
with pretty decent cuda performance.

Technically ILGPU supports F# but I don't use F# enough to really tutorialize it. I will be sticking to C# in these
tutorials.

### High level setup steps.

If enough people care I can record a short video of this process, but I expect this will be enough for most programmers.

1. Install the most recent [.Net SDK](https://dotnet.microsoft.com/download/visual-studio-sdks) for your chosen
   platform.
2. Create a new C# project.
   ![dotnet new console](Images/newProject.png?raw=true)
3. Add the ILGPU package
   ![dotnet add package ILGPU](Images/beta.png?raw=true)
4. ??????
5. Profit

# More Info

If you would like more info about GPGPU I would recommend the following resources.

* [The Cuda docs](https://developer.nvidia.com/about-cuda) / [OpenCL docs](https://www.khronos.org/opencl/)
* [An Introduction to CUDA Programming - 5min](https://www.youtube.com/watch?v=kIyCq6awClM)
* [Introduction to GPU Architecture and Programming Models - 2h 14min](https://www.youtube.com/watch?v=uvVy3CqpVbM)
