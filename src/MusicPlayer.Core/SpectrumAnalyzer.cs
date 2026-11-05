using System.Numerics;

namespace MusicPlayer.Core;

/// <summary>Thread-safe PCM ring buffer and a radix-2 FFT. No generated/random levels.</summary>
public sealed class SpectrumAnalyzer
{
    private const int Size = 2048;
    private readonly float[] samples = new float[Size];
    private readonly object gate = new();
    private int position;
    private int count;
    public const int BandCount = 32;

    public void Push(float[] buffer, int offset, int length, int channels)
    {
        if (channels < 1 || offset < 0 || length < 0 || offset + length > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        lock (gate)
        {
            for (int i = offset; i + channels <= offset + length; i += channels)
            {
                // Maximum absolute channel value avoids cancelling antiphase stereo.
                float mono = 0;
                for (int c = 0; c < channels; c++)
                    if (Math.Abs(buffer[i + c]) > Math.Abs(mono)) mono = buffer[i + c];
                samples[position] = float.IsFinite(mono) ? mono : 0;
                position = (position + 1) % Size;
                count = Math.Min(Size, count + 1);
            }
        }
    }

    public void Clear()
    {
        lock (gate) { Array.Clear(samples); position = count = 0; }
    }

    public double[] Snapshot()
    {
        var fft = new Complex[Size];
        lock (gate)
        {
            if (count == 0) return new double[BandCount];
            for (int i = 0; i < Size; i++)
            {
                int index = (position + i) % Size;
                double window = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Size - 1));
                fft[i] = new Complex(samples[index] * window, 0);
            }
        }
        for (int i = 1, j = 0; i < Size; i++)
        {
            int bit = Size >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (fft[i], fft[j]) = (fft[j], fft[i]);
        }
        for (int length = 2; length <= Size; length <<= 1)
        {
            var rotation = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
            for (int start = 0; start < Size; start += length)
            {
                var factor = Complex.One;
                for (int k = 0; k < length / 2; k++)
                {
                    var a = fft[start + k]; var b = fft[start + k + length / 2] * factor;
                    fft[start + k] = a + b; fft[start + k + length / 2] = a - b;
                    factor *= rotation;
                }
            }
        }
        var bands = new double[BandCount];
        for (int band = 0; band < BandCount; band++)
        {
            int first = Math.Max(1, (int)Math.Pow(Size / 2.0, band / (double)BandCount));
            int last = Math.Max(first + 1, (int)Math.Pow(Size / 2.0, (band + 1.0) / BandCount));
            double peak = 0;
            for (int bin = first; bin < Math.Min(last, Size / 2); bin++)
                peak = Math.Max(peak, fft[bin].Magnitude * 4 / Size);
            bands[band] = Math.Clamp((20 * Math.Log10(Math.Max(peak, 1e-6)) + 60) / 60, 0, 1);
        }
        return bands;
    }
}
