// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AddressSpaceKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Tests.Framework;
using System.Runtime.CompilerServices;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// A struct whose only field is a pointer: the IR represents it as a pointer.
/// </summary>
unsafe struct PointerHolder
{
    public int* P;
}

/// <summary>
/// A struct holding a pointer and a tag.
/// </summary>
unsafe struct TaggedPointer
{
    public int* P;
    public int Tag;
}

/// <summary>
/// Kernel definitions for address space inference: references into shared,
/// local and global memory that flow through phis and helper methods.
/// </summary>
static class AddressSpaceKernels
{
    /// <summary>
    /// Increments the referenced value. Never inlined, so the reference stays a
    /// parameter whose address space depends on all callers.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Increment(ref int value) => value += 1;

    /// <summary>
    /// Returns a reference to the next element. Never inlined: the returned
    /// reference keeps the generic return type.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static ref int Next(ref int value) => ref Unsafe.Add(ref value, 1);

    /// <summary>
    /// Advances the pointer the given pointer points to. Never inlined: the
    /// pointer stays in local memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe void Advance(int** pointer, int steps) => *pointer += steps;

    /// <summary>
    /// Reads an element through a local array of pointers. Never inlined: the
    /// kernel declares the local array of the helper method and passes it on.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe int ReadThroughLocalArray(int* pointer, int slot)
    {
        var addresses = new nint[2];
        addresses[slot] = (nint)pointer;
        return *(int*)addresses[slot];
    }

    /// <summary>
    /// Advances a reference through shared memory: the loop-carried reference
    /// is a phi of element addresses that all point into shared memory.
    /// </summary>
    public static void SharedPointerLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
            shared[groupIdx] = groupIdx + 1;
        Group.Barrier();

