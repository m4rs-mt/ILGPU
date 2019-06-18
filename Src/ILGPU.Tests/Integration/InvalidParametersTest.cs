using System;
using FluentAssertions;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using Xunit;

namespace ILGPU.Tests.Integration
{
    public class InvalidParametersTest
    {
        [Theory]
        [MemberData(nameof(TestConfigurations.Default), MemberType = typeof(TestConfigurations))]
        public void BigGroupSizeTest(TestConfiguration config)
        {
            using (var context = config.CreateContext())
            using (var accelerator = config.CreateAccelerator(context))
            {
                var kernel = accelerator.LoadStreamKernel<GroupedIndex, int>(EmptyKernel);

                var groupSize = accelerator.MaxNumThreadsPerGroup + 1;
                var dimension = new GroupedIndex(2, groupSize);
                
                Action a = () => kernel(dimension, 0);
                a.Should().Throw<Exception>().Where(x => x is CudaException || x is NotSupportedException);
            }
        }
        
        static void EmptyKernel(GroupedIndex index, int c)
        { }
    }
}