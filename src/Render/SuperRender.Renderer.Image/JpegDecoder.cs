using System.Buffers.Binary;

namespace SuperRender.Renderer.Image;

/// <summary>
/// Pure C# baseline JPEG decoder (SOF0 only). Returns null for progressive (SOF2) or invalid data.
/// Supports 4:4:4 and 4:2:0 chroma subsampling, grayscale, and YCbCr color spaces.
/// </summary>
public static class JpegDecoder
{
    public static bool IsJpeg(ReadOnlySpan<byte> data)
        => data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8;

    /// <summary>
    /// Zigzag order lookup table: maps zigzag scan index to linear (row-major) position in an 8x8 block.
    /// </summary>
    internal static readonly int[] ZigZag =
    [
         0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    ];

    /// <summary>
    /// Pre-computed cosine table for IDCT: CosTable[x * 8 + u] = cos((2*x+1) * u * pi / 16).
    /// </summary>
    private static readonly double[] CosTable = PrecomputeCosTable();

    /// <summary>
    /// Normalization constants: C(0) = 1/sqrt(2), C(k) = 1 for k > 0.
    /// </summary>
    private static readonly double[] NormC = ComputeNormC();

    private static double[] PrecomputeCosTable()
    {
        var table = new double[64];
        for (int x = 0; x < 8; x++)
        {
            for (int u = 0; u < 8; u++)
            {
                table[x * 8 + u] = Math.Cos((2 * x + 1) * u * Math.PI / 16.0);
            }
        }
        return table;
    }

    private static double[] ComputeNormC()
    {
        var c = new double[8];
        c[0] = 1.0 / Math.Sqrt(2.0);
        for (int i = 1; i < 8; i++) c[i] = 1.0;
        return c;
    }

    /// <summary>
    /// Decodes a baseline JPEG image into RGBA pixel data. Returns null for invalid, truncated,
    /// or progressive JPEG data.
    /// </summary>
    public static ImageData? Decode(ReadOnlySpan<byte> data)
    {
        try
        {
            return DecodeInternal(data);
        }
        catch
        {
            return null;
        }
    }

