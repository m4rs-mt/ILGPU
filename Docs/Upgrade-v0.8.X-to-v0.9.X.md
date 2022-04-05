---
layout: wiki
---

## General

In order to support 64-bit memory buffers and array views, we have introduced the `LongIndexX` types that represent (multidimensional) 64-bit index value.
We have changed the return types of all `Length` and `LengthInBytes` properties from `int` to `long`.
This might affect your code base, if you work with explicit length information from views and buffers.
Furthermore, if you are using custom index, view or memory buffer implementations, you have to adapt your code to comply with the latest interface definitions or `IArrayView`, `IGenericIndex` and `IMemoryBuffer`.

It is possible to use 32-bit `IndexX` values and 64-bit `LongIndexX` values for accessing generic array views.
This allows programmers to decide whether they want to favor performance (fast 32-bit indexing) or large memory views (slower 64-bit indexing that consumes more 64-bit registers).
*Note that specialized array views working on 32-bit indexes will accept 32-bit `IndexX` instances only.*

In order to provide backwards compatibility, it is possible to implicitly convert a `System.Int64` value into an `Index1` value.
Each conversion operator performs bounds checks that will trigger an assertion in the case on an overflow.
This allows you to launch your kernels using accesses to `Length` and `Extent` properties working on 64-bit integer values.