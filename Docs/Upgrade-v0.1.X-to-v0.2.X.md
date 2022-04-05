---
layout: wiki
---

## General Upgrade

If you rely on the `LightningContext` class (of `ILGPU.Lightning` v0.1.X) for high-level kernel loading or other high-level operations, you will have to adapt your projects to the API changes.
The new API does not require a `LightningContext` instance.
Instead, all operations are extension methods to the ILGPU Accelerator class.
This simplifies programming and makes the general API more consistent.
Furthermore, kernel caching and convenient kernel loading are now included in the ILGPU runtime system and do not require any `ILGPU.Lightning` operations.
Moreover, if you make use of the low-level kernel-loading functionality of the ILGPU runtime system (in order to avoid additional library dependencies to `ILGPU.Lightning`), you will also benefit from the new API changes.

Note that all functions from v0.1.X will still work to ensure backwards compatibility.
However, they will be removed in future versions.

## The Obsolete Lightning Context

The `LightningContext` class is obsolete and will be removed in future versions.
It encapsulated an ILGPU `Accelerator` instance and provided useful kernel caching and loading features.
Moreover, all extensions functions (like sorting, for example) were based on a `LightningContext`.

We recommend that you replace all occurrences of a `LightningContext` with an ILGPU `Accelerator`.
Furthermore, change the `LightningContext` creation code with an appropriate accelerator construction from ILGPU.
Note that kernel caching and loading are now natively provided by an `Accelerator` object.

```c#
class ...
{
    public static void Main(string[] args)
    {
        // Create the required ILGPU context
        using (var context = new Context())
        {
            // Deprecated code snippets for creating a LightningContext
            var ... = LightningContext.CreateCPUContext(context);
            var ... = LightningContext.CreateCudaContext(context);
            var ... = LightningContext.Create(context, acceleratorId);

            // New version: use default ILGPU accelerators and perform
            // all required operations on an accelerator instance.
            var ... = new CPUAccelerator(context);
            var ... = new CudaAccelerator(context);
            var ... = Accelerator.Create(context, acceleratorId);


            // Old sample for an Initialize command
            var lc = LightningContext.Create(context, ...);
            lc.Initialize(targetView);

            // New version
            var accl = Accelerator.Create(context, acceleratorId);
            accl.Initialize(targetView);
        }
    }
}
```