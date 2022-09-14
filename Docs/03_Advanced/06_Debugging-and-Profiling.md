The best debugging experience can be achieved with the `CPUAccelerator`.
Debugging with the software emulation layer is very convenient due to the very good properties of the .Net debugging
environments.
Currently, detailed kernel debugging is only possible with the CPU accelerator.
However, we are currently extending the debugging capabilities to also emulate different GPUs in order to test your
algorithms with "virtual GPU devices" without needing to have direct access to the actual GPU devices (more information
about this feature can be found [here](https://github.com/m4rs-mt/ILGPU/pull/402).

Assertions on GPU hardware devices can be enabled using the `Assertions()` method of `Context.Builder` (disabled by
default when a `Debugger` is not attached to the application).
Note that enabling assertions using this flag will cause them to be enabled in `Release` builds as well.
Be sure to disable this flag if you want to get the best runtime performance.

Source-line based debugging information can be turned on via the `DebugSymbols()` method of `Context.Builder` (disabled
by default).
Note that only the new portable PBD format is supported.
Enabling debug information is essential to identify problems and catch break points on GPU hardware.
It is also very useful for kernel profiling as you can link the profiling insights to your source lines.
You may want to disable inlining via `Inlining()` to significantly increase the accuracy of your debugging information
at the expense cost of runtime performance.

*Note that the inspection of variables, registers, and global memory on GPU hardware is currently not supported.*

## Named Kernels

PR [#401](https://github.com/m4rs-mt/ILGPU/pull/401) added support for using either the .Net function name or a custom
name as the entry point for each Cuda/OpenCL kernel. This simplifies profiling and debugging because multiple kernels
then have different names.

*Note that custom kernel names have to consist of ASCII characters only. Other characters will be automatically mapped
to '_' in the assembly code.*

## PrintF-Like Debugging

Cuda and OpenCL provide the ability to print/output basic values into the console output stream for debugging. This is
especially useful for device-specific concurrency problems that need to be analyzed without changing environment
settings. Starting with [v0.10.0](https://github.com/m4rs-mt/ILGPU/releases/tag/v0.10.0), ILGPU provides platform
independent support for `Interop.Write` and `Interop.WriteLine` that accept (very) basic .Net-like format specifiers of
the form `Test output {0} and test output {1}`.

This string can be formatted to `Test output 1.0 and test output -45` using:

```c#
Interop.Write("Test output {0} and test output {1}", 1.0, -45);
```

Note that this functionality is enabled by default when a `Debugger` is attached to the application. For this to work
without the Debugger, or in Release mode, call the `.IOOperations()` method of `Context.Builder`. Be sure to remove this
flag if you want to get the best runtime performance.
