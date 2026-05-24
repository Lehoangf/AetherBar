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
        if (_capture == null) return;

        var buffer = e.Buffer;
        int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
        if (bytesPerSample <= 0) bytesPerSample = 2; // fallback

        int channels = _capture.WaveFormat.Channels;
        if (channels <= 0) channels = 2; // fallback

        int frameSize = channels * bytesPerSample;
        bool isFloat = _capture.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat || _capture.WaveFormat.BitsPerSample == 32;

        for (int i = 0; i + frameSize <= e.BytesRecorded && _fftPos < _fftSize; i += frameSize)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++)
            {
                int sampleOffset = i + c * bytesPerSample;
                float sample = 0;

                if (isFloat)
                {
                    if (sampleOffset + 4 <= e.BytesRecorded)
                    {
                        sample = BitConverter.ToSingle(buffer, sampleOffset);
                    }
                }
                else if (bytesPerSample == 2)
                {
                    if (sampleOffset + 2 <= e.BytesRecorded)
                    {
                        sample = BitConverter.ToInt16(buffer, sampleOffset) / 32768f;
                    }
                }
                else if (bytesPerSample == 3)
                {
                    if (sampleOffset + 3 <= e.BytesRecorded)
                    {
                        int sampleVal = (buffer[sampleOffset + 2] << 16) | (buffer[sampleOffset + 1] << 8) | buffer[sampleOffset];
                        if ((sampleVal & 0x800000) != 0) sampleVal |= unchecked((int)0xff000000); // sign extend
                        sample = sampleVal / 8388608f;
                    }
                }
                else if (bytesPerSample == 4)
                {
                    if (sampleOffset + 4 <= e.BytesRecorded)
                    {
                        sample = BitConverter.ToInt32(buffer, sampleOffset) / 2147483648f;
                    }
                }

                sum += sample;
            }

            _fftBuffer[_fftPos++] = sum / channels;
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

            // Apply frequency-dependent equalization (EQ) boost
            // Bass and Treble naturally have lower visual energy compared to Mids in FFT,
            // so we balance them dynamically.
            float freq = (float)Math.Exp(logMin + (float)i / barCount * logRange);
            float boost = 1.0f;
            if (freq < 250f) // Bass: progressive boost below 250Hz
            {
                boost = 1.0f + (250f - freq) / 250f * 0.8f; // up to 1.8x boost for cymbals/kick cheng
            }
            else if (freq > 1500f) // High-mids/Treble: progressive quadratic boost above 1.5kHz
            {
                float t = Math.Min(1.0f, (freq - 1500f) / 14500f);
                boost = 1.0f + t * 4.0f; // up to 5.0x boost for high-end crispness
            }

            float finalMag = maxMag * boost;

            // Convert to dB and normalize to 0-1 with a -60dB floor (more sensitive to cymbals and cheng-cheng transients)
            float db = 20 * (float)Math.Log10(finalMag + 1e-10f);
            float normalized = (db + 60f) / 60f;
            if (normalized < 0) normalized = 0;
            if (normalized > 1) normalized = 1;
            
            // Apply a high-contrast power curve to make peak frequencies sharp, punchy, and distinct
            float contrasted = (float)Math.Pow(normalized, 1.3f);
            bars[i] = Math.Max(0.02f, contrasted);

            if (contrasted > peak) peak = contrasted;
        }

        peak = Math.Min(1, peak);

        for (int i = 0; i < barCount; i++)
        {
            float freq = (float)Math.Exp(logMin + (float)i / barCount * logRange);
            float smoothFactor = 0.35f;
            if (freq < 250f)
            {
                // Slower smoothing for bass to keep visual bars solid and punchy without cringey flickering
                smoothFactor = 0.28f;
            }
            else if (freq > 2000f)
            {
                // Faster smoothing for treble to let high cymbals/vocals react snap-instantly
                float t = Math.Min(1.0f, (freq - 2000f) / 14000f);
                smoothFactor = 0.35f + t * 0.15f; // Up to 0.50f
            }

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
