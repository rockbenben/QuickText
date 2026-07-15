using System.Text.Encodings.Web;
using System.Text.Json;

namespace QuickText.Core.Persistence;

public static class JsonConfig
{
    public static readonly JsonSerializerOptions Write = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static readonly JsonSerializerOptions Read = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
