using System;
using Xunit;
using FluentAssertions;


namespace ResourceReader.Tests
{
    public class ResourceReaderTests
    {
        [Fact]
        public void ShouldReadResource()
        {
            var resources = new ResourceBuilder().Build<IResources>();
            var content = resources.SampleResource;
            content.Should().Be("This is a sample resource");
        }

        [Fact]
        public void ShouldReadResourceWhenSpecifyingAssembly()
        {
            var resources = new ResourceBuilder().AddAssembly(typeof(ResourceReaderTests).Assembly).Build<IResources>();
            var content = resources.SampleResource;
            content.Should().Be("This is a sample resource");
        }
    }

    public interface IResources
    {
        string SampleResource { get; }
    }
}