    private static ImageData? DecodeInternal(ReadOnlySpan<byte> data)
    {
        if (!IsJpeg(data) || data.Length < 4) return null;

        // Quantization tables: up to 4, each with 64 values
        var quantTables = new int[4][];

        // Huffman tables: [class, id] where class 0 = DC, 1 = AC
        var huffTables = new HuffmanTable?[2, 4];

        // Frame info
        int width = 0, height = 0;
        int componentCount = 0;
        var components = new FrameComponent[4];

        // Scan header info
        var scanComponents = new ScanComponent[4];
        int scanComponentCount = 0;

        // Restart interval
        int restartInterval = 0;

        int pos = 2; // skip SOI
        bool foundSOS = false;
        int sosPos = 0;

        while (pos < data.Length - 1)
        {
            if (data[pos] != 0xFF) return null;

            // Skip padding FF bytes
            while (pos < data.Length && data[pos] == 0xFF) pos++;
            if (pos >= data.Length) return null;

            byte marker = data[pos];
            pos++;

            if (marker == 0xD9) break; // EOI
            if (marker == 0x00) continue; // byte-stuffed FF (should not appear outside scan)
            if (marker == 0xD8) continue; // SOI (should not appear again)
            if (marker >= 0xD0 && marker <= 0xD7) continue; // RST markers

            // All remaining markers have a length field
            if (pos + 2 > data.Length) return null;
            int length = BinaryPrimitives.ReadUInt16BigEndian(data[pos..]);
            if (length < 2 || pos + length > data.Length) return null;

            var segment = data.Slice(pos + 2, length - 2);

            switch (marker)
            {
                case 0xC0: // SOF0 - Baseline DCT
                    if (!ParseSOF0(segment, ref width, ref height, ref componentCount, components))
                        return null;
                    break;

                case 0xC2: // SOF2 - Progressive DCT
                    return null;

                case 0xC4: // DHT
                    if (!ParseDHT(segment, huffTables))
                        return null;
                    break;

                case 0xDB: // DQT
                    if (!ParseDQT(segment, quantTables))
                        return null;
                    break;

                case 0xDD: // DRI (Define Restart Interval)
                    if (segment.Length >= 2)
                        restartInterval = BinaryPrimitives.ReadUInt16BigEndian(segment);
                    break;

                case 0xDA: // SOS
                    if (!ParseSOS(segment, components, componentCount, ref scanComponentCount, scanComponents))
                        return null;
                    sosPos = pos + length;
                    foundSOS = true;
                    break;

                    // APP markers (E0-EF), COM (FE), and others: skip
            }

            if (foundSOS) break;
            pos += length;
        }

        if (!foundSOS || width == 0 || height == 0 || componentCount == 0) return null;

        // Determine max sampling factors
        int maxH = 1, maxV = 1;
        for (int i = 0; i < componentCount; i++)
        {
            if (components[i].H > maxH) maxH = components[i].H;
            if (components[i].V > maxV) maxV = components[i].V;
        }

        // MCU dimensions in pixels
        int mcuWidth = maxH * 8;
        int mcuHeight = maxV * 8;
        int mcuCols = (width + mcuWidth - 1) / mcuWidth;
        int mcuRows = (height + mcuHeight - 1) / mcuHeight;

        // Allocate block storage per component
        var blockData = new int[componentCount][][];
        for (int c = 0; c < componentCount; c++)
        {
            int blocksX = mcuCols * components[c].H;
            int blocksY = mcuRows * components[c].V;
            blockData[c] = new int[blocksX * blocksY][];
            for (int b = 0; b < blockData[c].Length; b++)
                blockData[c][b] = new int[64];
        }

        // Decode entropy-coded data
        var bitReader = new BitReader(data[sosPos..]);
        var dcPred = new int[componentCount];
        int mcuCount = 0;

        for (int mcuRow = 0; mcuRow < mcuRows; mcuRow++)
        {
            for (int mcuCol = 0; mcuCol < mcuCols; mcuCol++)
            {
                // Handle restart markers
                if (restartInterval > 0 && mcuCount > 0 && mcuCount % restartInterval == 0)
                {
                    bitReader.AlignToByte();
                    bitReader.SkipRestartMarker();
                    Array.Clear(dcPred);
                }

                for (int sc = 0; sc < scanComponentCount; sc++)
                {
                    int compIdx = scanComponents[sc].FrameIndex;
                    int hi = components[compIdx].H;
                    int vi = components[compIdx].V;
                    int qtId = components[compIdx].QuantTableId;
                    int dcTableId = scanComponents[sc].DcTableId;
                    int acTableId = scanComponents[sc].AcTableId;

                    var dcTable = huffTables[0, dcTableId];
                    var acTable = huffTables[1, acTableId];
                    if (dcTable == null || acTable == null) return null;
                    if (quantTables[qtId] == null) return null;

                    int blocksPerRow = mcuCols * hi;

                    for (int v = 0; v < vi; v++)
                    {
                        for (int h = 0; h < hi; h++)
                        {
                            int blockX = mcuCol * hi + h;
                            int blockY = mcuRow * vi + v;
                            int blockIdx = blockY * blocksPerRow + blockX;

                            if (blockIdx >= blockData[compIdx].Length) return null;

                            if (!DecodeBlock(ref bitReader, dcTable, acTable,
                                    quantTables[qtId]!, ref dcPred[compIdx],
                                    blockData[compIdx][blockIdx]))
                                return null;
                        }
                    }
                }

                mcuCount++;
            }
        }

        // Convert blocks to RGBA pixel data
        var pixels = new byte[width * height * 4];

        if (componentCount == 1)
        {
            AssembleGrayscale(blockData[0], width, height, mcuCols * components[0].H, pixels);
        }
        else if (componentCount >= 3)
        {
            AssembleYCbCr(blockData, components, maxH, maxV, mcuCols, mcuRows, width, height, pixels);
        }
        else
        {
            return null;
        }

        return new ImageData { Width = width, Height = height, Pixels = pixels };
    }

