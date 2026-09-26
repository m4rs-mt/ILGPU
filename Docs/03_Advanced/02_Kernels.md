## General Information

Kernels are `static` functions that can work on value types and can invoke other functions that work on value types.
`class` (reference) types are currently **not supported** in the scope of kernels.
Note that exception handling and boxing are also **not supported** and will be rejected by the ILGPU compiler.
Throwing an exception is fine in a branch that is removed at compile time, as described in
[Type-Specialized Generic Code](#type-specialized-generic-code).
Methods that call themselves, directly or through other methods, are compiled as recursive functions instead of being
inlined, and the build reports each of them with warning `ILGPU1210`.
Every recursive call uses stack memory of its thread, so keep the depth of the recursion small.
The CPU, Cuda and ROCm backends run recursive calls; the Metal and OpenCL backends reject them with error `ILGPU1211`,
unless optimization removes the calls.
A kernel must not call itself, directly or through other methods; every backend rejects this with error `ILGPU1212`.
Move the code that calls itself into a helper method that the kernel calls.
Two kinds of recursion are removed at compile time and run on every backend:
- A static method that calls itself in tail position, returning the result of the call right away (as in
  `Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b)`), compiles to a loop. This requires that its parameters and its
  result are value types other than pointers and ref structs (no `ref` parameters, pointers, delegates or classes),
  that it declares no delegate variables, handles no exceptions and allocates no arrays or `stackalloc` memory. A
  method that takes the address of one of its variables or parameters stays recursive if it converts a value to a
  pointer or `nint`, calls a method returning one, passes an address to a method that converts one (directly or through
  the methods it calls), or has an `nint`, `nuint` or pointer-holding structure parameter, as its next call could then
  read the variable of the calling one.
- A call with constant arguments, such as `Factorial(5)`, is evaluated at compile time if the called method calls other
  methods and accesses no memory, and if its evaluation nests at most 256 calls and executes at most 100,000 IL
  instructions.
Kernels and the functions they call must also be able to return: a function in which every path loops forever, such as
a `while (true)` loop without a `return` or `break`, is rejected with the error "'X' never returns".
Such a kernel could only be stopped by resetting the device.
Loops like `while (true)` that are left through a `return` or `break` are supported.

Arrays created in kernels, such as `new int[4]`, can be `null` (e.g. `c ? new int[4] : null` or `a ?? b`) and can be
tested for `null` like in .NET.
Arrays are compared by their buffers, and every allocation site has a single buffer per thread: two arrays allocated by
the same `new` expression, such as in a loop or in a helper method called twice, are equal.
Unlike in .NET, the `Length` of a `null` array is 0, and accessing an element of a `null` array is undefined behavior on
GPUs (the CPU accelerator throws a `NullReferenceException`).
Accesses to the elements of arrays created in kernels are not bounds-checked: accessing an element outside of an array
is undefined behavior on every accelerator.

The type of the first parameter must always be a supported index type in the case of implicitly grouped kernels (see
below).
In the case of explicitly grouped kernels, the first parameter is manually defined by the programmer (see below)
All other parameters are always uniform constants that are passed from the CPU to the GPU via constant memory (in the
parameter address space).
All parameter types must be value types and must not be passed by reference (e.g. via `out` or `ref` keywords in C#).

Memory allocation is performed via so called `memory buffers`, which are classes that are allocated and disposed on the
CPU.
Since they cannot be passed directly to kernels, you can pass `views` (`Span<T>` like data structures) to these buffers
by value as kernel arguments.

Note that you must not pass pointers to non-accessible memory regions since these are also passed by value and cannot be
marshalled automatically by ILGPU when launching kernels.

Arrays created in kernels, such as `new int[4]`, can be pinned with `fixed` statements, such as
`fixed (int* p = array)`; the pointer of an empty array is `null`, as in .NET.
Leaving such a `fixed` statement through `break`, `continue` or `goto` is not supported.

Some Metal devices, such as the Apple Paravirtual device of macOS virtual machines, place the first thread-private
object of a kernel at address 0, where an array created in the kernel or the address of a local would compare equal
to `null`.
Metal kernels therefore start such arrays and locals one element into a longer array by default.
To save this element, turn it off with `--metal-null-safe-locals false`, for instance in the project file:

```xml
<PropertyGroup>
  <ILGPUCompilationOptions>--metal-null-safe-locals false</ILGPUCompilationOptions>
</PropertyGroup>
```

or with `EnableMetalNullSafeLocals = false` in the `CompilationProperties` passed to the compiler.
The Metal runtime then writes a warning to `System.Diagnostics.Trace` when it loads such a kernel on the Apple
Paravirtual device.

## Lambdas and Delegates

Kernel code can use delegates (e.g. `Func<int, int>`) whose target is known at compile time.
This covers lambda expressions (capturing or not) and static methods that are invoked in kernel code, as well as
lambdas and static method groups passed directly to a method called by the kernel.
The compiler creates a copy of the called method for each distinct delegate target, also when that method passes the
delegate on to further methods:

```c#
static int Apply(Func<int, int> f, int x) => f(x);

static void MyKernel(Index1D index, ArrayView<int> data, int k)
{
    data[index] = Apply(x => x * 3, index) + Apply(x => x + k, index);
}
```

Launch lambdas can pass lambdas to kernel methods taking a delegate in the same way, as in
`stream.Launch(view.IntExtent, index => UnaryOpKernel(index, view, x => -x))`.

The kernel passed to a launch must be a lambda expression or a method group.
Anonymous methods (`delegate (Index1D i) { ... }`), delegate variables and conditional expressions selecting a kernel
are reported at build time (`ILGPU1113`); write the kernel as a lambda expression, or launch each kernel in a branch
of its own.
Launches must be direct calls on a stream expression, such as `stream.Launch(...)`.
Conditional access (`stream?.Launch(...)`) and launch methods used as delegates cannot be compiled and are reported
at build time (`ILGPU1112`).
Launches that are not compiled at all, for example when called through reflection, throw a `NotSupportedException`
at runtime.

Delegates selected at runtime, such as `flag ? f : g` or a delegate variable reassigned in a loop, dispatch on the
selected target when they are invoked or passed to a method.
Delegates that cannot be traced back to lambdas or static methods in kernel code, such as delegates returned from
methods, are rejected by the compiler.

The compiler rejects all other uses of delegates in kernel code at build time, with an error at the offending source
line in the form `File.cs(line,column): error ILGPU12xx: message`:

| Diagnostic | Rejected kernel code |
|---|---|
| `ILGPU1200` | Other constructs kernels do not support, such as `Expression<Func<...>>` or `object`, and `DynamicInvoke`, whose argument array is an `object[]`, and expressions nested too deeply for the stack of the compiling thread, such as unrolled chains of thousands of operations |
| `ILGPU1201` | Null tests and comparisons of delegates (`f?.Invoke(x)`, `f != null`, `f == null`, `f == g`, `Equals`), casts and type tests to delegate types, and delegate members other than `Invoke`, such as `+=`/`Delegate.Combine`, `Method` or `Target` |
| `ILGPU1202` | Invoking a delegate, or passing it to a method, that cannot be traced to a lambda or a static method in kernel code, such as a delegate returned from a method |
| `ILGPU1203` | Delegate variables passed as `ref`, `out` or `in` arguments, or used through a reference |
| `ILGPU1204` | Lambdas and local functions capturing views or pointers of shared or local memory, also when the view is a parameter of the capturing method |
| `ILGPU1205` | Delegates stored in fields of closures (a lambda capturing a delegate, or a capturing lambda creating another lambda), structures or tuples, and arrays of delegates |
| `ILGPU1206` | Delegates loaded from static fields, including `static readonly` fields |
| `ILGPU1207` | Delegates created from instance methods of structs, such as `Func<int, int> f = value.Add` |
| `ILGPU1208` | Lambdas and local functions whose closure is allocated inside a loop, such as a lambda capturing a variable declared in the loop body |
| `ILGPU1209` | Methods taking delegates that call themselves, directly or through the delegates passed to them |
| `ILGPU1211` | Methods that call themselves, directly or through other methods, on the Metal and OpenCL backends |
| `ILGPU1212` | Kernels that call themselves, directly or through other methods |
| `ILGPU1213` | Calls of methods whose type arguments are nested more than 32 levels deep, such as a generic method `F<T>` calling `F<Wrap<T>>`, which would create new instantiations without end |
| `ILGPU1214` | Views, pointers and structures or arrays containing them loaded from static fields, including `static readonly` fields, whose values are read at compile time and do not refer to device memory; pass the view as a kernel parameter or capture it instead. Static fields of strings, classes and interfaces, and structures in static fields that hold arrays, strings or class instances, such as `struct H { public int V; public int[] A; }`, are rejected the same way, even if they hold null; pass the data in an `ArrayView` instead. A helper method that loads such a field is only an error if a call to it remains after optimization, such as a call on a path that constant arguments rule out |

Invoking a delegate variable that is still `null` on some path, without a null test, is undefined behavior in kernels.
To call an optional operation, pass a separate flag instead of testing the delegate for null.

A launch lambda can also capture a delegate parameter of the method that launches the kernel:

```c#
static class Host
{
    public static void UnaryOp(AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
    {
        stream.Launch(view.IntExtent, index => UnaryOpKernel(index, view, op));
    }
}

Host.UnaryOp(stream, view, v => -v);
Host.UnaryOp(stream, view, v => ~v);
Host.UnaryOp(stream, view, MyOps.Square);
```

The compiler traces all calls of the launching method in the project and compiles one kernel for each lambda
expression, anonymous method and static method passed to it, also when a calling method forwards a delegate parameter
of its own.
At runtime, the launch selects the kernel compiled for the target of the delegate it receives.
The passed lambdas must not capture variables (`ILGPU1107`); pass the values as separate arguments of the launching
method instead.
Arguments that cannot be traced to a lambda or a static method, such as delegates returned from methods, multicast
delegates combined with `+` or `Delegate.Combine`, instance methods or a launching method used as a delegate itself,
are reported at build time (`ILGPU1108`), as well as a
captured delegate parameter of a launching method that no call passes a lambda or a static method to (`ILGPU1109`).
Lambdas declared in generic methods or types, and method groups that depend on type parameters, cannot be bound to a
kernel and are reported at build time (`ILGPU1104`).
The same holds for a launch lambda in a generic method or type that captures a delegate parameter: launch lambdas in
generic code are compiled for each instantiation, but the delegates passed to them are not traced per instantiation.
Launching methods, and methods forwarding a delegate parameter to them, must be called directly: methods that are
virtual, abstract or overrides, or that implement an interface member, can be called through a base type or an
interface, where the passed delegates cannot be traced, and are reported at build time (`ILGPU1110`).
Delegates passed by callers the compiler cannot see cannot be traced either.
In library projects, launching methods that other assemblies can call (public methods of public types, protected
methods of public types that other assemblies can derive from, or any non-private method when the assembly declares
`InternalsVisibleTo`), and methods forwarding delegates to them, are therefore reported at build time (`ILGPU1111`):
make them internal or private.
Other assemblies do not call executables (an `OutputType` of `Exe` or `WinExe`), so their launching methods can be
public.
Delegates passed through reflection throw a `NotSupportedException` at the launch.
Other delegates captured by a launch lambda, such as delegate variables of the launching method or structs holding
delegate fields (also in nested structs), cannot be passed from the CPU to a kernel and are reported at build time
(`ILGPU1103`), as are such structs passed to a kernel method by a launch lambda.
Use a generic struct implementing an interface instead:

```c#
interface IUnaryOp { int Apply(int x); }

readonly struct Negate : IUnaryOp { public int Apply(int x) => -x; }

static int Apply<TOp>(TOp op, int x) where TOp : struct, IUnaryOp => op.Apply(x);

static void NegateKernel(Index1D index, ArrayView<int> data, Negate op) =>
    data[index] = Apply(op, data[index]);
```

## Type-Specialized Generic Code

Generic kernels and helper methods can select code for their type arguments with `typeof(T)` tests. ILGPU compiles one
kernel per closed instantiation and evaluates these tests at compile time, so only the selected branch is compiled.
The other branches may contain code that kernels cannot run, such as a `throw`:

```c#
static T GenericSin<T>(T x) where T : unmanaged
{
    if (typeof(T) == typeof(float))
        return Unsafe.BitCast<float, T>(MathF.Sin(Unsafe.BitCast<T, float>(x)));
    else if (typeof(T) == typeof(double))
        return Unsafe.BitCast<double, T>(Math.Cos(Unsafe.BitCast<T, double>(x)));
    else
        throw new NotSupportedException();
}

static void Kernel<T>(Index1D index, ArrayView1D<T, Stride1D.Dense> x, ArrayView1D<T, Stride1D.Dense> ans)
    where T : unmanaged =>
    ans[index] = GenericSin(x[index]);
```

The following are evaluated at compile time:

* `typeof(T)` and comparisons of types with `==`, `!=` and `Type.Equals`, also through `Type` locals, `?:`, `||`,
  `&&` and `switch` statements on types.
* The type properties `IsValueType`, `IsPrimitive`, `IsEnum` and `IsGenericType`, and `IsAssignableFrom` and
  `IsAssignableTo` between types.
* `RuntimeHelpers.IsReferenceOrContainsReferences<T>()` and `Unsafe.SizeOf<T>()`.
* Type tests with patterns on values of value types, such as `x is float f`, `default(T) is int` and `x is IComparable`,
  and `GetType()` on such values: a boxed value type is never null and its type is known exactly.
* Helper predicates built from these, at any depth: generic methods such as `IsFloat<T>()`, static members of generic
  types such as `Ops<T>.IsFloat()`, `static readonly` fields of generic types, static abstract interface members, and
  struct members called through a type parameter such as `default(TOp).IsFloat()`. A helper is evaluated when all its
  arguments are constants and it only computes with them.

`Unsafe.BitCast<TFrom, TTo>` reinterprets a value as another value type of the same size, which is how a branch
converts between `T` and the type it tested for.

A `throw` that stays reachable for an instantiation is a compile error that names the method, the exception type and the
source location. If a type test could not be evaluated at compile time, the error also explains why. When a kernel
instantiation is only reached through generic code (see [Generic Kernels](../../Src/ILGPUC/build/README.md)) and throws
for its type arguments, the build skips it with warning `ILGPU1006` instead, and launching it with these type arguments
throws a `NotSupportedException`, as the same code would on the CPU. An instantiation is only skipped if reachable
`throw`s are its only problem; any other error in it still fails the build.

Other reflection, such as `typeof(T).Name`, `MakeGenericType` or `Type` values that are computed at run time, is not
supported in kernels and is reported with an error that names the member.

## Implicitly Grouped Kernels

Implicitly grouped kernels allow very convenient high-level kernel programming.
They can be launched with automatically configured group sizes (that are determined by ILGPU) or manually defined group
sizes.

Such kernels **must not use shared memory, group or warp functionality** since there is no guaranteed group size or
thread participation inside a warp/group.
The details of the kernel invocation are hidden from the programmer and managed by ILGPU.
There is no way to access or manipulate the low-level peculiarities from the user's point of view.
Use explicitly grouped kernels for full control over GPU-kernel dispatching.

```c#
class ...
{
    static void ImplicitlyGrouped_Kernel(
        [Index1D|Index2D|Index3D] index,
        [Kernel Parameters]...)
    {
        // Kernel code

        // Use the index parameter to access the global index of i-th thread in the global thread grid
    }
}
```

## Explicitly Grouped Kernels

Explicitly grouped kernels offer the full kernel-programming power and behave similarly to Cuda and OpenCL kernels.
These kernels do not receive an index type as first parameter.
Instead, you can use Grid and Group properties to resolve the indices you are interested in.
Moreover, these kernel offer access to shared memory, Group and other Warp-specific intrinsics.
However, the kernel-dispatch dimensions have to be managed manually.

```c#
class ...
{
    static void ExplicitlyGrouped_Kernel(
        [Kernel Parameters]...)
    {
        var globalIndex = Grid.GlobalIndex.X;
        // or
        var globalIndex = Group.IdxX + Grid.IdxX * Group.DimX;

        // Kernel code
    }
}
```

## Loading and Launching Kernels

Kernels have to be loaded by an accelerator first before they can be executed.
See the [ILGPU kernel sample](https://github.com/m4rs-mt/ILGPU.Samples/tree/master/Src/SimpleKernel) for details.
There are two possibilities in general: using the high-level (described here) and
the [low-level loading API](07_Inside-ILGPU.md).
We strongly recommend to use the high-level API that simplifies programming, is less error prone and features automatic
kernel caching and disposal.

An accelerator object offers different functions to load and configure kernels:

* `LoadAutoGroupedStreamKernel`
  Loads an implicitly grouped kernel with an automatically determined group size (uses a the default accelerator stream)
* `LoadAutoGroupedKernel`
  Loads an implicitly grouped kernel with an automatically determined group size (requires an accelerator stream)
* `LoadImplicitlyGroupedStreamKernel`
  Loads an implicitly grouped kernel with a custom group size (uses the default accelerator stream)
* `LoadImplicitlyGroupedKernel`
  Loads an implicitly grouped kernel with a custom group size (requires an accelerator stream)
* `LoadStreamKernel`
  Loads explicitly and implicitly grouped kernels. However, implicitly grouped kernels will be launched with a group
  size that is equal to the warp size (uses the default accelerator stream)
* `LoadKernel`
  Loads explicitly and implicitly grouped kernels. However, implicitly grouped kernels will be launched with a group
  size that is equal to the warp size (requires an accelerator stream)

Functions following the naming pattern `LoadXXXStreamKernel` use the default accelerator stream for all operations.
If you want to specify the associated accelerator stream, you will have to use the `LoadXXXKernel` functions.

Each function returns a typed delegate (a kernel launcher) that can be called in order to invoke the actual kernel
execution.
These launchers are specialized methods that are dynamically generated and specialized for every kernel.
They avoid boxing and realize high-performance kernel dispatching.
In contrast to older versions of ILGPU, all kernels loaded with these functions will be managed by their associated
accelerator instances.

```c#
class ...
{
    static void MyKernel(Index1D index, ArrayView<int> data, int c)
    {
        data[index] = index + c;
    }

    static void Main(string[] args)
    {
        ...
        var buffer = accelerator.Allocate1D<int>(1024);

         // Load a sample kernel MyKernel using one of the available overloads
        var kernelWithDefaultStream = accelerator.LoadAutoGroupedStreamKernel<
                     Index1D, ArrayView<int>, int>(MyKernel);
        kernelWithDefaultStream(buffer.Extent, buffer.View, 1);

         // Load a sample kernel MyKernel using one of the available overloads
        var kernelWithStream = accelerator.LoadAutoGroupedKernel<
                     Index1D, ArrayView<int>, int>(MyKernel);
        kernelWithStream(someStream, buffer.Extent, buffer.View, 1);

        ...
    }
}
```

Note that a kernel-loading operation will trigger a kernel compilation in the case of an uncached kernel.
The compilation step will happen in the background and is transparent for the user.
However, if you require custom control over the low-level kernel-compilation process refer
to [Advanced Low-Level Functionality](07_Inside-ILGPU.md).

## Immediate Launching of Kernels

Starting with version [v0.10.0](https://github.com/m4rs-mt/ILGPU/releases/tag/v0.10.0), ILGPU offers the ability to
immediately compile and launch kernels via the accelerator methods (similar to those provided by other frameworks).
ILGPU exposes direct `Launch` and `LaunchAutoGrouped` methods via the `Accelerator` class using a new strong-reference
based kernel cache.
This cache is used for the new launch methods only and can be disabled via the `Caching(CachingMode.NoKernelCaching)`
method of `ContextBuilder`.

```c#
class ...
{
    static void MyKernel(...)
    {

    }

    static void MyImplicitKernel(Index1D index, ...)
    {

    }

    static void Main(string[] args)
    {
        // ...

        // Launch explicitly grouped MyKernel using the default stream
        accl.Launch(MyKernel, < MyKernelConfig >, ...);

        // Launch explicitly grouped MyKernel using the given stream
        accl.Launch(stream, MyKernel, < MyKernelConfig >, ...);

        // Launch implicitly grouped MyKernel using the default stream
        accl.LaunchAutoGrouped(MyImplicitKernel, new Index1D(...), ...);

        // Launch implicitly grouped MyKernel using the given stream
        accl.LaunchAutoGrouped(stream, MyImplicitKernel, new Index1D(...), ...);
    }
}
```

## Retrieving Information about Loaded Kernels

You can get the underlying `CompiledKernel` instance (see [Inside ILGPU](07_Inside-ILGPU.md)) of a kernel launcher
instance via:

```c#
var compiledKernel = launcher.GetCompiledKernel();
```

This allows users to access accelerator-specific properties of particular kernel.
For instance, you can access the PTX assembly code of a Cuda kernel by casting the `CompiledKernel` instance into
a `PTXCompiledKernel` and access its `PTXAssembly` property, as shown below.

```c#
var ptxKernel = launcher.GetCompiledKernel() as PTXCompiledKernel;
System.IO.File.WriteAllText("Kernel.ptx", ptxKernel.PTXAssembly);
```

You can use the `DebugSymbols()` method of `Context.Builder` to enable additional information about compiled kernels.
This includes local functions and consumed local and shared memory.
After enabling, you can get the information from a compiled kernel launcher delegate instance via:

```c#
// Get kernel information from a kernel launcher instance
var information = launcher.GetKernelInfo();

// Dump all information to the stdout stream
information.DumpToConsole();
```
