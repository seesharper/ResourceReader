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

        [Fact]
        public void ShouldThrowMeanfullExceptionWhenResourceIsNotFound()
        {
            var resources = new ResourceBuilder().AddAssembly(typeof(ResourceReaderTests).Assembly).Build<IResources>();

            Action act = () => {var content = resources.UnknownResource;};

            act.Should().Throw<InvalidOperationException>().WithMessage("Unable to find any resources that matches 'UnknownResource'");
        }
    }

    public interface IResources
    {
        string SampleResource { get; }

        string UnknownResource { get; }
    }
}
