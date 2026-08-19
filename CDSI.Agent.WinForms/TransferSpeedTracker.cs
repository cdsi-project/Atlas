using System.Diagnostics;

namespace CDSI.Agent.WinForms;

internal sealed class TransferSpeedTracker
{
    private readonly long _timestampFrequency;
    private bool _hasSample;
    private long _lastBytes;
    private long _lastTimestamp;

    public TransferSpeedTracker()
        : this(Stopwatch.Frequency)
    {
    }

    internal TransferSpeedTracker(long timestampFrequency)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestampFrequency);
        _timestampFrequency = timestampFrequency;
    }

    public double BytesPerSecond { get; private set; }

    public void Reset()
    {
        _hasSample = false;
        _lastBytes = 0;
        _lastTimestamp = 0;
        BytesPerSecond = 0;
    }

    public double Update(long transferredBytes)
    {
        return Update(transferredBytes, Stopwatch.GetTimestamp());
    }

    internal double Update(long transferredBytes, long timestamp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(transferredBytes);
        if (!_hasSample || transferredBytes < _lastBytes)
        {
            SetSample(transferredBytes, timestamp);
            BytesPerSecond = 0;
            return BytesPerSecond;
        }

        if (transferredBytes == 0)
        {
            SetSample(0, timestamp);
            BytesPerSecond = 0;
            return BytesPerSecond;
        }

        var elapsedTicks = timestamp - _lastTimestamp;
        if (elapsedTicks <= 0 || elapsedTicks < _timestampFrequency / 4)
        {
            return BytesPerSecond;
        }

        var transferredDelta = transferredBytes - _lastBytes;
        if (transferredDelta <= 0)
        {
            BytesPerSecond = 0;
            SetSample(transferredBytes, timestamp);
            return BytesPerSecond;
        }

        BytesPerSecond = transferredDelta * (double)_timestampFrequency /
            elapsedTicks;
        SetSample(transferredBytes, timestamp);
        return BytesPerSecond;
    }

    private void SetSample(long transferredBytes, long timestamp)
    {
        _hasSample = true;
        _lastBytes = transferredBytes;
        _lastTimestamp = timestamp;
    }
}
