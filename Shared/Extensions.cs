using Jint;
using System.Text.Json;

public static class Extensions
{
    public static Dictionary<string, string> ToDictionary(this IEnumerable<HeadersModel> headers)
    {
        if (headers == null)
            return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in headers)
            result.TryAdd(h.name, h.val);

        return result;
    }

    public static string ToLowerAndTrim(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        ReadOnlySpan<char> span = input.AsSpan().Trim();

        if (span.Length < 256)
        {
            Span<char> buffer = stackalloc char[span.Length];

            for (int i = 0; i < span.Length; i++)
                buffer[i] = char.ToLowerInvariant(span[i]);

            if (buffer.SequenceEqual(input))
                return input;

            return new string(buffer);
        }
        else
        {
            return string.Create(span.Length, span, (dest, src) =>
            {
                for (int i = 0; i < src.Length; i++)
                {
                    dest[i] = char.ToLowerInvariant(src[i]);
                }
            });
        }
    }

    public static T Invoke<T>(this Engine engine, string propertyName, params object[] arguments) where T : class
    {
        var result = engine.Invoke(propertyName, arguments);
        if (result == null || result.IsNull() || result.IsUndefined())
            return default;

        if (typeof(T) == typeof(string))
            return (T)(object)result.AsString();

        return JsonSerializer.Deserialize<T>(result.AsString());
    }

    async public static Task<T> InvokeAsync<T>(this Engine engine, string propertyName, params object[] arguments) where T : class
    {
        var result = await engine.InvokeAsync(propertyName, arguments);
        if (result == null || result.IsNull() || result.IsUndefined())
            return default;

        if (typeof(T) == typeof(string))
            return (T)(object)result.AsString();

        return JsonSerializer.Deserialize<T>(result.AsString());
    }
}