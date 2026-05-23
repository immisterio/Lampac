using System.Runtime.CompilerServices;

namespace Shared.Services.Utilities;

public static class IPNetwork
{
    public static bool IsLocalIp(ReadOnlySpan<char> ip, bool forced = false)
    {
        if (ip.IsEmpty || (!forced && CoreInit.conf.BaseModule.NotCheckLocalIp))
            return false;

        if (TryParseIPv4(ip, out uint ipv4))
            return IsLocalIPv4(ipv4);

        if (TryParseIPv6(ip, out ushort g0, out bool isLoopback))
        {
            // ::1 handled by IsLoopback above
            if (isLoopback)
                return true;

            // fc00::/7 Unique Local Address
            byte firstByte = (byte)(g0 >> 8);
            return (firstByte & 0xfe) == 0xfc;
        }

        return false;
    }

    private static bool IsLocalIPv4(uint ip)
    {
        byte b0 = (byte)(ip >> 24);
        byte b1 = (byte)(ip >> 16);

        // 10.0.0.0/8
        if (b0 == 10)
            return true;

        // 127.0.0.0/8
        if (b0 == 127)
            return true;

        // 192.168.0.0/16
        if (b0 == 192 && b1 == 168)
            return true;

        // 172.16.0.0/12
        if (b0 == 172 && b1 >= 16 && b1 <= 31)
            return true;

        return false;
    }

    private static bool TryParseIPv4(ReadOnlySpan<char> s, out uint value)
    {
        value = 0;

        int partCount = 0;
        int current = 0;
        int digits = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if ((uint)(c - '0') <= 9)
            {
                current = current * 10 + (c - '0');

                if (++digits > 3 || current > 255)
                    return false;

                continue;
            }

            if (c == '.')
            {
                if (digits == 0 || partCount >= 3)
                    return false;

                value = (value << 8) | (byte)current;

                partCount++;
                current = 0;
                digits = 0;
                continue;
            }

            return false;
        }

        if (digits == 0 || partCount != 3)
            return false;

        value = (value << 8) | (byte)current;
        return true;
    }

    private static bool TryParseIPv6(ReadOnlySpan<char> s, out ushort firstGroup, out bool isLoopback)
    {
        firstGroup = 0;
        isLoopback = false;

        if (s.IsEmpty)
            return false;

        Span<ushort> groups = stackalloc ushort[8];

        int groupIndex = 0;
        int compressIndex = -1;
        int i = 0;

        if (s.Length >= 2 && s[0] == ':' && s[1] == ':')
        {
            compressIndex = 0;
            i = 2;

            if (i == s.Length)
            {
                // ::
                firstGroup = 0;
                isLoopback = false;
                return true;
            }
        }
        else if (s[0] == ':')
        {
            return false;
        }

        while (i < s.Length)
        {
            if (groupIndex >= 8)
                return false;

            int tokenStart = i;
            bool hasDot = false;

            while (i < s.Length && s[i] != ':')
            {
                if (s[i] == '.')
                    hasDot = true;

                i++;
            }

            ReadOnlySpan<char> token = s.Slice(tokenStart, i - tokenStart);

            if (token.IsEmpty)
                return false;

            if (hasDot)
            {
                // IPv4-embedded IPv6, e.g. ::ffff:192.168.1.1
                // IPv4 tail must be the final token.
                if (i != s.Length || groupIndex > 6)
                    return false;

                if (!TryParseIPv4(token, out uint ipv4))
                    return false;

                groups[groupIndex++] = (ushort)(ipv4 >> 16);
                groups[groupIndex++] = (ushort)ipv4;
                break;
            }

            if (!TryParseIPv6Group(token, out ushort group))
                return false;

            groups[groupIndex++] = group;

            if (i >= s.Length)
                break;

            // s[i] == ':'
            i++;

            if (i >= s.Length)
                return false;

            if (s[i] == ':')
            {
                if (compressIndex >= 0)
                    return false;

                compressIndex = groupIndex;
                i++;

                if (i >= s.Length)
                    break;
            }
        }

        if (compressIndex >= 0)
        {
            int zerosToInsert = 8 - groupIndex;

            if (zerosToInsert < 1)
                return false;

            for (int src = groupIndex - 1, dst = 7; src >= compressIndex; src--, dst--)
                groups[dst] = groups[src];

            for (int z = 0; z < zerosToInsert; z++)
                groups[compressIndex + z] = 0;

            groupIndex = 8;
        }

        if (groupIndex != 8)
            return false;

        firstGroup = groups[0];

        // ::1
        isLoopback =
            groups[0] == 0 &&
            groups[1] == 0 &&
            groups[2] == 0 &&
            groups[3] == 0 &&
            groups[4] == 0 &&
            groups[5] == 0 &&
            groups[6] == 0 &&
            groups[7] == 1;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseIPv6Group(ReadOnlySpan<char> s, out ushort value)
    {
        value = 0;

        if ((uint)s.Length - 1u >= 4u)
            return false;

        int result = 0;

        for (int i = 0; i < s.Length; i++)
        {
            int hex = FromHex(s[i]);

            if (hex < 0)
                return false;

            result = (result << 4) | hex;
        }

        value = (ushort)result;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromHex(char c)
    {
        if ((uint)(c - '0') <= 9)
            return c - '0';

        c = (char)(c | 0x20);

        if ((uint)(c - 'a') <= 5)
            return c - 'a' + 10;

        return -1;
    }
}
