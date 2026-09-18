using System.Text.Json.Nodes;

namespace AppleQueue.Cli.Json;

/// <summary>
/// The backend is the source of truth for the shape of its responses, and it has
/// changed key names before, so everything here is tolerant: a missing or
/// wrong-typed value reads as null rather than throwing.
/// </summary>
internal static class JsonExtensions
{
    public static string? Str(this JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var node) || node is null) return null;
        try
        {
            return node.GetValueKind() switch
            {
                System.Text.Json.JsonValueKind.String => node.GetValue<string>(),
                System.Text.Json.JsonValueKind.Number => node.ToJsonString(),
                _ => null,
            };
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static bool? Bool(this JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var node) || node is null) return null;
        return node.GetValueKind() switch
        {
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            _ => null,
        };
    }

    public static int? Int(this JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var node) || node is null) return null;
        if (node.GetValueKind() != System.Text.Json.JsonValueKind.Number) return null;
        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static JsonObject? Obj(this JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var node)) return null;
        return node as JsonObject;
    }

    public static JsonArray? Arr(this JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var node)) return null;
        return node as JsonArray;
    }

    /// <summary>The first of <paramref name="keys"/> that holds an object.</summary>
    public static JsonObject? FirstObj(this JsonObject? obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            var found = obj.Obj(key);
            if (found is not null) return found;
        }

        return null;
    }

    public static IReadOnlyList<string> Strings(this JsonArray? array)
    {
        if (array is null) return [];
        var values = new List<string>(array.Count);
        foreach (var node in array)
        {
            if (node is null) continue;
            if (node.GetValueKind() == System.Text.Json.JsonValueKind.String) values.Add(node.GetValue<string>());
        }

        return values;
    }

    public static JsonArray ToJsonArray(this IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values) array.Add(JsonValue.Create(value));
        return array;
    }

    public static JsonArray ToJsonArray(this IEnumerable<int> values)
    {
        var array = new JsonArray();
        foreach (var value in values) array.Add(JsonValue.Create(value));
        return array;
    }
}