    #region Marker Parsing

    private static bool ParseSOF0(ReadOnlySpan<byte> data, ref int width, ref int height,
        ref int componentCount, FrameComponent[] components)
    {
        if (data.Length < 6) return false;

        int precision = data[0];
        if (precision != 8) return false;

        height = BinaryPrimitives.ReadUInt16BigEndian(data[1..]);
        width = BinaryPrimitives.ReadUInt16BigEndian(data[3..]);
        componentCount = data[5];

        if (componentCount < 1 || componentCount > 4) return false;
        if (data.Length < 6 + componentCount * 3) return false;

        for (int i = 0; i < componentCount; i++)
        {
            int offset = 6 + i * 3;
            components[i] = new FrameComponent
            {
                Id = data[offset],
                H = (data[offset + 1] >> 4) & 0x0F,
                V = data[offset + 1] & 0x0F,
                QuantTableId = data[offset + 2]
            };
            if (components[i].H < 1 || components[i].H > 4) return false;
            if (components[i].V < 1 || components[i].V > 4) return false;
            if (components[i].QuantTableId > 3) return false;
        }

        return true;
    }

    private static bool ParseDQT(ReadOnlySpan<byte> data, int[][] quantTables)
    {
        int pos = 0;
        while (pos < data.Length)
        {
            int info = data[pos++];
            int precision = (info >> 4) & 0x0F;
            int tableId = info & 0x0F;
            if (tableId > 3) return false;

            int valueSize = precision == 0 ? 1 : 2;
            if (pos + 64 * valueSize > data.Length) return false;

            var table = new int[64];
            for (int i = 0; i < 64; i++)
            {
                if (precision == 0)
                {
                    table[i] = data[pos++];
                }
                else
                {
                    table[i] = BinaryPrimitives.ReadUInt16BigEndian(data[pos..]);
                    pos += 2;
                }
            }

            quantTables[tableId] = table;
        }
        return true;
    }

    private static bool ParseDHT(ReadOnlySpan<byte> data, HuffmanTable?[,] huffTables)
    {
        int pos = 0;
        while (pos < data.Length)
        {
            int info = data[pos++];
            int tableClass = (info >> 4) & 0x0F; // 0 = DC, 1 = AC
            int tableId = info & 0x0F;
            if (tableClass > 1 || tableId > 3) return false;

            if (pos + 16 > data.Length) return false;
            var counts = new int[16];
            int totalSymbols = 0;
            for (int i = 0; i < 16; i++)
            {
                counts[i] = data[pos++];
                totalSymbols += counts[i];
            }

            if (pos + totalSymbols > data.Length) return false;
            var symbols = new byte[totalSymbols];
            for (int i = 0; i < totalSymbols; i++)
            {
                symbols[i] = data[pos++];
            }

            huffTables[tableClass, tableId] = BuildHuffmanTable(counts, symbols);
        }
        return true;
    }

    private static bool ParseSOS(ReadOnlySpan<byte> data, FrameComponent[] frameComponents,
        int frameComponentCount, ref int scanComponentCount, ScanComponent[] scanComponents)
    {
        if (data.Length < 1) return false;
        scanComponentCount = data[0];
        if (scanComponentCount < 1 || scanComponentCount > 4) return false;
        if (data.Length < 1 + scanComponentCount * 2 + 3) return false;

        for (int i = 0; i < scanComponentCount; i++)
        {
            int offset = 1 + i * 2;
            int selectorId = data[offset];

            // Map selector ID to frame component index
            int frameIndex = -1;
            for (int j = 0; j < frameComponentCount; j++)
            {
                if (frameComponents[j].Id == selectorId)
                {
                    frameIndex = j;
                    break;
                }
            }
            if (frameIndex < 0) return false;

            scanComponents[i] = new ScanComponent
            {
                FrameIndex = frameIndex,
                DcTableId = (data[offset + 1] >> 4) & 0x0F,
                AcTableId = data[offset + 1] & 0x0F
            };
        }

        return true;
    }

