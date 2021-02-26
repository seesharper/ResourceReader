using System;
using FluentAssertions;
using Xunit;


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
        public void ShouldPassResourceInfoToTextProcessor()
        {
            var resources = new ResourceBuilder().WithTextProcessor(ri =>
            {
                ri.Name.Should().Be("ResourceReader.Tests.SampleResource.txt");
                ri.Property.Name.Should().Be("SampleResource");
                return ri.Stream.ReadAsUTF8();
            }).Build<IResources>();

            resources.SampleResource.Should().Be("This is a sample resource");
        }

        [Fact]
        public void ShouldThrowMeanfullExceptionWhenResourceIsNotFound()
        {
            var resources = new ResourceBuilder().AddAssembly(typeof(ResourceReaderTests).Assembly).Build<IResources>();

            Action act = () => { var content = resources.UnknownResource; };

            act.Should().Throw<InvalidOperationException>().WithMessage("Unable to find any resources that matches 'UnknownResource'");
        }

        [Fact]
        public void ShouldThrowMeanfullExceptionOnAmbigiousResource()
        {
            var resources = new ResourceBuilder().AddAssembly(typeof(ResourceReaderTests).Assembly)
            .WithPredicate((resourceName, requestingProperty) =>
            {
                return true;
            })
            .Build<IResources>();

            Action act = () => { var content = resources.SampleResource; };

            act.Should().Throw<InvalidOperationException>().WithMessage("Found multiple resources macthing 'SampleResource'");
        }

        [Fact]
        public void ShouldInvokeResourcePredicate()
        {
            bool invoked = false;

            var resources = new ResourceBuilder().WithPredicate((resourceName, requestingProperty) =>
            {
                invoked = true;
                return ResourceBuilder.DefaultResourcePredicate(resourceName, requestingProperty);
            }).Build<IResources>();

            resources.SampleResource.Should().Be("This is a sample resource");
            invoked.Should().BeTrue();
        }
    }

    public interface IResources
    {
        string SampleResource { get; }

        string UnknownResource { get; }
    }
}
