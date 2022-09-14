# Structs

As we saw in the [memory tutorial](Tutorial_02.md) programs need data.
However a problem arises when we use ILGPU because it restricts how you can store, move, allocate, and access data.
This is mostly due to the fact that ILGPU is turning C# code into lower level languages.

## How do we deal with this?
*Data is data is data.*

> Note: this example is a console version of the N-body template of my ILGPUView project.
> When this is more ready I will include a link, but ILGPUView will allow you to see the result in realtime.

### N-Body Example
```c#
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Program
{
    public static void Main()
    {
        Context context = Context.Create(builder => builder.Default().EnableAlgorithms());
        Accelerator device = context.GetPreferredDevice(preferCPU: false)
                                  .CreateAccelerator(context);

        int width = 500;
        int height = 500;
        
        // my GPU can handle around 10,000 when using the struct of arrays
        int particleCount = 100; 

        byte[] h_bitmapData = new byte[width * height * 3];

        using MemoryBuffer2D<Vec3, Stride2D.DenseY> canvasData = device.Allocate2DDenseY<Vec3>(new Index2D(width, height));
        using MemoryBuffer1D<byte, Stride1D.Dense> d_bitmapData = device.Allocate1D<byte>(width * height * 3);

        CanvasData c = new CanvasData(canvasData, d_bitmapData, width, height);

        using HostParticleSystem h_particleSystem = new HostParticleSystem(device, particleCount, width, height);

        var frameBufferToBitmap = device.LoadAutoGroupedStreamKernel<Index2D, CanvasData>(CanvasData.CanvasToBitmap);
        var particleProcessingKernel = device.LoadAutoGroupedStreamKernel<Index1D, CanvasData, ParticleSystem>(ParticleSystem.particleKernel);

        //process 100 N-body ticks
        for (int i = 0; i < 100; i++)
        {
            particleProcessingKernel(particleCount, c, h_particleSystem.deviceParticleSystem);
            device.Synchronize();
        }

        frameBufferToBitmap(canvasData.Extent.ToIntIndex(), c);
        device.Synchronize();

        d_bitmapData.CopyToCPU(h_bitmapData);

        //bitmap magic that ignores bitmap striding, be careful some sizes will mess up the striding
        using Bitmap b = new Bitmap(width, height, width * 3, PixelFormat.Format24bppRgb, Marshal.UnsafeAddrOfPinnedArrayElement(h_bitmapData, 0));
        b.Save("out.bmp");
        Console.WriteLine("Wrote 100 iterations of N-body simulation to out.bmp");
    }

    public struct CanvasData
    {
        public ArrayView2D<Vec3, Stride2D.DenseY> canvas;
        public ArrayView1D<byte, Stride1D.Dense> bitmapData;
        public int width;
        public int height;

        public CanvasData(ArrayView2D<Vec3, Stride2D.DenseY> canvas, ArrayView1D<byte, Stride1D.Dense> bitmapData, int width, int height)
        {
            this.canvas = canvas;
            this.bitmapData = bitmapData;
            this.width = width;
            this.height = height;
        }

        public void setColor(Index2D index, Vec3 c)
        {
            if ((index.X >= 0) && (index.X < canvas.IntExtent.X) && (index.Y >= 0) && (index.Y < canvas.IntExtent.Y))
            {
                canvas[index] = c;
            }
        }

        public static void CanvasToBitmap(Index2D index, CanvasData c)
        {
            Vec3 color = c.canvas[index];

            int bitmapIndex = ((index.Y * c.width) + index.X) * 3;

            c.bitmapData[bitmapIndex] = (byte)(255.99f * color.x);
            c.bitmapData[bitmapIndex + 1] = (byte)(255.99f * color.y);
            c.bitmapData[bitmapIndex + 2] = (byte)(255.99f * color.z);

            c.canvas[index] = new Vec3(0, 0, 0);
        }
    }

    public class HostParticleSystem : IDisposable
    {
        public MemoryBuffer1D<Particle, Stride1D.Dense> particleData;
        public ParticleSystem deviceParticleSystem;

        public HostParticleSystem(Accelerator device, int particleCount, int width, int height)
        {
            Particle[] particles = new Particle[particleCount];
            Random rng = new Random();

            for (int i = 0; i < particleCount; i++)
            {
                Vec3 pos = new Vec3((float)rng.NextDouble() * width, (float)rng.NextDouble() * height, 1);
                particles[i] = new Particle(pos);
            }

            particleData = device.Allocate1D(particles);
            deviceParticleSystem = new ParticleSystem(particleData, width, height);
        }

        public void Dispose()
        {
            particleData.Dispose();
        }
    }

    public struct ParticleSystem
    {
        public ArrayView1D<Particle, Stride1D.Dense> particles;
        public float gc;
        public Vec3 centerPos;
        public float centerMass;

        public ParticleSystem(ArrayView1D<Particle, Stride1D.Dense> particles, int width, int height)
        {
            this.particles = particles;

            gc = 0.001f;

            centerPos = new Vec3(0.5f * width, 0.5f * height, 0);
            centerMass = (float)particles.Length;
        }

        public Vec3 update(int ID)
        {
            particles[ID].update(this, ID);
            return particles[ID].position;
        }

        public static void particleKernel(Index1D index, CanvasData c, ParticleSystem p)
        {
            Vec3 pos = p.update(index);
            Index2D position = new Index2D((int)pos.x, (int)pos.y);
            c.setColor(position, new Vec3(1, 1, 1));
        }
    }

    public struct Particle
    {
        public Vec3 position;
        public Vec3 velocity;
        public Vec3 acceleration;

        public Particle(Vec3 position)
        {
            this.position = position;
            velocity = new Vec3();
            acceleration = new Vec3();
        }

        private void updateAcceleration(ParticleSystem d, int ID)
        {
            acceleration = new Vec3();

            for (int i = 0; i < d.particles.Length; i++)
            {
                Vec3 otherPos;
                float mass;

                if (i == ID)
                {
                    //creates a mass at the center of the screen
                    otherPos = d.centerPos;
                    mass = d.centerMass;
                }
                else
                {
                    otherPos = d.particles[i].position;
                    mass = 1f;
                }

                float deltaPosLength = (position - otherPos).length();
                float temp = (d.gc * mass) / XMath.Pow(deltaPosLength, 3f);
                acceleration += (otherPos - position) * temp;
            }
        }

        private void updatePosition()
        {
            position = position + velocity + acceleration * 0.5f;
        }

        private void updateVelocity()
        {
            velocity = velocity + acceleration;
        }

        public void update(ParticleSystem particles, int ID)
        {
            updateAcceleration(particles, ID);
            updatePosition();
            updateVelocity();
        }
    }
}

public struct Vec3
{
    public float x;
    public float y;
    public float z;

    public Vec3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static Vec3 operator +(Vec3 v1, Vec3 v2)
    {
        return new Vec3(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
    }

    public static Vec3 operator -(Vec3 v1, Vec3 v2)
    {
        return new Vec3(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);
    }

    public static Vec3 operator *(Vec3 v1, float v)
    {
        return new Vec3(v1.x * v, v1.y * v, v1.z * v);
    }

    public float length()
    {
        return XMath.Sqrt(x * x + y * y + z * z);
    }
}
```