        ref int current = ref shared[0];
        int sum = 0;
        for (int i = 0; i <= groupIdx; ++i)
        {
            sum += current;
            current = ref Unsafe.Add(ref current, 1);
        }
        data[index] = sum;
    }

    /// <summary>
    /// Passes a reference into shared memory to a helper method.
    /// </summary>
    public static void SharedRefHelperKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx] = index.X * 10;
            Increment(ref shared[groupIdx]);
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx];
    }

    /// <summary>
    /// Passes a reference to a local variable to a helper method.
    /// </summary>
    public static void LocalRefHelperKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X * 10;
        Increment(ref value);
        data[index] = value;
    }

    /// <summary>
    /// A reference selects one of two elements of shared memory per thread.
    /// </summary>
    public static void SharedRefSelectKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(64);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx] = 10 * (groupIdx + 1);
            shared[groupIdx + 32] = groupIdx;
        }
        Group.Barrier();

        if (groupIdx < 32)
        {
            ref int target = ref groupIdx % 2 == 0
                ? ref shared[groupIdx]
                : ref shared[groupIdx + 32];
            target += 1;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx] + shared[groupIdx + 32];
    }

    /// <summary>
    /// Selects a reference into either shared or global memory: the merged
    /// reference mixes both address spaces and must stay generic.
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void MixedSpaceRefKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
            shared[groupIdx] = 10 * (groupIdx + 1);
        data[index] = index.X;
        Group.Barrier();

        if (groupIdx < 32)
        {
            ref int target = ref groupIdx % 2 == 0
                ? ref shared[groupIdx]
                : ref data[index];
            target += 1;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] += shared[groupIdx];
    }

    /// <summary>
    /// Writes through a reference into shared memory returned by a helper
    /// method.
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void SharedRefReturnKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(33);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx + 1] = 0;
            Next(ref shared[groupIdx]) = index.X + 100;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx + 1];
    }

    /// <summary>
    /// Takes the address of a local pointer into global memory: the pointer
    /// lives in local memory, and is changed and read through a pointer to
    /// it, directly and in a helper method.
    /// </summary>
    public static unsafe void LocalPointerAddressKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        fixed (int* own = &input[index])
        fixed (int* mirrored = &input[3 - index])
        {
            int* p = own;
            int** pp = &p;
            if (index % 2 == 0)
                *pp = mirrored;
            Advance(pp, 0);
            data[index] = **pp;
        }
    }

    /// <summary>
    /// Stores the address of an input element in a local array of pointers
    /// that a helper method allocates, and in a local array of the kernel.
    /// </summary>
    public static unsafe void LocalPointerArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var addresses = new nint[2];
        fixed (int* element = &input[index])
        {
            addresses[index % 2] = (nint)element;
            data[index] = ReadThroughLocalArray(element, index % 2) +
                *(int*)addresses[index % 2];
        }
    }

    /// <summary>
    /// Stores the address of an input element into a view of structs holding
    /// a pointer and into a view of nint addresses, and reads the element back
    /// through both views.
    /// </summary>
    public static unsafe void PointerViewKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<PointerHolder, Stride1D.Dense> holders,
        ArrayView1D<nint, Stride1D.Dense> addresses,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        fixed (int* p = &input[index])
        {
            holders[index] = new PointerHolder { P = p };
            addresses[index] = (nint)p;
        }
        data[index] = *holders[index].P + *(int*)addresses[index];
    }

    /// <summary>
    /// Compares a pointer into global memory that is null in every other
    /// thread with null.
    /// </summary>
    public static unsafe void NullPointerKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        fixed (int* element = &data[index])
        {
            int* p = element;
            if (index % 2 == 0)
                p = null;
            if (p != null)
                *p += 1;
        }
    }

    /// <summary>
    /// Stores the address of a local variable in a local pointer whose address
    /// is taken, and reads the variable through the pointer to the pointer.
    /// </summary>
    public static unsafe void LocalAddressInLocalPointerKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 2 * x;
        int* p = &x;
        int** pp = &p;
        if (index % 2 == 0)
            *pp = &y;
        data[index] = **pp + 1;
    }

    /// <summary>
    /// Stores the address of a local variable in a view of nint addresses and
    /// reads the variable through the view.
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static unsafe void LocalAddressInViewKernel(
        Index1D index,
        ArrayView1D<nint, Stride1D.Dense> addresses,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index] + 1;
        addresses[index] = (nint)(&x);
        data[index] = *(int*)addresses[index];
    }

    /// <summary>
    /// Stores the address of an element of shared memory in a view of structs
    /// holding a pointer and reads the element through the view.
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static unsafe void SharedAddressInViewKernel(
        Index1D index,
        ArrayView1D<TaggedPointer, Stride1D.Dense> pointers,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
            shared[groupIdx] = index.X + 1;
        Group.Barrier();

        if (groupIdx < 32)
        {
            fixed (int* p = &shared[groupIdx])
                pointers[index] = new TaggedPointer { P = p, Tag = 1 };
            data[index] = *pointers[index].P + pointers[index].Tag;
        }
    }

    /// <summary>
    /// Returns the value the pointer points to plus 7, or -1 for null. Never
    /// inlined, so the pointer is compared with null in a helper method.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe int ValueOrMinusOne(int* p) => p == null ? -1 : *p + 7;

    /// <summary>
    /// Compares the address of a local with null, which C# emits as a compare
    /// with the integer zero: the null pointer takes the address space of the
    /// local pointer. A pointer local that holds null is null.
    /// </summary>
    public static unsafe void LocalAddressNullKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X;
        int* p = &value;
        int* none = null;
        data[index] = (p == null ? -1 : *p + 7) + (none == null ? 0 : 100);
    }

    /// <summary>
    /// A pointer that is the address of a local for even threads and null for
    /// odd threads: a phi of the null pointer and a pointer into local memory,
    /// which differs per thread, compared with null in the kernel and in a
    /// helper method.
    /// </summary>
    public static unsafe void NullPointerPhiKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X;
        int* p = null;
        if ((index.X & 1) == 0)
            p = &value;
        data[index] = ValueOrMinusOne(p) * 100 + (p == null ? -1 : *p + 7);
    }

    /// <summary>
    /// Two pointers start as null and take the address of the first output
    /// element, which is the same for all threads, and the address of a local,
    /// which differs per thread: the null pointer is an argument of a uniform
    /// and of a per-thread pointer phi of the same block.
    /// </summary>
    public static unsafe void NullPointerPhisKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X;
        fixed (int* first = &data[0])
        {
            int* p = null;
            int* q = null;
            if (data.IntLength > 2)
            {
                p = first;
                q = &value;
            }
            data[index] = (p == null ? -1 : 2) * 10 + (q == null ? -1 : *q);
        }
    }

    /// <summary>
    /// A pointer that is the address of the thread's shared-memory element for
    /// even threads and null for odd threads, compared with null: the null
    /// pointer takes the shared address space of the other operand.
    /// </summary>
    public static unsafe void SharedNullPointerKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int g = Group.Index;
        shared[g] = index.X + 1;
        Group.Barrier();
        fixed (int* p = &shared[g])
        {
            int* q = index.X % 2 == 0 ? p : null;
            data[index] = q == null ? -1 : *q;
        }
    }
}
