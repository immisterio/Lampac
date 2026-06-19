using Microsoft.IO;
using Shared.Services.Pools;
using System;
using System.Buffers.Binary;
using System.IO;

namespace GStreamer;

public readonly record struct Segment(
    RecyclableMemoryStream audio,
    RecyclableMemoryStream video,
    double startSeconds
);

public class Mp4BoxReader
{
    readonly Action<byte[]> _onInit;
    readonly Action<Segment> _onSegment;

    MemoryStream _init = new();
    MemoryStream _pending = new MemoryStream(128 * 1024);

    RecyclableMemoryStream _audioPart;
    RecyclableMemoryStream _videoPart;

    bool _initDone;
    uint _lastMoofTrackId;
    uint _videoTimescale;
    double _segmentStartSeconds = -1;

    const uint VideoTrackId = 1;
    const uint AudioTrackId = 2;

    const uint BoxStyp = ((uint)'s' << 24) | ((uint)'t' << 16) | ((uint)'y' << 8) | 'p';
    const uint BoxMoov = ((uint)'m' << 24) | ((uint)'o' << 16) | ((uint)'o' << 8) | 'v';
    const uint BoxMoof = ((uint)'m' << 24) | ((uint)'o' << 16) | ((uint)'o' << 8) | 'f';
    const uint BoxMdat = ((uint)'m' << 24) | ((uint)'d' << 16) | ((uint)'a' << 8) | 't';
    const uint BoxTrak = ((uint)'t' << 24) | ((uint)'r' << 16) | ((uint)'a' << 8) | 'k';
    const uint BoxTkhd = ((uint)'t' << 24) | ((uint)'k' << 16) | ((uint)'h' << 8) | 'd';
    const uint BoxMdia = ((uint)'m' << 24) | ((uint)'d' << 16) | ((uint)'i' << 8) | 'a';
    const uint BoxMdhd = ((uint)'m' << 24) | ((uint)'d' << 16) | ((uint)'h' << 8) | 'd';
    const uint BoxTraf = ((uint)'t' << 24) | ((uint)'r' << 16) | ((uint)'a' << 8) | 'f';
    const uint BoxTfhd = ((uint)'t' << 24) | ((uint)'f' << 16) | ((uint)'h' << 8) | 'd';
    const uint BoxTfdt = ((uint)'t' << 24) | ((uint)'f' << 16) | ((uint)'d' << 8) | 't';

    public Mp4BoxReader(Action<byte[]> onInit, Action<Segment> onSegment)
    {
        _onInit = onInit;
        _onSegment = onSegment;
    }

    public void ResetSegment()
    {
        if (_videoPart != null)
            _videoPart.Dispose();
        _videoPart = PoolInvk.msm.GetStream();

        if (_audioPart != null)
            _audioPart.Dispose();
        _audioPart = PoolInvk.msm.GetStream();

        _lastMoofTrackId = 0;
        _segmentStartSeconds = -1;
    }

    public void SeekReset()
    {
        _initDone = false;
        _videoTimescale = 0;
        _segmentStartSeconds = -1;

        _init.SetLength(0);
        _pending.SetLength(0);
    }

