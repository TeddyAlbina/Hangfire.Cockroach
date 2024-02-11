using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Hangfire.Cockroach.Utils;

/// <summary>
/// Represents a registry for managing AutoResetEvent instances using event keys.
/// </summary>
public sealed class AutoResetEventRegistry
{
    private readonly ConcurrentDictionary<string, AutoResetEvent> events = new();

    /// <summary>
    /// Retrieves the wait handles associated with the specified event keys.
    /// </summary>
    /// <param name="eventKeys">The event keys.</param>
    /// <returns>An enumerable of wait handles.</returns>
    public IEnumerable<WaitHandle> GetWaitHandles(IEnumerable<string> eventKeys)
    {
        foreach (var eventKey in eventKeys)
        {
            var newHandle = this.events.GetOrAdd(eventKey, _ => new AutoResetEvent(false));
            yield return newHandle;
        }
    }

    /// <summary>
    /// Sets the specified event.
    /// </summary>
    /// <param name="eventKey">The event key.</param>
    public void Set(string eventKey)
    {
        if (this.events.TryGetValue(eventKey, out var handle))
        {
            handle.Set();
        }
    }
}