    #endregion

    #region Huffman Table Construction

    private static HuffmanTable BuildHuffmanTable(int[] counts, byte[] symbols)
    {
        var minCode = new int[16];
        var maxCode = new int[16];
        var valPtr = new int[16];

        int code = 0;
        int symbolIndex = 0;

        for (int bits = 0; bits < 16; bits++)
        {
            if (counts[bits] == 0)
            {
                minCode[bits] = -1;
                maxCode[bits] = -1;
                valPtr[bits] = 0;
            }
            else
            {
                valPtr[bits] = symbolIndex;
                minCode[bits] = code;
                code += counts[bits];
                maxCode[bits] = code - 1;
                symbolIndex += counts[bits];
            }
            code <<= 1;
        }

        return new HuffmanTable
        {
            MinCode = minCode,
            MaxCode = maxCode,
            ValPtr = valPtr,
            Symbols = symbols
        };
    }

    #endregion

    #region Entropy Decoding

    private static bool DecodeBlock(ref BitReader reader, HuffmanTable dcTable,
        HuffmanTable acTable, int[] quantTable, ref int dcPred, int[] block)
    {
        Array.Clear(block);

        // DC coefficient
        int dcCategory = DecodeHuffman(ref reader, dcTable);
        if (dcCategory < 0) return false;

        int dcDiff = 0;
        if (dcCategory > 0)
        {
            dcDiff = ReadBits(ref reader, dcCategory);
            if (dcDiff < 0) return false;
            dcDiff = Extend(dcDiff, dcCategory);
        }
        dcPred += dcDiff;
        block[ZigZag[0]] = dcPred * quantTable[0];

        // AC coefficients
        int k = 1;
        while (k < 64)
        {
            int rs = DecodeHuffman(ref reader, acTable);
            if (rs < 0) return false;

            int run = (rs >> 4) & 0x0F;
            int size = rs & 0x0F;

            if (size == 0)
            {
                if (run == 0) break; // EOB
                if (run == 15)
                {
                    k += 16; // ZRL: skip 16 zeros
                    continue;
                }
                break; // Treat any other zero-size as EOB
            }

            k += run;
            if (k >= 64) break;

            int value = ReadBits(ref reader, size);
            if (value < 0) return false;
            value = Extend(value, size);

            block[ZigZag[k]] = value * quantTable[k];
            k++;
        }

        // Apply IDCT in-place
        IDCT8x8(block);

        return true;
    }

    private static int DecodeHuffman(ref BitReader reader, HuffmanTable table)
    {
        int code = 0;
        for (int bits = 1; bits <= 16; bits++)
        {
            int bit = reader.ReadBit();
            if (bit < 0) return -1;
            code = (code << 1) | bit;

            int idx = bits - 1;
            if (table.MinCode[idx] != -1 && code <= table.MaxCode[idx])
            {
                int symbolIdx = table.ValPtr[idx] + (code - table.MinCode[idx]);
                if (symbolIdx >= 0 && symbolIdx < table.Symbols.Length)
                    return table.Symbols[symbolIdx];
            }
        }
        return -1;
    }

    private static int ReadBits(ref BitReader reader, int count)
    {
        if (count == 0) return 0;
        int value = 0;
        for (int i = 0; i < count; i++)
        {
            int bit = reader.ReadBit();
            if (bit < 0) return -1;
            value = (value << 1) | bit;
        }
        return value;
    }

    /// <summary>
    /// Extends a variable-length integer to its signed value.
    /// If the value is less than 2^(bits-1), it is negative.
    /// </summary>
    private static int Extend(int value, int bits)
    {
        int vt = 1 << (bits - 1);
        if (value < vt)
            return value - (2 * vt - 1);
        return value;
    }