    public void Push(Gst.Buffer buffer, int size)
    {
        int position = (int)_pending.Length;
        _pending.SetLength(position + size);

        if (!_pending.TryGetBuffer(out ArraySegment<byte> segment) || segment.Array == null)
            throw new InvalidOperationException("MemoryStream buffer is not accessible.");

        Span<byte> dst = segment.Array.AsSpan(
            segment.Offset + position,
            size
        );

        nuint copied = buffer.Extract(0, dst);
        if (copied == 0)
        {
            // откатываем Length назад, ничего не записали
            _pending.SetLength(position);
            return;
        }

        // на всякий случай, если Extract скопировал меньше
        if ((int)copied != size)
            _pending.SetLength(position + (int)copied);

        _pending.Position = 0;

        while (TryReadBox(_pending, out uint type, out ReadOnlySpan<byte> box))
        {
            if (!_initDone && (type == BoxStyp || type == BoxMoof))
            {
                _initDone = true;

                byte[] init = _init.ToArray();

                _videoTimescale = GetTrackTimescale(
                    init,
                    VideoTrackId
                );

                _onInit(init);
            }

            if (!_initDone)
            {
                // ждем ftyp/moov/free и похожие init-box'ы
                // mdat до init это провал
                if (type == BoxMdat)
                    throw new InvalidOperationException("Bad init");

                _init.Write(box);
                continue;
            }

            if (type == BoxMoof)
            {
                _lastMoofTrackId = GetMoofTrackId(
                    box,
                    out ulong? decodeTime
                );

                if (_lastMoofTrackId == AudioTrackId)
                {
                    _audioPart.Write(box);
                }
                else if (_lastMoofTrackId == VideoTrackId)
                {
                    _videoPart.Write(box);

                    if (_videoTimescale > 0 && decodeTime.HasValue)
                    {
                        _segmentStartSeconds =
                            (double)decodeTime.Value / _videoTimescale;
                    }
                }

                continue;
            }

            if (type == BoxMdat)
            {
                if (_lastMoofTrackId == AudioTrackId)
                    _audioPart.Write(box);
                else if (_lastMoofTrackId == VideoTrackId)
                    _videoPart.Write(box);

                if (_audioPart.Length > 0 && _videoPart.Length > 0)
                {
                    _videoPart.Position = 0;
                    _audioPart.Position = 0;

                    _onSegment(new Segment(
                        _audioPart,
                        _videoPart,
                        _segmentStartSeconds
                    ));

                    break;
                }

                continue;
            }

            // styp, sidx и прочие box'ы игнорируем
        }

        CompactPending(); // ужасный метод для hot path, но я куст и пока будет так
    }

    static uint GetMoofTrackId(ReadOnlySpan<byte> moof, out ulong? decodeTime)
    {
        decodeTime = null;

        if (moof.Length < 8)
            return 0;

        int pos = 8;
        int end = moof.Length;

        while (pos + 8 <= end)
        {
            uint size = BinaryPrimitives.ReadUInt32BigEndian(
                moof.Slice(pos, 4)
            );

            uint type = BinaryPrimitives.ReadUInt32BigEndian(
                moof.Slice(pos + 4, 4)
            );

            if (size == 1 || size == 0)
                return 0;

            if (size < 8 || pos + size > end)
                return 0;

            if (type == BoxTraf)
            {
                uint trackId = GetTrafTrackId(
                    moof,
                    pos + 8,
                    pos + (int)size,
                    out decodeTime
                );

                if (trackId != 0)
                    return trackId;
            }

            pos += (int)size;
        }

        return 0;
    }

    static uint GetTrafTrackId(ReadOnlySpan<byte> data, int start, int end, out ulong? decodeTime)
    {
        decodeTime = null;

        uint trackId = 0;
        int pos = start;

        while (pos + 8 <= end)
        {
            uint size = BinaryPrimitives.ReadUInt32BigEndian(
                data.Slice(pos, 4)
            );

            uint type = BinaryPrimitives.ReadUInt32BigEndian(
                data.Slice(pos + 4, 4)
            );

            if (size == 1 || size == 0)
                return 0;

            if (size < 8 || pos + size > end)
                return 0;

            if (type == BoxTfhd && size >= 16)
            {
                trackId = BinaryPrimitives.ReadUInt32BigEndian(
                    data.Slice(pos + 12, 4)
                );
            }
            else if (type == BoxTfdt && size >= 16)
            {
                byte version = data[pos + 8];

                if (version == 1)
                {
                    if (size >= 20)
                    {
                        decodeTime = BinaryPrimitives.ReadUInt64BigEndian(
                            data.Slice(pos + 12, 8)
                        );
                    }
                }
                else
                {
                    decodeTime = BinaryPrimitives.ReadUInt32BigEndian(
                        data.Slice(pos + 12, 4)
                    );
                }
            }

            pos += (int)size;
        }

        return trackId;
    }

    static uint GetTrackTimescale(ReadOnlySpan<byte> init, uint requiredTrackId)
    {
        int pos = 0;

        while (TryReadBox(
            init,
            ref pos,
            out uint type,
            out ReadOnlySpan<byte> box
        ))
        {
            if (type != BoxMoov)
                continue;

            int moovPos = 8;

            while (TryReadBox(
                box,
                ref moovPos,
                out uint childType,
                out ReadOnlySpan<byte> child
            ))
            {
                if (childType != BoxTrak)
                    continue;

                uint trackId = 0;
                uint timescale = 0;
                int trakPos = 8;

                while (TryReadBox(
                    child,
                    ref trakPos,
                    out uint trakType,
                    out ReadOnlySpan<byte> trakBox
                ))
                {
                    if (trakType == BoxTkhd)
                    {
                        int offset = trakBox.Length > 8 && trakBox[8] == 1
                            ? 28
                            : 20;

                        if (trakBox.Length >= offset + 4)
                        {
                            trackId = BinaryPrimitives.ReadUInt32BigEndian(
                                trakBox.Slice(offset, 4)
                            );
                        }
                    }
                    else if (trakType == BoxMdia)
                    {
                        timescale = GetMdiaTimescale(trakBox);
                    }
                }

                if (trackId == requiredTrackId)
                    return timescale;
            }
        }

        return 0;
    }