## Ok, this is a long one.
I am not going to explain every line like I did with the kernel example.

I will however explain each struct. Lets start with the easy ones.

### Vec3 && Particle

These are just simple data structures. C# is super nice lets you create member functions and 
constructors for structs.

### CanvasData

You can just create a struct that holds ArrayViews and pass them to kernels. The issue with this is it seperates the MemoryBuffer and ArrayView into two different places.
There is nothing wrong with this but I think it leads to messy code. My attempt to fix this is the pattern that HostParticleSystem uses.

### ParticleSystem && HostParticleSystem

You need to manage both sides of memory, Host and Device. IDisposable allows you to use the super convenient "using" patterns but requires a class.
The solution is simple, have a host side class that creates a device side struct.

## This sample code works... BUT
This code can be MUCH faster. 

# Array of Structs VS Struct of Arrays
The ParticleSystem struct follows a pattern called an array of structs, because its data is stored in an array of structs.
In RAM the array of structs(Particles) looks like this:

```
p0:
    pos
    vel
    accel
p1:
    pos
    vel
    accel
```

Consider what happens when the GPU loads the pos value from memory. The GPU is loading multiple pieces of data at a time. 
If the loads are "coherent" or how I think of it "chunked together" they will be MUCH faster.

We can do this my simply having 3 arrays. This causes memory to look like this:
```
pos0
pos1

vel0
vel1

accel0
accel1

```

