using System.Text.Json;

namespace Blockcore.Features.NodeHost
{
    public static class DefaultJsonSerializerOptions
    {
        public static JsonSerializerOptions Options => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IgnoreNullValues = true
        };
    }
}