    static uint GetMdiaTimescale(ReadOnlySpan<byte> mdia)
    {
        int pos = 8;

        while (TryReadBox(
            mdia,
            ref pos,
            out uint type,
            out ReadOnlySpan<byte> box
        ))
        {
            if (type != BoxMdhd)
                continue;

            int offset = box.Length > 8 && box[8] == 1
                ? 28
                : 20;

            if (box.Length >= offset + 4)
            {
                return BinaryPrimitives.ReadUInt32BigEndian(
                    box.Slice(offset, 4)
                );
            }
        }

        return 0;
    }

    static bool TryReadBox(ReadOnlySpan<byte> data, ref int position, out uint type, out ReadOnlySpan<byte> box)
    {
        type = 0;
        box = default;

        int start = position;

        if (start < 0 || start + 8 > data.Length)
            return false;

        ulong size = BinaryPrimitives.ReadUInt32BigEndian(
            data.Slice(start, 4)
        );

        type = BinaryPrimitives.ReadUInt32BigEndian(
            data.Slice(start + 4, 4)
        );

        int headerSize = 8;

        if (size == 1)
        {
            if (start + 16 > data.Length)
                return false;

            size = BinaryPrimitives.ReadUInt64BigEndian(
                data.Slice(start + 8, 8)
            );

            headerSize = 16;
        }
        else if (size == 0)
        {
            size = (ulong)(data.Length - start);
        }

        if (size < (ulong)headerSize || size > int.MaxValue)
            return false;

        int boxSize = (int)size;

        if (boxSize > data.Length - start)
            return false;

        box = data.Slice(start, boxSize);
        position = start + boxSize;

        return true;
    }

    static bool TryReadBox(MemoryStream ms, out uint type, out ReadOnlySpan<byte> box)
    {
        type = 0;
        box = default;

        long start = ms.Position;
        long available = ms.Length - start;

        if (available < 8)
            return false;

        if (!ms.TryGetBuffer(out ArraySegment<byte> segment) || segment.Array == null)
            throw new InvalidOperationException("MemoryStream buffer is not accessible.");

        var buffer = segment.Array.AsSpan(
            segment.Offset + (int)start,
            (int)available
        );

        ulong size = BinaryPrimitives.ReadUInt32BigEndian(buffer[..4]);
        type = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4, 4));

        if (size == 0)
            return false;

        int headerSize = 8;

        if (size == 1)
        {
            if (available < 16)
                return false;

            size = BinaryPrimitives.ReadUInt64BigEndian(
                buffer.Slice(8, 8)
            );

            headerSize = 16;
        }

        if (size < (ulong)headerSize)
            return false;

        if ((ulong)available < size)
            return false;

        box = buffer[..(int)size];
        ms.Position = start + (long)size;

        return true;
    }

    void CompactPending()
    {
        int pos = (int)_pending.Position;
        int len = (int)_pending.Length;
        int rest = len - pos;

        if (rest <= 0)
        {
            _pending.SetLength(0);
            _pending.Position = 0;
            return;
        }

        // если ничего не прочитали, компактить нечего
        if (pos == 0)
            return;

        if (!_pending.TryGetBuffer(out ArraySegment<byte> segment) || segment.Array == null)
            throw new InvalidOperationException("MemoryStream buffer is not accessible.");

        Buffer.BlockCopy(
            segment.Array,
            segment.Offset + pos,
            segment.Array,
            segment.Offset,
            rest
        );

        _pending.SetLength(rest);
        _pending.Position = 0;
    }

    public void Dispose()
    {
        _audioPart?.Dispose();
        _audioPart = null;

        _videoPart?.Dispose();
        _videoPart = null;

        _pending?.Dispose();
        _pending = null;

        _init?.Dispose();
        _init = null;
    }
}