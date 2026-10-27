namespace Coop.World.Streaming;

/// <summary>
/// The last few timed samples of something that moves, newest last, for playing it back a little in
/// the past. Allocation-free once built.
/// </summary>
internal sealed class TimedRing<T> where T : struct
{
    private readonly float[] _times;
    private readonly T[] _values;
    private int _count;
    private int _newest = -1;

    public TimedRing(int size)
    {
        _times = new float[size];
        _values = new T[size];
    }

    public bool IsEmpty => _count == 0;

    public float NewestTime => _count == 0 ? float.NegativeInfinity : _times[_newest];

    public ref T Newest => ref _values[_newest];

    public ref T At(int index) => ref _values[index];

    public void Clear()
    {
        _count = 0;
        _newest = -1;
    }

    /// <summary>Adds a sample; older or repeated ones are dropped.</summary>
    public bool Add(float time, in T value)
    {
        if (_count > 0 && time <= _times[_newest])
            return false;
        _newest = (_newest + 1) % _times.Length;
        _times[_newest] = time;
        _values[_newest] = value;
        if (_count < _times.Length)
            _count++;
        return true;
    }

    /// <summary>
    /// The two samples around <paramref name="time"/> and how far between them it is. Past the newest
    /// sample, or before the oldest, both are the same sample.
    /// </summary>
    public void Find(float time, out int older, out int newer, out float t)
    {
        int size = _times.Length;
        if (time >= _times[_newest])
        {
            older = newer = _newest;
            t = 0f;
            return;
        }
        for (int i = 1; i < _count; i++)
        {
            int o = (_newest - i + size) % size;
            int n = (o + 1) % size;
            if (time >= _times[o])
            {
                older = o;
                newer = n;
                float span = _times[n] - _times[o];
                t = span > 0f ? (time - _times[o]) / span : 1f;
                return;
            }
        }
        older = newer = (_newest - _count + 1 + size) % size;
        t = 0f;
    }
}
