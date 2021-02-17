Create a new `.Net Framework 4.7.X` (or higher) or a `.Net Standard 2.1` compatible project (e.g. `.Net Core 3.1`) and install the required [ILGPU Nuget](https://www.nuget.org/packages/ILGPU/) package.
```bash
// Create new  project with .Net 4.7 (or higher) or .Net Standard 2.1 compatible (e.g. .Net Core 3.1)
dotnet new console
nuget install ILGPU
```

We recommend that you disable the `Prefer 32bit` option in the application build-settings panel.
This typically ensures that the application is executed in native-OS mode (e.g. 64bit on a 64bit OS).
You will not able to instantiate an `Accelerator` instance unless your application runs in native-OS mode since ILGPU requires direct interaction with the graphics-driver API.

The ILGPU compiler has been designed in way that it **does not** rely on native libraries.
Therefore, it is not necessary to worry about such dependencies (except, of course, for the actual GPU drivers) or environment variables.
Neither a Cuda SDK nor an OpenCL SDK have to be installed to use ILGPU.

While GPU programming can be done using only the ILGPU package, we recommend using the [ILGPU.Algorithms library](https://www.nuget.org/packages/ILGPU.Algorithms/) that realizes useful functions like scan, reduce and sort.

If you want to know about recent changes or new features refer to the [upgrade Guide](), the [milestone descriptions](https://github.com/m4rs-mt/ILGPU/milestones) or the [change logs](https://github.com/m4rs-mt/ILGPU/releases/).
