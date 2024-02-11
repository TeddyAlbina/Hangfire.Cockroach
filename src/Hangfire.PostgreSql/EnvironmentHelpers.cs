using System;

namespace Hangfire.Cockroach;

internal sealed class EnvironmentHelpers
{
    private static bool? _isMono;

    public static bool IsMono() => _isMono ??= Type.GetType("Mono.Runtime") != null;
}
