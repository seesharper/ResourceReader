# ResourceReader

[![Build status](https://bernhardrichter.visualstudio.com/ResourceReader/_apis/build/status/ResourceReader-CI)](https://bernhardrichter.visualstudio.com/ResourceReader/_build/latest?definitionId=2)

Install it from [NuGet](https://www.nuget.org/packages/ResourceReader/).

Provides type safe access to embedded text files. So what does that mean? Say that we are creating an application that does data access using an micro-orm like [Dapper](https://www.nuget.org/packages/Dapper/), [DbReader](https://www.nuget.org/packages/DbReader/) or any other tool that uses plain old SQL as input.

These database queries needs to live somewhere, either as verbatim strings in our source code or as files on disk.
Given the latter approach we might have a folder in our project that looks something like this.

```shell
└── Queries
    ├── GetAllCustomers.sql
    ├── GetCustomerById.sql
    ├── InsertCustomer.sql
    └── DeleteCustomer.sql
```

Further on we can embed these files as resources in our `csproj` file like this.

```xml
<ItemGroup>
  <EmbeddedResource Include="**/*.sql" />
</ItemGroup>
```

So when we compile our project these files are now embedded into our assembly and in order to retrieve these files, we need to read them out using the [GetManifestResourceStream](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.assembly.getmanifestresourcestream?view=netframework-4.7.2) method. Such code can get messy real fast and besides also hard to maintain as these resources needs to be qualified with the full namespace making it prone to errors if we move this files around.

Lets look at an alternative approach using **ResourceReader**

We start off by creating an interface that will represent our queries.

```c#
public interface IQueries
{
    string GetAllCustomers { get; }
    string GetCustomerById { get; }
    string InsertCustomer { get; }
    string DeleteCustomer { get; }
}
```

Now, wouldn't it be nice if could retrieve our queries just by accessing these properties? So that we can have code like this

```C#
public class SomeService
{
  private readonly IQueries _queries;
  public SomeService(IQueries queries)
  {
    _queries = queries;
  }

  public Customer GetCustomer(int id)
  {
    var query = _queries.GetCustomerById;
   	// Execute query using our preferred tool
  }
}
```

So where is the implementation of `IQueries` we might ask? In fact don't need to implement it and this is where **ResourceReader** comes into play.

```c#
var queries = new ResourceBuilder().Build<IQueries>();
```

That's it! That is all we need to do in order to create an `IQueries` instance that we can use to access our embedded files. If we are using an DI container, we can register this as a service so that we can inject `IQueries` into other services. Something like the following using [Microsoft.Extensions.DependencyInjection](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.2).

```c#
public void ConfigureServices(IServiceCollection services)
{
  services.Singleton<IQueries>(f => new ResourceBuilder().Build<IQueries>());
}
```

By default, **ResourceReader** will look for resources in all loaded assemblies except for those in the `System` namespace. For more fine grained control we can specify which assemblies that contains these resources.

```c#
var queries = new ResourceBuilder().AddAssembly(typeof(SomeType).Assembly).Build<IQueries>();
```

One nice aspect of this is that it makes it possible to choose resource binding at runtime. For instance, in the example of database queries, we could have one assembly containing [SQLite](https://www.sqlite.org/index.html) queries and another containing the [MySql](https://en.wikipedia.org/wiki/MySQL) equivalents of the same queries.

For further customization we can also specify how to match a given property to an embedded resource.

```c#
new ResourceBuilder().WithPredicate((resourceName, requestingProperty) => true);
```

Finally we can also customize how to process the resource stream that is read from the assembly. The default here is to  read it as UTF-8.

```c#
new ResourceBuilder().WithTextProcessor((resourceInfo) => resourceInfo.Stream.ReadAsUTF8())
```

Enjoy!





