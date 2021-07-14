The default math functions in .Net are realized with static methods from the `Math` class.
However, many operations work on doubles by default (like `Math.Sin`) and there is often no float overload.
This causes many floating-point operations to be performed on 64-bit floats, even when this precision is not required.
.Net Core and .Net Standard compatible frameworks ship the `MathF` class to overcome this limitation.
ILGPU offers the `IntrinsicMath` class that supports basic math operations which are supported on all target platforms.
The algorithms library offers the `XMath` class that has support for all common 32-bit float and 64-bit float math operations.
Using the 32-bit overloads ensure that the operations are performed on 32-bit floats on the GPU hardware.

### Fast Math
Fast-math can be enabled using the `ContextFlags.FastMath` flag and enables the use of fast (and unprecise) math functions.
Unlike previous versions, the fast-math mode applies to all math instructions. Even to default math operations like `x / y`.

### Forced 32-bit Math
Your kernels might rely on third-party functions that are not under your control.
These functions typically depend on the default .Net `Math` class, and thus, work on 64-bit floating-point operations.
You can force the use of 32-bit floating-point operations in all cases using the `ContextFlags.Force32BitMath` flag.
Caution: all doubles will be considered as floats to circumvent issues with third-party code.
However, this also affects the address computations of array-view elements.
Avoid the use of this flag unless you know exactly what you are doing.