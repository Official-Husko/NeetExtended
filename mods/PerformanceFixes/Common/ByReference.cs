using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PerformanceFixes.Common;

/// <summary>
/// Compares by reference. Sets of Unity objects need it: UnityEngine.Object's equality treats every
/// destroyed object as equal to null and to each other.
/// </summary>
internal sealed class ByReference<T> : IEqualityComparer<T> where T : class
{
    public static readonly ByReference<T> Instance = new ByReference<T>();

    public bool Equals(T x, T y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
