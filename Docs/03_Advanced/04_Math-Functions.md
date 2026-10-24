The default math functions in .Net are realized with static methods from the `Math` class.
However, many operations work on doubles by default (like `Math.Sin`) and there is often no float overload.
This causes many floating-point operations to be performed on 64-bit floats, even when this precision is not required.
.Net Core and .Net Standard compatible frameworks ship the `MathF` class to overcome this limitation.
ILGPU offers the `IntrinsicMath` class that supports basic math operations which are supported on all target platforms.
The algorithms library offers the `XMath` class that has support for all common 32-bit float and 64-bit float math
operations.
Using the 32-bit overloads ensure that the operations are performed on 32-bit floats on the GPU hardware.

### Fast Math

Fast-math can be enabled using the `Math(MathMode.Fast)` method of `Context.Builder` and enables the use of fast (and
unprecise) math functions.
Unlike previous versions, the fast-math mode applies to all math instructions. Even to default math operations
like `x / y`.

```C#
Context context = Context.Create(builder => builder.Math(MathMode.Fast));
```

On Metal, the fast math modes (`--math Fast` or `--math Fast32BitOnly`, for instance
`<ILGPUCompilationOptions>--math Fast</ILGPUCompilationOptions>` in the project file) enable the fast math of the Metal
compiler, which approximates divisions and uses the fast variants of the math functions.
Without a fast math mode, the Metal compiler runs without fast math.

### Fused Multiply-Adds

By default, the native compilers of all GPU backends may fuse multiplications and additions into multiply-adds
for the best performance.
A fused multiply-add rounds once instead of twice, so an expression like `a * b - c * d` or `a / b - c * d` may differ
from the host in the last bit, and an infinite product may yield an infinity instead of the NaN of `inf - inf`.

To compute every operation like the host, turn fusion off with `--fma Off`, for instance in the project
file:

```xml
<PropertyGroup>
  <ILGPUCompilationOptions>--fma Off</ILGPUCompilationOptions>
</PropertyGroup>
```

or with `FusedMultiplyAddMode.Off` in the `CompilationProperties` passed to the compiler.
This applies to all backends: CUDA kernels are compiled with `--fmad=false`, ROCm kernels with `-ffp-contract=off`,
OpenCL sources start with `#pragma OPENCL FP_CONTRACT OFF` and Metal sources with `#pragma STDC FP_CONTRACT OFF` (and
are compiled with `-ffp-contract=off`), and the pragmas also apply when the runtime compiles the embedded source.
In addition, Metal kernels then divide `Half` values by constants with correct rounding instead of multiplying with
the rounded reciprocal of the constant (unless a fast math mode is requested), so `Half` and `float` results match
`System.Half` and `float` on the host.

CPU kernels never fuse multiplications and additions: they are C# code compiled by the .NET JIT, which does not
fuse floating-point operations, so they match the host in both modes.

### Forced 32-bit Math

Your kernels might rely on third-party functions that are not under your control.
These functions typically depend on the default .Net `Math` class, and thus, work on 64-bit floating-point operations.
You can force the use of 32-bit floating-point operations in all cases using the `Math(MathMode.Fast32BitOnly)` method
of `Context.Builder`.

```c#
Context context = Context.Create(builder => builder.Math(MathMode.Fast32BitOnly));
```

Caution: All doubles will be considered as floats to circumvent issues with third-party code.
However, this also affects the address computations of array-view elements.
Avoid the use of this flag unless you know exactly what you are doing.
