namespace Shared.Services.Utilities;

public static class UnicTo
{
    const string ArrayListToNumber = "1234567890";
    const string ArrayList = "qwertyuioplkjhgfdsazxcvbnmQWERTYUIOPLKJHGFDSAZXCVBNM1234567890";

    public static string Code(int size = 8)
    {
        return string.Create(size, ArrayList, (span, chars) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = chars[Random.Shared.Next(chars.Length)];
            }
        });
    }

    public static string Number(int size = 8)
    {
        return string.Create(size, ArrayListToNumber, (span, chars) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = chars[Random.Shared.Next(chars.Length)];
            }
        });
    }
}
