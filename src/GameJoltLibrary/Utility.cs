using System;
using System.Collections.Generic;
using System.Linq;

namespace GameJoltLibrary;

public static class Utility
{
    private static bool? _is64BitsOs;

    public static bool Is64BitOs => _is64BitsOs ??= Environment.Is64BitOperatingSystem;

    public static string EmptyIfNull(this string value) => value is null ? string.Empty : value;
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> value) => value is null ? Enumerable.Empty<T>() : value;
}
