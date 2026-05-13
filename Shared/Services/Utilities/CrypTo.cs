using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Shared.Services.Utilities;

public class CrypTo
{
    #region ThreadStatic
    [ThreadStatic]
    private static byte[] _threadByteBuffer;
    readonly static int _threadByteSize = 16 * 1024;

    [ThreadStatic]
    private static char[] _threadCharBuffer;
    readonly static int _threadCharSize = 1024;
    #endregion

    #region md5 - StringBuilder
    public static string md5(StringBuilder text)
    {
        if (text == null || text.Length == 0)
            return string.Empty;

        char[] _threadBuffer = null;
        BufferCharPool charBuf = null;

        if (text.Length > _threadCharSize)
            charBuf = new BufferCharPool(text.Length);
        else
            _threadBuffer = _threadCharBuffer ??= new char[_threadCharSize];

        try
        {
            Span<char> buffer = charBuf != null
                ? charBuf.Span
                : _threadBuffer;

            text.CopyTo(0, buffer, text.Length);

            return md5(buffer.Slice(0, text.Length));
        }
        finally
        {
            charBuf?.Dispose();
        }
    }
    #endregion

    #region md5 - string
    public static string md5(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return string.Empty;

        byte[] _threadByte = null;
        BufferBytePool byteBuf = null;
        int capacity = Encoding.UTF8.GetByteCount(text);

        if (capacity > _threadByteSize)
            byteBuf = new BufferBytePool(capacity);
        else
            _threadByte = _threadByteBuffer ??= new byte[_threadByteSize];

        try
        {
            Span<byte> utf8 = byteBuf != null
                ? byteBuf.Span
                : _threadByte;

            int bytesWritten = Encoding.UTF8.GetBytes(text, utf8);
            if (0 >= bytesWritten)
                return string.Empty;

            Span<byte> hash = stackalloc byte[16];  // MD5 = 16 байт
            if (!MD5.TryHashData(utf8.Slice(0, bytesWritten), hash, out _))
                return string.Empty;

            Span<char> hex = stackalloc char[32];   // 16 байт -> 32 hex-символа
            if (!Convert.TryToHexStringLower(hash, hex, out _))
                return string.Empty;

            return new string(hex);
        }
        finally
        {
            byteBuf?.Dispose();
        }
    }
    #endregion

    #region md5Writer
    public static void md5Writer(ReadOnlySpan<char> text, StringBuilder sb)
    {
        if (text.IsEmpty)
            return;

        byte[] _threadByte = null;
        BufferBytePool byteBuf = null;
        int capacity = Encoding.UTF8.GetByteCount(text);

        if (capacity > _threadByteSize)
            byteBuf = new BufferBytePool(capacity);
        else
            _threadByte = _threadByteBuffer ??= new byte[_threadByteSize];

        try
        {
            Span<byte> utf8 = byteBuf != null
                ? byteBuf.Span
                : _threadByte;

            int bytesWritten = Encoding.UTF8.GetBytes(text, utf8);
            if (0 >= bytesWritten)
                return;

            Span<byte> hash = stackalloc byte[16];  // MD5 = 16 байт
            if (!MD5.TryHashData(utf8.Slice(0, bytesWritten), hash, out _))
                return;

            Span<char> hex = stackalloc char[32];   // 16 байт -> 32 hex-символа
            if (!Convert.TryToHexStringLower(hash, hex, out _))
                return;

            sb.Append(hex);
        }
        finally
        {
            byteBuf?.Dispose();
        }
    }
    #endregion

    #region md5 - unsafe
    static unsafe string md5Native(ReadOnlySpan<char> text, int byteCount)
    {
        byte* nativeBuffer = (byte*)NativeMemory.Alloc((nuint)byteCount);
        Span<byte> utf8 = new Span<byte>(nativeBuffer, byteCount);

        try
        {
            Encoding.UTF8.GetBytes(text, utf8);

            Span<byte> hash = stackalloc byte[16];     // MD5 = 16 байт
            if (!MD5.TryHashData(utf8, hash, out _))
                return string.Empty;

            Span<char> hex = stackalloc char[32];      // 16 байт -> 32 hex-символа
            if (!Convert.TryToHexStringLower(hash, hex, out _))
                return string.Empty;

            return new string(hex);
        }
        finally
        {
            NativeMemory.Free(nativeBuffer);
        }
    }

    static string md5Stack(ReadOnlySpan<char> text, int byteCount)
    {
        try
        {
            Span<byte> utf8 = stackalloc byte[byteCount];

            Encoding.UTF8.GetBytes(text, utf8);

            Span<byte> hash = stackalloc byte[16];     // MD5 = 16 байт
            if (!MD5.TryHashData(utf8, hash, out _))
                return string.Empty;

            Span<char> hex = stackalloc char[32];      // 16 байт -> 32 hex-символа
            if (!Convert.TryToHexStringLower(hash, hex, out _))
                return string.Empty;

            return new string(hex);
        }
        catch { return string.Empty; }
    }
    #endregion

