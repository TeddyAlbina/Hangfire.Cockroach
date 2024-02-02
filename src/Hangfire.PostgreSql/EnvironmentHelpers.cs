using System;

namespace Hangfire.Cockroach
{
  internal class EnvironmentHelpers
  {
    private static bool? _isMono;

    public static bool IsMono()
    {
      return _isMono ??= Type.GetType("Mono.Runtime") != null;
    }
  }
}
