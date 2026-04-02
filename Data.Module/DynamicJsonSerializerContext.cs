using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Module.Api;

public class DynamicJsonSerializerContext : JsonSerializerContext, IDisposable
{
    private ConditionalWeakTable<Type, JsonTypeInfo> _cache = new();
    private JsonSerializerOptions? _options;

    protected override JsonSerializerOptions? GeneratedSerializerOptions => _options;

    public new JsonSerializerOptions Options => _options ?? throw new ObjectDisposedException(nameof(DynamicJsonSerializerContext));

    public DynamicJsonSerializerContext(JsonSerializerOptions options) : base(options)
    {
        _options = options;
    }

    public override JsonTypeInfo? GetTypeInfo(Type type)
    {
        if (_cache == null) return null;
        return _cache.GetValue(type, t => CreateTypeInfo(t));
    }

    private JsonTypeInfo CreateTypeInfo(Type t)
    {
        var typeInfo = JsonTypeInfo.CreateJsonTypeInfo(t, Options);

        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object:
                var ctor = t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (ctor != null)
                    typeInfo.CreateObject = () => Activator.CreateInstance(t)!;

                foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;

                    var jsonProp = typeInfo.CreateJsonPropertyInfo(prop.PropertyType, prop.Name);
                    jsonProp.Name = Options.PropertyNamingPolicy?.ConvertName(prop.Name) ?? prop.Name;

                    var p = prop;
                    jsonProp.Get = obj => p.GetValue(obj);
                    if (p.CanWrite)
                        jsonProp.Set = (obj, val) => p.SetValue(obj, val);

                    typeInfo.Properties.Add(jsonProp);
                }
                break;

            case JsonTypeInfoKind.Enumerable:
                var elementType = GetEnumerableElementType(t);
                if (elementType != null)
                {
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    if (t.IsAssignableFrom(listType))
                        typeInfo.CreateObject = () => Activator.CreateInstance(listType)!;
                }
                break;
        }
        return typeInfo;
    }

    private static Type? GetEnumerableElementType(Type t)
    {
        if (t.IsGenericType && t.GetGenericArguments().Length == 1)
            return t.GetGenericArguments()[0];
        return t.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    public void Dispose()
    {
        _cache = null!;
        _options = null;
    }
}