    #region md5File
    public static string md5File(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return string.Empty;

        try
        {
            Span<byte> hash = stackalloc byte[16];

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: PoolInvk.bufferSize))
            {
                int bytesWritten = MD5.HashData(stream, hash);
                if (bytesWritten != 16)
                    return string.Empty;
            }

            Span<char> hex = stackalloc char[32];
            if (!Convert.TryToHexStringLower(hash, hex, out _))
                return string.Empty;

            return new string(hex);
        }
        catch { return string.Empty; }
    }
    #endregion

    #region md5binary
    public static byte[] md5binary(string text)
    {
        if (text == null)
            return null;

        using (var md5 = MD5.Create())
        {
            var result = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            return result;
        }
    }
    #endregion

    #region Encrypt/Decrypt Query
    public static string EncryptQuery(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
            return string.Empty;

        return HttpUtility.UrlEncode(AesTo.Encrypt(value.Trim()));
    }

    public static string DecryptQuery(ReadOnlySpan<char> value)
    {
        return AesTo.Decrypt(value);
    }
    #endregion

    #region DecodeBase64
    public static string DecodeBase64(string base64Text)
    {
        if (string.IsNullOrEmpty(base64Text))
            return string.Empty;

        try
        {
            using (var nbuf = new BufferBytePool(Encoding.UTF8.GetByteCount(base64Text)))
            {
                if (Convert.TryFromBase64String(base64Text, nbuf.Span, out int bytesWritten))
                    return Encoding.UTF8.GetString(nbuf.Span.Slice(0, bytesWritten));
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "{Class} {CatchId}", "CrypTo", "id_bhkp1fwo");
        }

        return string.Empty;
    }

    public static void DecodeBase64(string base64Text, Action<ReadOnlySpan<char>> action)
    {
        if (string.IsNullOrEmpty(base64Text))
            return;

        try
        {
            using (var byteBuffer = new BufferBytePool(Encoding.UTF8.GetByteCount(base64Text)))
            {
                if (!Convert.TryFromBase64String(base64Text, byteBuffer.Span, out int bytesWritten))
                    return;

                ReadOnlySpan<byte> utf8Bytes = byteBuffer.Span.Slice(0, bytesWritten);

                using (var charBuffer = new BufferCharPool(Encoding.UTF8.GetCharCount(utf8Bytes)))
                {
                    int written = Encoding.UTF8.GetChars(utf8Bytes, charBuffer.Span);
                    action(charBuffer.Span.Slice(0, written));
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "{Class} {CatchId}", "CrypTo", "id_brqubxp1");
        }
    }
    #endregion

    #region Base64
    public static string Base64(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        byte[] _threadByte = null;
        BufferBytePool plainBuf = null;
        int capacity = Encoding.UTF8.GetByteCount(text);

        if (capacity > _threadByteSize)
            plainBuf = new BufferBytePool(capacity);
        else
            _threadByte = _threadByteBuffer ??= new byte[_threadByteSize];

        try
        {
            Span<byte> utf8 = plainBuf is not null
                ? plainBuf.Span
                : _threadByte.AsSpan(0, capacity);

            int bytesWritten = Encoding.UTF8.GetBytes(text, utf8);
            return Convert.ToBase64String(utf8.Slice(0, bytesWritten));
        }
        finally
        {
            plainBuf?.Dispose();
        }
    }

    public static void Base64(string text, Action<ReadOnlySpan<char>> action)
    {
        if (string.IsNullOrEmpty(text))
            return;

        byte[] _threadByte = null;
        BufferBytePool plainBuf = null;
        int capacity = Encoding.UTF8.GetByteCount(text);

        if (capacity > _threadByteSize)
            plainBuf = new BufferBytePool(capacity);
        else
            _threadByte = _threadByteBuffer ??= new byte[_threadByteSize];

        try
        {
            Span<byte> utf8 = plainBuf != null
                ? plainBuf.Span
                : _threadByte;

            int bytesWritten = Encoding.UTF8.GetBytes(text, utf8);
            capacity = ((bytesWritten + 2) / 3) * 4;

            char[] _threadChar = null;
            BufferCharPool base64Buf = null;

            if (capacity > _threadCharSize)
                base64Buf = new BufferCharPool(capacity);
            else
                _threadChar = _threadCharBuffer ??= new char[_threadCharSize];

            try
            {
                Span<char> base64 = base64Buf != null
                    ? base64Buf.Span
                    : _threadChar;

                if (Convert.TryToBase64Chars(utf8.Slice(0, bytesWritten), base64, out int charsWritten))
                    action.Invoke(base64.Slice(0, charsWritten));
            }
            finally
            {
                base64Buf?.Dispose();
            }
        }
        finally
        {
            plainBuf?.Dispose();
        }
    }
    #endregion
}
