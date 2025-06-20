﻿using Shared.Model.Online;

public static class HeadersModelExtensions
{
    public static Dictionary<string, string>? ToDictionary(this IEnumerable<HeadersModel> headers)
    {
        if (headers == null)
            return null;

        var result = new Dictionary<string, string>();
        foreach (var h in headers)
            result.TryAdd(h.name, h.val);

        return result;
    }
}