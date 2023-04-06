using System;
using System.Collections.Generic;

namespace GameJoltLibrary.Helpers;

public class SelectorComparer<T, U> : IEqualityComparer<T>
{
    private readonly Func<T, U> _compareSelector;

    public SelectorComparer(Func<T, U> compareSelector)
    {
        _compareSelector = compareSelector;
    }

    public bool Equals(T x, T y)
    {
        var xValue = _compareSelector(x);
        var yValue = _compareSelector(y);

        return xValue is null && yValue is null || xValue.Equals(yValue);
    }

    public int GetHashCode(T obj)
    {
        var value = _compareSelector(obj);
        return value.GetHashCode();
    }
}

public static class SelectorComparer
{
    public static SelectorComparer<T, U> Create<T, U>(Func<T, U> compareSelector) => new SelectorComparer<T, U>(compareSelector);
}
