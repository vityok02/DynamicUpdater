using System.Text.Json;
using System.Text.Json.Serialization;

namespace Module.Api;

internal static class DynamicJsonContext
{
    public static readonly DynamicJsonSerializerContext Default = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    });
}
