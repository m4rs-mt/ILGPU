## General Notes

All implicitly grouped kernel launchers have been updated with additional overloads.
These new methods output additional kernel statistics (e.g. the amount of local memory used in bytes).
Detailed information can be enabled with the context flag `ContextFlags.EnableKernelStatistics`.
This information is highly similar to the output of `ptxas -v`.

The new version features an IR verifier that ensures the integrity of the internal IR. It can be enabled via the context flag `ContextFlags.EnableVerifier`.
This is normally not required. However, if you encounter any problems that might be related to a compiler issue, you can enable the verifier.

## Optimization Levels

A new optimization level `O2` has been added. It is disabled by default, but can be enabled via `OptimizationLevel.O2`.

Earlier versions contained *Debug* and *Release* versions of the ILGPU compiler.
The new version contains only the *Release* build.
This significantly improves compile time and simplifies the integration with other third-party libraries.
If you want to enable intrinsic assertions you have to build the compiler from source.
Alternatively, you can use the IR verifier to ensure the validity of a particular IR program.