using System.Text.Json.Nodes;

namespace ShipMate.Application.Ai;

/// <summary>
/// Turns a schema template into the final JSON Schema: every <c>"x-enum": "TypeName"</c> marker is replaced
/// by an <c>enum</c> list generated from that C# enum, so allowed values are never written by hand.
/// </summary>
public static class AiSchemaBuilder
{
    private const string EnumMarker = "x-enum";
    private const string EnumKeyword = "enum";

    public static string Build(string schemaTemplate, params Type[] enumTypes)
    {
        var enumTypesByName = enumTypes.ToDictionary(type => type.Name);
        var schema = JsonNode.Parse(schemaTemplate)
            ?? throw new InvalidOperationException("The AI schema template is empty.");

        ReplaceEnumMarkers(schema, enumTypesByName);
        return schema.ToJsonString();
    }

    private static void ReplaceEnumMarkers(JsonNode node, IReadOnlyDictionary<string, Type> enumTypesByName)
    {
        if (node is JsonObject obj)
        {
            if (obj[EnumMarker] is JsonValue marker)
            {
                var typeName = marker.GetValue<string>();
                if (!enumTypesByName.TryGetValue(typeName, out var enumType))
                {
                    throw new InvalidOperationException($"The AI schema references enum '{typeName}', which was not supplied.");
                }

                obj.Remove(EnumMarker);
                obj[EnumKeyword] = new JsonArray(Enum.GetNames(enumType)
                    .Select(name => (JsonNode)AiJson.NamingPolicy.ConvertName(name))
                    .ToArray());
            }

            foreach (var child in obj.Select(property => property.Value).OfType<JsonNode>().ToList())
            {
                ReplaceEnumMarkers(child, enumTypesByName);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array.OfType<JsonNode>().ToList())
            {
                ReplaceEnumMarkers(item, enumTypesByName);
            }
        }
    }
}
