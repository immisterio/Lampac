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

    // moof держим целиком: внутри него нужны tfhd/tfdt
    MemoryStream _moof = new(16 * 1024);

    // Остаток Gst.Buffer после уже готового сегмента
    MemoryStream _deferred = new(64 * 1024);

    // Переиспользуемый буфер извлечения данных из Gst.Buffer
    readonly byte[] _readBuffer = new byte[64 * 1024];

    // Заголовок обычного box занимает 8 байт, extended-size box — 16 байт
    readonly byte[] _boxHeader = new byte[16];
    int _boxHeaderLength;
    int _boxHeaderRequired = 8;

    uint _currentBoxType;
    ulong _currentBoxRemaining;
    BoxTarget _currentTarget;

    RecyclableMemoryStream _audioPart;
    RecyclableMemoryStream _videoPart;

    bool _initDone;

    // Track ID из последнего moof: следующий mdat принадлежит этому треку
    uint _lastMoofTrackId;

    // Timescale треков из moov/trak/mdia/mdhd
    uint _videoTimescale;
    uint _audioTimescale;

    // Локальное начало сегмента и смещение timeline после seek
    double _segmentStartSeconds = -1;
    double _tfdtOffsetSeconds;

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

    enum BoxTarget
    {
        None,
        Init,
        Moof,
        Audio,
        Video
    }

    public Mp4BoxReader(Action<byte[]> onInit, Action<Segment> onSegment)
    {
        _onInit = onInit;
        _onSegment = onSegment;
    }

    /// <summary>
    /// Освобождает предыдущие части и начинает собирать новый сегмент
    /// </summary>
    public void ResetSegment()
    {
        // Если предыдущий запрос прервался посреди mdat, его начало уже было
        // записано в старый stream. Остаток этого mdat нужно отбросить
        if (_currentBoxType == BoxMdat && _currentBoxRemaining > 0)
            _currentTarget = BoxTarget.None;

        if (_videoPart != null)
            _videoPart.Dispose();

        _videoPart = PoolInvk.msm.GetStream();

        if (_audioPart != null)
            _audioPart.Dispose();

        _audioPart = PoolInvk.msm.GetStream();

        _lastMoofTrackId = 0;
        _segmentStartSeconds = -1;
    }

    /// <summary>
    /// Сбрасывает парсер после seek и задаёт смещение новой timeline
    /// </summary>
    public void SeekReset(double seconds = 0)
    {
        _initDone = false;

        _videoTimescale = 0;
        _audioTimescale = 0;

        _lastMoofTrackId = 0;
        _segmentStartSeconds = -1;

        _tfdtOffsetSeconds =
            double.IsFinite(seconds) && seconds > 0
                ? seconds
                : 0;

        _init.SetLength(0);
        _init.Position = 0;

        _moof.SetLength(0);
        _moof.Position = 0;

        _deferred.SetLength(0);
        _deferred.Position = 0;

        ResetBoxState();
    }

    /// <summary>
    /// Потоково разбирает MP4 box:
    /// init и mdat пишет сразу, целиком накапливает только moof
    /// </summary>
    public void Push(Gst.Buffer buffer, int size)
    {
        // Сначала дорабатываем редкий остаток предыдущего Gst.Buffer
        if (_deferred.Length > 0)
        {
            if (!_deferred.TryGetBuffer(out ArraySegment<byte> deferredSegment) || deferredSegment.Array == null)
                throw new InvalidOperationException("Deferred buffer is not accessible.");

            ReadOnlySpan<byte> deferredData = deferredSegment.Array.AsSpan(
                deferredSegment.Offset,
                (int)_deferred.Length
            );

            int consumed = ProcessBytes(
                deferredData,
                out bool segmentCompleted
            );

            if (segmentCompleted)
            {
                KeepDeferredRemainder(deferredData, consumed);
                AppendGstBufferToDeferred(buffer, 0, size);
                return;
            }

            _deferred.SetLength(0);
            _deferred.Position = 0;
        }

        int sourceOffset = 0;

        while (sourceOffset < size)
        {
            int requested = Math.Min(
                _readBuffer.Length,
                size - sourceOffset
            );

            nuint copiedValue = buffer.Extract(
                (nuint)sourceOffset,
                _readBuffer.AsSpan(0, requested)
            );

            int copied = (int)copiedValue;

            if (copied <= 0)
                return;

            int consumed = ProcessBytes(
                _readBuffer.AsSpan(0, copied),
                out bool segmentCompleted
            );

            sourceOffset += copied;

            if (segmentCompleted)
            {
                // Сохраняем только байты ПОСЛЕ готового сегмента
                if (consumed < copied)
                {
                    _deferred.Write(
                        _readBuffer.AsSpan(
                            consumed,
                            copied - consumed
                        )
                    );
                }

                if (sourceOffset < size)
                {
                    AppendGstBufferToDeferred(
                        buffer,
                        sourceOffset,
                        size - sourceOffset
                    );
                }

                _deferred.Position = 0;
                return;
            }

        }
    }

    /// <summary>
    /// Разбирает переданный кусок и возвращает число использованных байт
    /// </summary>
    int ProcessBytes(
        ReadOnlySpan<byte> data,
        out bool segmentCompleted
    )
    {
        segmentCompleted = false;
        int position = 0;

        while (position < data.Length)
        {
            // Сначала набираем 8 или 16 байт заголовка текущего box
            if (_boxHeaderLength < _boxHeaderRequired)
            {
                int copy = Math.Min(
                    _boxHeaderRequired - _boxHeaderLength,
                    data.Length - position
                );

                data.Slice(position, copy).CopyTo(
                    _boxHeader.AsSpan(_boxHeaderLength, copy)
                );

                _boxHeaderLength += copy;
                position += copy;

                if (_boxHeaderLength < _boxHeaderRequired)
                    break;

                if (_boxHeaderRequired == 8)
                {
                    uint size32 = BinaryPrimitives.ReadUInt32BigEndian(
                        _boxHeader.AsSpan(0, 4)
                    );

                    _currentBoxType = BinaryPrimitives.ReadUInt32BigEndian(
                        _boxHeader.AsSpan(4, 4)
                    );

                    if (size32 == 1)
                    {
                        _boxHeaderRequired = 16;
                        continue;
                    }

                    if (size32 == 0)
                    {
                        throw new NotSupportedException(
                            "MP4 box with size=0 cannot be parsed before end of stream."
                        );
                    }

                    BeginBox(size32, 8);
                }
                else
                {
                    ulong size64 = BinaryPrimitives.ReadUInt64BigEndian(
                        _boxHeader.AsSpan(8, 8)
                    );

                    BeginBox(size64, 16);
                }

                if (_currentBoxRemaining == 0)
                {
                    bool completed = CompleteBox();
                    ResetBoxState();

                    if (completed)
                    {
                        segmentCompleted = true;
                        break;
                    }
                }

                continue;
            }

            int bodySize = (int)Math.Min(
                (ulong)(data.Length - position),
                _currentBoxRemaining
            );

            if (bodySize <= 0)
                break;

            WriteCurrentBoxData(
                data.Slice(position, bodySize)
            );

            position += bodySize;
            _currentBoxRemaining -= (ulong)bodySize;

            if (_currentBoxRemaining == 0)
            {
                bool completed = CompleteBox();
                ResetBoxState();

                if (completed)
                {
                    segmentCompleted = true;
                    break;
                }
            }
        }

        return position;
    }

    /// <summary>
    /// Проверяет размер, завершает init при первом styp/moof
    /// и выбирает поток назначения текущего box
    /// </summary>
    void BeginBox(ulong size, int headerSize)
    {
        if (size < (ulong)headerSize)
            throw new InvalidDataException("Invalid MP4 box size.");

        if (_currentBoxType == BoxMoof && size > int.MaxValue)
            throw new InvalidDataException("moof is too large.");

        _currentBoxRemaining = size - (ulong)headerSize;
        _currentTarget = BoxTarget.None;

        // Первый media box завершает init. Сам styp/moof в init не входит
        if (!_initDone && (_currentBoxType == BoxStyp || _currentBoxType == BoxMoof))
            CompleteInit();

        if (!_initDone)
        {
            if (_currentBoxType == BoxMdat) // mdat до _initDone это провал
                throw new InvalidOperationException("Bad init");

            _currentTarget = BoxTarget.Init;
        }
        else if (_currentBoxType == BoxMoof)
        {
            _moof.SetLength(0);
            _moof.Position = 0;
            _currentTarget = BoxTarget.Moof;
        }
        else if (_currentBoxType == BoxMdat)
        {
            if (_lastMoofTrackId == AudioTrackId)
                _currentTarget = BoxTarget.Audio;
            else if (_lastMoofTrackId == VideoTrackId)
                _currentTarget = BoxTarget.Video;
        }

        // Заголовок является частью box и тоже сразу идёт в нужный поток
        WriteCurrentBoxData(
            _boxHeader.AsSpan(0, headerSize)
        );
    }

    /// <summary>
    /// Пишет кусок текущего box непосредственно в его поток назначения
    /// </summary>
    void WriteCurrentBoxData(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return;

        switch (_currentTarget)
        {
            case BoxTarget.Init:
                _init.Write(data);
                break;

            case BoxTarget.Moof:
                _moof.Write(data);
                break;

            case BoxTarget.Audio:
                _audioPart.Write(data);
                break;

            case BoxTarget.Video:
                _videoPart.Write(data);
                break;
        }
    }

    /// <summary>
    /// Завершает текущий box. Возвращает true, когда готов весь Segment
    /// </summary>
    bool CompleteBox()
    {
        if (_currentBoxType == BoxMoof)
        {
            CompleteMoof();
            return false;
        }

        if (_currentBoxType != BoxMdat)
            return false;

        if (_audioPart.Length <= 0 || _videoPart.Length <= 0)
            return false;

        _videoPart.Position = 0;
        _audioPart.Position = 0;

        _onSegment(new Segment(
            _audioPart,
            _videoPart,
            _segmentStartSeconds
        ));

        return true;
    }

    /// <summary>
    /// Разбирает полностью накопленный moof, изменяет tfdt
    /// и только после определения track_ID пишет его в audio/video
    /// </summary>
    void CompleteMoof()
    {
        if (!_moof.TryGetBuffer(out ArraySegment<byte> segment) || segment.Array == null)
            throw new InvalidOperationException("moof buffer is not accessible.");

        Span<byte> box = segment.Array.AsSpan(
            segment.Offset,
            (int)_moof.Length
        );

        _lastMoofTrackId = GetMoofTrackId(
            box,
            out ulong? decodeTime
        );

        uint timescale = 0;

        if (_lastMoofTrackId == AudioTrackId)
        {
            timescale = _audioTimescale;
        }
        else if (_lastMoofTrackId == VideoTrackId)
        {
            timescale = _videoTimescale;

            // Сохраняем локальное начало video fragment до изменения tfdt
            if (_videoTimescale > 0 && decodeTime.HasValue)
            {
                _segmentStartSeconds =
                    (double)decodeTime.Value / _videoTimescale;
            }
        }

        // После seek добавляем абсолютное смещение к tfdt обоих треков
        if (_tfdtOffsetSeconds > 0 && timescale > 0)
        {
            ShiftTfdt(
                box,
                timescale,
                _tfdtOffsetSeconds
            );
        }

        if (_lastMoofTrackId == AudioTrackId)
        {
            _audioPart.Write(box);
        }
        else if (_lastMoofTrackId == VideoTrackId)
        {
            _videoPart.Write(box);
        }

        _moof.SetLength(0);
        _moof.Position = 0;
    }

    /// <summary>
    /// Завершает init и читает timescale обоих треков
    /// </summary>
    void CompleteInit()
    {
        _initDone = true;

        byte[] init = _init.ToArray();

        _videoTimescale = GetTrackTimescale(
            init,
            VideoTrackId
        );

        _audioTimescale = GetTrackTimescale(
            init,
            AudioTrackId
        );

        _onInit(init);
    }

    /// <summary>
    /// Сбрасывает состояние заголовка для следующего top-level box
    /// </summary>
    void ResetBoxState()
    {
        _boxHeaderLength = 0;
        _boxHeaderRequired = 8;

        _currentBoxType = 0;
        _currentBoxRemaining = 0;
        _currentTarget = BoxTarget.None;
    }

    /// <summary>
    /// Оставляет в _deferred только неиспользованный хвост
    /// Вызывается лишь на границе уже готового сегмента
    /// </summary>
    void KeepDeferredRemainder(
        ReadOnlySpan<byte> data,
        int consumed
    )
    {
        int rest = data.Length - consumed;

        if (rest <= 0)
        {
            _deferred.SetLength(0);
            _deferred.Position = 0;
            return;
        }

        if (!_deferred.TryGetBuffer(out ArraySegment<byte> segment) || segment.Array == null)
            throw new InvalidOperationException("Deferred buffer is not accessible.");

        System.Buffer.BlockCopy(
            segment.Array,
            segment.Offset + consumed,
            segment.Array,
            segment.Offset,
            rest
        );

        _deferred.SetLength(rest);
        _deferred.Position = rest;
    }

    /// <summary>
    /// Добавляет оставшуюся часть Gst.Buffer в редкий deferred-хвост
    /// </summary>
    void AppendGstBufferToDeferred(
        Gst.Buffer buffer,
        int offset,
        int count
    )
    {
        while (count > 0)
        {
            int requested = Math.Min(
                _readBuffer.Length,
                count
            );

            nuint copiedValue = buffer.Extract(
                (nuint)offset,
                _readBuffer.AsSpan(0, requested)
            );

            int copied = (int)copiedValue;

            if (copied <= 0)
                return;

            _deferred.Write(
                _readBuffer.AsSpan(0, copied)
            );

            offset += copied;
            count -= copied;

        }
    }

    /// <summary>
    /// Переводит смещение из секунд в units трека и ищет tfdt внутри moof
    /// </summary>
    static bool ShiftTfdt(
        Span<byte> moof,
        uint timescale,
        double offsetSeconds
    )
    {
        if (moof.Length < 8 || timescale == 0 || offsetSeconds <= 0)
            return false;

        double offsetValue = offsetSeconds * timescale;

        if (!double.IsFinite(offsetValue) ||
            offsetValue <= 0 ||
            offsetValue > ulong.MaxValue)
        {
            return false;
        }

        ulong offset = (ulong)Math.Round(offsetValue);

        // Пропускаем заголовок moof и ищем вложенный traf
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
                return false;

            if (size < 8 || pos + size > end)
                return false;

            if (type == BoxTraf)
            {
                return ShiftTrafTfdt(
                    moof,
                    pos + 8,
                    pos + (int)size,
                    offset
                );
            }

            pos += (int)size;
        }

        return false;
    }

    /// <summary>
    /// Находит tfdt внутри traf и увеличивает baseMediaDecodeTime
    /// </summary>
    static bool ShiftTrafTfdt(
        Span<byte> data,
        int start,
        int end,
        ulong offset
    )
    {
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
                return false;

            if (size < 8 || pos + size > end)
                return false;

            if (type == BoxTfdt)
            {
                if (size < 16)
                    return false;

                byte version = data[pos + 8];

                // tfdt version 1 хранит 64-битное значение
                if (version == 1)
                {
                    if (size < 20)
                        return false;

                    ulong value = BinaryPrimitives.ReadUInt64BigEndian(
                        data.Slice(pos + 12, 8)
                    );

                    if (ulong.MaxValue - value < offset)
                        return false;

                    BinaryPrimitives.WriteUInt64BigEndian(
                        data.Slice(pos + 12, 8),
                        value + offset
                    );

                    return true;
                }

                // tfdt version 0 хранит 32-битное значение
                if (version == 0)
                {
                    uint value = BinaryPrimitives.ReadUInt32BigEndian(
                        data.Slice(pos + 12, 4)
                    );

                    ulong result = value + offset;

                    if (result > uint.MaxValue)
                        return false;

                    BinaryPrimitives.WriteUInt32BigEndian(
                        data.Slice(pos + 12, 4),
                        (uint)result
                    );

                    return true;
                }

                return false;
            }

            pos += (int)size;
        }

        return false;
    }

    /// <summary>
    /// Ищет traf внутри moof и возвращает track_ID вместе с его tfdt
    /// </summary>
    static uint GetMoofTrackId(
        ReadOnlySpan<byte> moof,
        out ulong? decodeTime
    )
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

    /// <summary>
    /// Читает track_ID из tfhd и decode time из tfdt одного traf
    /// </summary>
    static uint GetTrafTrackId(
        ReadOnlySpan<byte> data,
        int start,
        int end,
        out ulong? decodeTime
    )
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
                // tfhd: size + type + version/flags + track_ID
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
                else if (version == 0)
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

    /// <summary>
    /// Находит нужный trak по track_ID и возвращает его mdhd timescale
    /// </summary>
    static uint GetTrackTimescale(
        ReadOnlySpan<byte> init,
        uint requiredTrackId
    )
    {
        int pos = 0;

        // Верхний уровень init: ищем moov
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

            // Внутри moov перебираем trak
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

                // tkhd содержит ID, mdia/mdhd — timescale этого же трека
                while (TryReadBox(
                    child,
                    ref trakPos,
                    out uint trakType,
                    out ReadOnlySpan<byte> trakBox
                ))
                {
                    if (trakType == BoxTkhd)
                    {
                        // Положение track_ID зависит от версии full box
                        int offset =
                            trakBox.Length > 8 && trakBox[8] == 1
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

    /// <summary>
    /// Ищет mdhd внутри mdia и читает timescale трека
    /// </summary>
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

            // Положение timescale зависит от версии mdhd
            int offset =
                box.Length > 8 && box[8] == 1
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

    /// <summary>
    /// Читает один MP4 box из span и передвигает position только при успехе
    /// Используется только для уже готового init/moov
    /// </summary>
    static bool TryReadBox(
        ReadOnlySpan<byte> data,
        ref int position,
        out uint type,
        out ReadOnlySpan<byte> box
    )
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

        // size == 1 означает расширенный 64-битный размер
        if (size == 1)
        {
            if (start + 16 > data.Length)
                return false;

            size = BinaryPrimitives.ReadUInt64BigEndian(
                data.Slice(start + 8, 8)
            );

            headerSize = 16;
        }
        // size == 0 означает box до конца переданного span
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

    public void Dispose()
    {
        _audioPart?.Dispose();
        _audioPart = null;

        _videoPart?.Dispose();
        _videoPart = null;

        _deferred?.Dispose();
        _deferred = null;

        _moof?.Dispose();
        _moof = null;

        _init?.Dispose();
        _init = null;
    }
}
