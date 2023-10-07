// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2017-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.fs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

open ILGPU
open ILGPU.Runtime

type Program =
    static member MyKernel (index: Index1D) (buffer: ArrayView1D<int, Stride1D.Dense>) (constant: int) =
        buffer.[index] <- int(index) + constant

[<EntryPoint>]
let main _argv =
    // Create main context
    use context = Context.CreateDefault()

    // For each available device...
    for device in context do
        // Create accelerator for the given device
        use accelerator = device.CreateAccelerator(context)
        printfn "Performing operations on %O" accelerator

        use stream = accelerator.CreateStream()
        let kernel = accelerator.LoadAutoGroupedStreamKernel<_, _, _>(Program.MyKernel)
        use buffer = accelerator.Allocate1D<_>(1024L)

        kernel.Invoke(Index1D(int(buffer.Length)), buffer.View, 42)

        // Calls stream.Synchronize() to ensure
        // that the kernel and memory copy are completed first.        
        accelerator.Synchronize();

        // Reads data from the GPU buffer into a new CPU array.
        buffer.GetAsArray1D(stream)
        |> Array.iteri (fun index element -> printfn $"{index} = {element}")

    0 // return an integer exit code