    #endregion

    #region IDCT

    /// <summary>
    /// Inverse Discrete Cosine Transform on an 8x8 block using the direct formula.
    /// f(x,y) = (1/4) * sum_u sum_v C(u)*C(v)*F(u,v)*cos((2x+1)*u*pi/16)*cos((2y+1)*v*pi/16)
    /// Input: dequantized DCT coefficients in row-major order. Output: pixel values [0, 255].
    /// </summary>
    internal static void IDCT8x8(int[] block)
    {
        var result = new double[64];

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0;
                for (int u = 0; u < 8; u++)
                {
                    double cu = NormC[u];
                    double cosXu = CosTable[x * 8 + u];
                    for (int v = 0; v < 8; v++)
                    {
                        sum += cu * NormC[v] * block[u * 8 + v]
                               * cosXu * CosTable[y * 8 + v];
                    }
                }
                result[y * 8 + x] = sum / 4.0;
            }
        }

        // Level shift (+128) and clamp to [0, 255]
        for (int i = 0; i < 64; i++)
        {
            block[i] = Math.Clamp((int)Math.Round(result[i]) + 128, 0, 255);
        }
    }

    #endregion

    #region Color Conversion

    /// <summary>
    /// Converts YCbCr color space to RGB, clamped to [0, 255].
    /// </summary>
    internal static (byte R, byte G, byte B) YCbCrToRgb(int y, int cb, int cr)
    {
        int r = (int)Math.Round(y + 1.402 * (cr - 128));
        int g = (int)Math.Round(y - 0.344136 * (cb - 128) - 0.714136 * (cr - 128));
        int b = (int)Math.Round(y + 1.772 * (cb - 128));

        return (
            (byte)Math.Clamp(r, 0, 255),
            (byte)Math.Clamp(g, 0, 255),
            (byte)Math.Clamp(b, 0, 255)
        );
    }

    #endregion

    #region Block Assembly

    private static void AssembleGrayscale(int[][] blocks, int width, int height,
        int blocksPerRow, byte[] pixels)
    {
        for (int blockY = 0; blockY < (height + 7) / 8; blockY++)
        {
            for (int blockX = 0; blockX < blocksPerRow; blockX++)
            {
                int blockIdx = blockY * blocksPerRow + blockX;
                if (blockIdx >= blocks.Length) continue;
                var block = blocks[blockIdx];

                for (int py = 0; py < 8; py++)
                {
                    int imgY = blockY * 8 + py;
                    if (imgY >= height) break;

                    for (int px = 0; px < 8; px++)
                    {
                        int imgX = blockX * 8 + px;
                        if (imgX >= width) break;

                        int value = block[py * 8 + px];
                        int offset = (imgY * width + imgX) * 4;
                        pixels[offset] = (byte)value;
                        pixels[offset + 1] = (byte)value;
                        pixels[offset + 2] = (byte)value;
                        pixels[offset + 3] = 255;
                    }
                }
            }
        }
    }

    private static void AssembleYCbCr(int[][][] blockData, FrameComponent[] components,
        int maxH, int maxV, int mcuCols, int mcuRows, int width, int height, byte[] pixels)
    {
        for (int mcuRow = 0; mcuRow < mcuRows; mcuRow++)
        {
            for (int mcuCol = 0; mcuCol < mcuCols; mcuCol++)
            {
                int mcuPixelX = mcuCol * maxH * 8;
                int mcuPixelY = mcuRow * maxV * 8;

                for (int py = 0; py < maxV * 8; py++)
                {
                    int imgY = mcuPixelY + py;
                    if (imgY >= height) break;

                    for (int px = 0; px < maxH * 8; px++)
                    {
                        int imgX = mcuPixelX + px;
                        if (imgX >= width) break;

                        int yVal = SampleComponent(blockData[0], components[0], mcuCol, mcuRow,
                            mcuCols, maxH, maxV, px, py);
                        int cbVal = SampleComponent(blockData[1], components[1], mcuCol, mcuRow,
                            mcuCols, maxH, maxV, px, py);
                        int crVal = SampleComponent(blockData[2], components[2], mcuCol, mcuRow,
                            mcuCols, maxH, maxV, px, py);

                        var (r, g, b) = YCbCrToRgb(yVal, cbVal, crVal);
                        int offset = (imgY * width + imgX) * 4;
                        pixels[offset] = r;
                        pixels[offset + 1] = g;
                        pixels[offset + 2] = b;
                        pixels[offset + 3] = 255;
                    }
                }
            }
        }
    }

    private static int SampleComponent(int[][] blocks, FrameComponent component,
        int mcuCol, int mcuRow, int mcuCols, int maxH, int maxV, int px, int py)
    {
        int hi = component.H;
        int vi = component.V;
        int blocksPerRow = mcuCols * hi;

        // Map MCU-relative pixel to component-relative pixel (accounting for subsampling)
        int compPixelX = px * hi / maxH;
        int compPixelY = py * vi / maxV;

        int blockX = mcuCol * hi + compPixelX / 8;
        int blockY = mcuRow * vi + compPixelY / 8;
        int blockIdx = blockY * blocksPerRow + blockX;

        int inBlockX = compPixelX % 8;
        int inBlockY = compPixelY % 8;

        if (blockIdx >= blocks.Length) return 128;
        return blocks[blockIdx][inBlockY * 8 + inBlockX];
    }

    #endregion

    #region Internal Types

    private struct FrameComponent
    {
        public int Id;
        public int H;
        public int V;
        public int QuantTableId;
    }

    private struct ScanComponent
    {
        public int FrameIndex;
        public int DcTableId;
        public int AcTableId;
    }

    private sealed class HuffmanTable
    {
        public int[] MinCode = [];
        public int[] MaxCode = [];
        public int[] ValPtr = [];
        public byte[] Symbols = [];
    }

    /// <summary>
    /// Bitstream reader for JPEG entropy-coded data with FF00 byte-stuffing removal.
    /// Reads bits MSB-first.
    /// </summary>
    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _bytePos;
        private int _bitsLeft;
        private int _currentByte;

        public BitReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _bytePos = 0;
            _bitsLeft = 0;
            _currentByte = 0;
        }

        public int ReadBit()
        {
            if (_bitsLeft == 0)
            {
                if (_bytePos >= _data.Length) return -1;

                _currentByte = _data[_bytePos++];

                // Handle byte stuffing: FF 00 -> FF
                if (_currentByte == 0xFF)
                {
                    if (_bytePos >= _data.Length) return -1;
                    byte next = _data[_bytePos];
                    if (next == 0x00)
                    {
                        _bytePos++; // consume the stuffed 00 byte
                    }
                    else
                    {
                        // Found a marker within entropy data - stop reading
                        return -1;
                    }
                }

                _bitsLeft = 8;
            }

            _bitsLeft--;
            return (_currentByte >> _bitsLeft) & 1;
        }

        /// <summary>
        /// Discards remaining bits in the current byte to align to a byte boundary.
        /// </summary>
        public void AlignToByte()
        {
            _bitsLeft = 0;
        }

        /// <summary>
        /// Skips a restart marker (FFD0-FFD7) in the bitstream.
        /// </summary>
        public void SkipRestartMarker()
        {
            while (_bytePos < _data.Length - 1)
            {
                if (_data[_bytePos] == 0xFF)
                {
                    byte m = _data[_bytePos + 1];
                    if (m >= 0xD0 && m <= 0xD7)
                    {
                        _bytePos += 2;
                        return;
                    }
                    if (m == 0x00)
                    {
                        break; // stuffed byte, not a restart marker
                    }
                    return; // some other marker
                }
                break;
            }
        }
    }

    #endregion
}
