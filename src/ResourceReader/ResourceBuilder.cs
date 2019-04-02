using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace ResourceReader
{
    public delegate bool ResourcePredicate(string resourceName, PropertyInfo requestingProperty);

    public abstract class ResourceRepository
    {
        private readonly ConcurrentDictionary<PropertyInfo, string> resourceCache = new ConcurrentDictionary<PropertyInfo, string>();

        private readonly ResourcePredicate predicate;
        private readonly Func<Stream, string> textProcessor;
        private readonly ConcurrentDictionary<string, PropertyInfo> propertyMap;

        private readonly List<(Assembly assembly, string resourcename)> allResources;

        public ResourceRepository(Assembly[] assemblies, ResourcePredicate predicate, Func<Stream, string> textProcessor)
        {
            this.predicate = predicate;
            this.textProcessor = textProcessor;
            var properties = this.GetType().GetProperties();
            propertyMap = new ConcurrentDictionary<string, PropertyInfo>(properties.ToDictionary(p => p.Name, p => p));
            allResources  = assemblies.SelectMany(a => a.GetManifestResourceNames(), (assemblyName, resourceName) => (assemblyName, resourceName)).ToList();
        }

        protected string Load(string name)
        {
            var property = propertyMap[name];
            return resourceCache.GetOrAdd(property, FindResource);
        }

        private string FindResource(PropertyInfo property)
        {
            var resources = allResources.Where(r => predicate(r.resourcename, property)).ToArray();

            if (resources.Length == 0)
            {
                throw new InvalidOperationException($"Unable to find any resources that matches '{property.Name}'");
            }

            if (resources.Length > 1)
            {
                throw new InvalidOperationException($"Found multiple resources macthing '{property.Name}' ()");
            }

            var resourceStream = resources[0].assembly.GetManifestResourceStream(resources[0].resourcename);
            return textProcessor(resourceStream);
        }
    }

       public class ResourceBuilder
    {
        private static MethodInfo loadMethod;

        private static ConstructorInfo constructor;

        private ResourcePredicate resourcePredicate;

        private Func<Stream, string> textProcessor;

        static ResourceBuilder()
        {
            loadMethod = typeof(ResourceRepository).GetMethod("Load", BindingFlags.Instance | BindingFlags.NonPublic);
            constructor = typeof(ResourceRepository).GetConstructors()[0];
        }

        private List<Assembly> resourceAssemblies = new List<Assembly>();

        public ResourceBuilder AddAssembly(Assembly assembly)
        {
            resourceAssemblies.Add(assembly);
            return this;
        }

        public ResourceBuilder WithPredicate(ResourcePredicate predicate)
        {
            this.resourcePredicate = predicate;
            return this;
        }

        public ResourceBuilder WithTextProcessor(Func<Stream, string> processor)
        {
            this.textProcessor = processor;
            return this;
        }

        /// <summary>
        /// Builds an instances of typeparamref name="T" that can be used to access embedded text file resources.
        /// </summary>
        /// <typeparam name="T">The interface type for which to build the resource accessor.</typeparam>
        /// <returns></returns>
        public T Build<T>()
        {
            var properties = typeof(T).GetProperties();
            var typeBuilder = GetTypeBuilder(typeof(T));

            ImplementConstructor(typeBuilder);

            ImplementProperties(properties, typeBuilder);

            var type = typeBuilder.CreateTypeInfo().AsType();
            var instance = Activator.CreateInstance(type, GetAssemblies(), GetResourcePredicate(), GetTextProcessor());

            return (T)instance;
        }

        private Func<Stream, string> GetTextProcessor()
        {
            if (textProcessor == null)
            {
                return (resourceStream) =>
                {
                    using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                };
            }
            else
            {
                return textProcessor;
            }
        }


        private ResourcePredicate GetResourcePredicate()
        {
            if (resourcePredicate == null)
            {
                return (resourceName, requestingProperty) =>
                {
                    var resourceNameWithOutExtension = Path.GetFileNameWithoutExtension(resourceName);
                    return resourceNameWithOutExtension.EndsWith(requestingProperty.Name, StringComparison.OrdinalIgnoreCase);
                };
            }
            else
            {
                return resourcePredicate;
            }
        }

        private Assembly[] GetAssemblies()
        {
            if (resourceAssemblies.Count == 0)
            {
                var defaultAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.GetName().Name.StartsWith("System", StringComparison.OrdinalIgnoreCase) && !a.IsDynamic);
                return defaultAssemblies.ToArray();
            }
            else
            {
                return resourceAssemblies.ToArray();
            }
        }

        private void ImplementProperties(PropertyInfo[] properties, TypeBuilder typeBuilder)
        {
            foreach (var property in properties)
            {
                var propertyBuilder = GetPropertyBuilder(typeBuilder, property);
                MethodInfo getMethod = property.GetMethod;
                var methodBuilder = GetMethodBuilder(typeBuilder, getMethod);

                var ilGenerator = methodBuilder.GetILGenerator();

                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldstr, property.Name);
                ilGenerator.Emit(OpCodes.Call, loadMethod);
                ilGenerator.Emit(OpCodes.Ret);

                propertyBuilder.SetGetMethod(methodBuilder);
            }
        }

        private void ImplementConstructor(TypeBuilder typeBuilder)
        {
            MethodAttributes methodAttributes = constructor.Attributes | MethodAttributes.Public;
            CallingConventions callingConvention = constructor.CallingConvention;
            Type[] parameterTypes = constructor.GetParameters().Select(p => p.ParameterType).ToArray();

            var constructorBuilder = typeBuilder.DefineConstructor(methodAttributes, callingConvention, parameterTypes);

            foreach (var parameterInfo in constructor.GetParameters())
            {
                constructorBuilder.DefineParameter(
                    parameterInfo.Position + 1,
                    parameterInfo.Attributes,
                    parameterInfo.Name);
            }

            var generator = constructorBuilder.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Ldarg_3);
            generator.Emit(OpCodes.Call, constructor);
            generator.Emit(OpCodes.Ret);
        }



        private MethodBuilder GetMethodBuilder(TypeBuilder typeBuilder, MethodInfo targetMethod)
        {
            MethodAttributes methodAttributes;

            string methodName = targetMethod.Name;

            Type declaringType = targetMethod.DeclaringType;

            methodAttributes = targetMethod.Attributes ^ MethodAttributes.Abstract;

            var targetMethodParameters = targetMethod.GetParameters();
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(
                                            targetMethod.Name,
                                            methodAttributes,
                                            targetMethod.ReturnType,
                                            targetMethodParameters.Select(p => p.ParameterType).ToArray());

            return methodBuilder;
        }


        private PropertyBuilder GetPropertyBuilder(TypeBuilder typeBuilder, PropertyInfo property)
            => typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, new[] { property.PropertyType });

        private static TypeBuilder GetTypeBuilder(Type interfaceType)
        {
            ModuleBuilder moduleBuilder = GetModuleBuilder();
            const TypeAttributes typeAttributes = TypeAttributes.Public | TypeAttributes.Class;
            var targetType = typeof(ResourceRepository);

            var typeName = targetType.Name + interfaceType.Name;

            var typeBuilder = moduleBuilder.DefineType(typeName, typeAttributes, targetType, new Type[] { interfaceType });
            return typeBuilder;
        }

        private static ModuleBuilder GetModuleBuilder()
        {
            AssemblyBuilder assemblyBuilder = GetAssemblyBuilder();
            return assemblyBuilder.DefineDynamicModule("ResourceRepository");
        }

        private static AssemblyBuilder GetAssemblyBuilder()
        {
            var assemblybuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ResourceRepository"), AssemblyBuilderAccess.Run);
            return assemblybuilder;
        }
    }
}
