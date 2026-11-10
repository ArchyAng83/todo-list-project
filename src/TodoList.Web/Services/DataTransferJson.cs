using System.Text.Json;
using System.Text.Json.Serialization;

namespace TodoList.Web.Services;

/// <summary>
/// Shared JSON settings for the versioned data export. Enums are written as
/// names so the file stays readable and stable across releases.
/// </summary>
public static class DataTransferJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
