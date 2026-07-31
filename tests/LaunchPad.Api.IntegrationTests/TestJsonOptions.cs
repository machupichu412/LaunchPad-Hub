using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Mirrors the API's own JSON options (Program.cs AddJsonOptions): camelCase
/// property matching plus string enums. A bare `new JsonSerializerOptions()`
/// defaults to case-SENSITIVE property matching, which silently leaves every
/// property at its default value against camelCase JSON instead of throwing —
/// or, for enums, throws instead of parsing the string value at all.
/// </summary>
public static class TestJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
