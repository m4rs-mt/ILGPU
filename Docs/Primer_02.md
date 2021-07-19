# Primer 02: Memory

The following is my understanding of the performance quirks with GPU's due to memory and cache and coalescent memory access.
Just like with Primer 01, if you havea decent understanding of CUDA or OpenCL you can skip this.

Ok, buckle up.

## Memory and bandwidth and threads. Oh my!

### Computers need memory, and memory is slow<sup>0</sup>. (Like, really slow)
Back in the day (I assume, the first computer I remember using had DDR-200) computer memory
 was FAST. Most of the time the limiting factor was the CPU, though correctly timing video output was also
a driving force. As an example, the C64 ran the memory at 2x the CPU frequency so the VIC-II 
graphics chip could share the CPU memory by stealing half the cycles. In the almost 40 years since the C64, humanity 
has gotten much better at making silicon and precious metals do our bidding. Feeding 
data into the CPU from memory has become the slow part. Memory is slow.

Why is memory slow? To be honest, it seems to me that it's caused by two things:

1. Physics<br/>
Programmers like to think of computers as an abstract thing, a platonic ideal. 
But here in the real world there are no spherical cows, no free lunch. Memory values are ACTUAL
ELECTRONS traveling through silicon and precious metals. 

In general, the farther from the thing doing the math the ACTUAL ELECTRONS are the slower it is
to access.

2. We ~~need~~ want a lot of memory.<br/>
We can that is almost as fast as our processors, but it must literally be directly made into the processor cores in silicon. 
Not only is this is very expensive, the more memory in silicon the less room for processor stuff. 

### How do processors deal with slow memory?

This leads to an optimization problem. Modern processor designers use a complex system of tiered 
memory consisting of several layers of small, fast, on die memory and large, slow, distant, off die memory.

A processor can also perform a few tricks to help us deal with the fact that memory is slow. 
One example is prefetching. If a program uses memory at location X it probably will use the 
memory at location X+1 therefore the processor *prefetchs* a whole chunk of memory and puts it in 
the cache, closer to the processor. This way if you do need the memory at X+1 it is already in cache. 

I am getting off topic. For a more detailed explaination, see this thing I found on [google](https://formulusblack.com/blog/compute-performance-distance-of-data-as-a-measure-of-latency/).

# What does this mean for the ILGPU?

#### GPU's have memory, and memory is slow. 

GPU's on paper have TONS of memory bandwidth, my GPU has around 10x the memory bandwidth my CPU does. Right? Yeah... 

###### Kinda
If we go back into spherical cow territory and ignore a ton of important details, we can illustrate an 
important quirk in GPU design that is directly impacts performance.

My CPU, a Ryzen 5 3600 with dual channel DDR4, gets around 34 GB/s of memory bandwidth. The GDDR6 in my GPU, a RTX 2060, gets around 336 GB/s of memory bandwidth.

But lets compare bandwidth per thread.

CPU: Ryzen 5 3600 34 GB/s / 12 threads = 2.83 GB/s per thread

GPU: RTX 2060 336 GB/s / (30 SM's * 512 threads<sup>1</sup>) = 0.0218 GB/s or just *22.4 MB/s per thread*

#### So what?
In the end computers need memory because programs need memory. There are a few things I think about as I program that I think help:

1. If your code scans through memory linearly the GPU can optimize it by prefetching the data. This leads to the "struct of arrays"
 approach, more on that in the structs tutorial.
2. GPU's take prefetching to the next level by having coalescent memory access, which I need a more in depth explaination of, but
basically if threads are accessing memory in a linear way that the GPU can detect it can send one memory access for the whole chunk
of threads. 

Again, this all boils down to the very simple notion that memory is slow, and it gets slower the farther it gets from the processor.

> <sup>0</sup>
> This is obviously a complex topic. In general modern memory bandwidth has a speed, and a latency problem. They
> are different, but in subtle ways. If you are interested in this I would do some more research, I am just 
> some random dude on the internet.

> <sup>1</sup>
> I thought this would be simple, but after double checking, I found that the question "How many threads can a GPU run at once?"
>  is a hard question and also the wrong question to answer. According to the cuda manual at maximum an SM (Streaming Multiprocessor) can 
> have 16 warps executing simultaneously and 32 threads per warp so it can issue at minimum 512 memory accesses per 
> cycle. You may have more warps scheduled due to memory / instruction latency but a minimum estimate will do. This still provides a good
> illustration for how little memory bandwidth you have per thread. We will get into more detail in a 
> grouping tutorial.