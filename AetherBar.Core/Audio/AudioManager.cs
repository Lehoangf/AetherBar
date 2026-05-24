using NAudio.Wave;
using NAudio.Dsp;
using AetherBar.Core.Models;

namespace AetherBar.Core.Audio;

public class AudioManager : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private readonly int _fftSize = 1024;
    private readonly float[] _fftBuffer;
    private int _fftPos;
    private bool _disposed;

    private readonly Complex[] _fftComplex;
    private readonly float[] _smoothedFft;

    public event EventHandler<AudioData>? AudioDataAvailable;

    public bool IsCapturing { get; private set; }

    public AudioManager()
    {
        _fftBuffer = new float[_fftSize];
        _fftComplex = new Complex[_fftSize];
        _smoothedFft = new float[_fftSize / 2];
    }

    public bool StartCapture()
    {
        try
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
            IsCapturing = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void StopCapture()
    {
        if (_capture != null && IsCapturing)
        {
            _capture.StopRecording();
            IsCapturing = false;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var buffer = e.Buffer;
        var bytesPerSample = _capture?.WaveFormat.BitsPerSample / 8 ?? 2;

        for (int i = 0; i < e.BytesRecorded && _fftPos < _fftSize; i += bytesPerSample)
        {
            float sample = BitConverter.ToInt16(buffer, i) / 32768f;
            _fftBuffer[_fftPos++] = sample;
        }

        if (_fftPos >= _fftSize)
        {
            ProcessFft();
            _fftPos = 0;
        }
    }

    private void ProcessFft()
    {
        for (int i = 0; i < _fftSize; i++)
        {
            _fftComplex[i].X = _fftBuffer[i] * (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (_fftSize - 1))));
            _fftComplex[i].Y = 0;
        }

        FastFourierTransform.FFT(true, (int)Math.Log(_fftSize, 2), _fftComplex);

        int halfFft = _fftSize / 2;
            int barCount = Math.Min(256, halfFft);
        float[] bars = new float[barCount];
        float peak = 0;

        // Logarithmic frequency binning from 40Hz to 16kHz
        // Pre-compute bin boundaries so each bar gets at least 2 FFT bins
        float minFreq = 40f;
        float maxFreq = 16000f;
        float nyquist = (_capture?.WaveFormat.SampleRate ?? 44100) / 2f;
        float logMin = (float)Math.Log(minFreq);
        float logMax = (float)Math.Log(maxFreq);
        float logRange = logMax - logMin;

        int[] binBoundaries = new int[barCount + 1];
        binBoundaries[0] = 1;
        for (int i = 1; i < barCount; i++)
        {
            float freq = (float)Math.Exp(logMin + (float)i / barCount * logRange);
            int bin = (int)(freq / nyquist * halfFft);
            if (bin < 2) bin = 2;
            if (bin > halfFft) bin = halfFft;
            binBoundaries[i] = bin;
        }
        binBoundaries[barCount] = halfFft;

        // Ensure minimum 2 bins per bar
        for (int i = barCount - 2; i >= 0; i--)
        {
            if (binBoundaries[i + 1] - binBoundaries[i] < 2)
                binBoundaries[i] = binBoundaries[i + 1] - 2;
            if (binBoundaries[i] < 1) binBoundaries[i] = 1;
        }

        for (int i = 0; i < barCount; i++)
        {
            int binLow = binBoundaries[i];
            int binHigh = binBoundaries[i + 1];
            if (binLow >= binHigh) continue;

            float maxMag = 0;
            for (int b = binLow; b < binHigh; b++)
            {
                float mag = (float)Math.Sqrt(_fftComplex[b].X * _fftComplex[b].X + _fftComplex[b].Y * _fftComplex[b].Y);
                if (mag > maxMag) maxMag = mag;
            }

            // Convert to dB and normalize to 0-1
            float db = 20 * (float)Math.Log10(maxMag + 1e-10f);
            float normalized = (db + 50) / 50f;
            if (normalized < 0) normalized = 0;
            if (normalized > 1) normalized = 1;
            bars[i] = Math.Max(0.02f, normalized);

            if (normalized > peak) peak = normalized;
        }

        peak = Math.Min(1, peak);

        float smoothFactor = 0.35f;
        for (int i = 0; i < barCount; i++)
        {
            _smoothedFft[i] += (bars[i] - _smoothedFft[i]) * smoothFactor;
        }

        var data = new AudioData
        {
            FftBuffer = _smoothedFft.Take(barCount).ToArray(),
            WaveformBuffer = _fftBuffer.ToArray(),
            PeakLevel = peak,
            RmsLevel = peak * 0.7f,
            SampleRate = _capture?.WaveFormat.SampleRate ?? 44100,
            ChannelCount = _capture?.WaveFormat.Channels ?? 2
        };

        AudioDataAvailable?.Invoke(this, data);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsCapturing = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopCapture();
        _capture?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