This pattern is called a struct of arrays. 

As you can see from the example it is much more complex to deal with, but at a particle count of 50,000 it is 5 times faster.

```c#
public class HostParticleSystemStructOfArrays : IDisposable
{
    public int particleCount;
    public MemoryBuffer1D<Vec3, Stride1D.Dense> positions;
    public MemoryBuffer1D<Vec3, Stride1D.Dense> velocities;
    public MemoryBuffer1D<Vec3, Stride1D.Dense> accelerations;
    public ParticleSystemStructOfArrays deviceParticleSystem;

    public HostParticleSystemStructOfArrays(Accelerator device, int particleCount, int width, int height)
    {
        this.particleCount = particleCount;
        Vec3[] poses = new Vec3[particleCount];
        Random rng = new Random();

        for (int i = 0; i < particleCount; i++)
        {
            poses[i] = new Vec3((float)rng.NextDouble() * width, (float)rng.NextDouble() * height, 1);
        }

        positions = device.Allocate1D(poses);
        velocities = device.Allocate1D<Vec3>(particleCount);
        accelerations = device.Allocate1D<Vec3>(particleCount);

        velocities.MemSetToZero();
        accelerations.MemSetToZero();

        deviceParticleSystem = new ParticleSystemStructOfArrays(positions, velocities, accelerations, width, height);
    }

    public void Dispose()
    {
        positions.Dispose();
        velocities.Dispose();
        accelerations.Dispose();
    }
}

public struct ParticleSystemStructOfArrays
{
    public ArrayView1D<Vec3, Stride1D.Dense> positions;
    public ArrayView1D<Vec3, Stride1D.Dense> velocities;
    public ArrayView1D<Vec3, Stride1D.Dense> accelerations;
    public float gc;
    public Vec3 centerPos;
    public float centerMass;

    public ParticleSystemStructOfArrays(ArrayView1D<Vec3, Stride1D.Dense> positions, ArrayView1D<Vec3, Stride1D.Dense> velocities, ArrayView1D<Vec3, Stride1D.Dense> accelerations, int width, int height)
    {
        this.positions = positions;
        this.velocities = velocities;
        this.accelerations = accelerations;
        gc = 0.001f;
        centerPos = new Vec3(0.5f * width, 0.5f * height, 0);
        centerMass = (float)positions.Length;
    }

    private void updateAcceleration(int ID)
    {
        accelerations[ID] = new Vec3();

        for (int i = 0; i < positions.Length; i++)
        {
            Vec3 otherPos;
            float mass;

            if (i == ID)
            {
                //creates a mass at the center of the screen
                otherPos = centerPos;
                mass = centerMass;
            }
            else
            {
                otherPos = positions[i];
                mass = 1f;
            }

            float deltaPosLength = (positions[ID] - otherPos).length();
            float temp = (gc * mass) / XMath.Pow(deltaPosLength, 3f);
            accelerations[ID] += (otherPos - positions[ID]) * temp;
        }
    }

    public static void particleKernel(Index1D index, CanvasData c, ParticleSystemStructOfArrays p)
    {
        Vec3 pos = p.update(index);
        Index2D position = new Index2D((int)pos.x, (int)pos.y);
        c.setColor(position, new Vec3(1, 1, 1));
    }

    private void updatePosition(int ID)
    {
        positions[ID] = positions[ID] + velocities[ID] + accelerations[ID] * 0.5f;
    }

    private void updateVelocity(int ID)
    {
        velocities[ID] = velocities[ID] + accelerations[ID];
    }

    public Vec3 update(int ID)
    {
        updateAcceleration(ID);
        updatePosition(ID);
        updateVelocity(ID);
        return positions[ID];
    }
}
```