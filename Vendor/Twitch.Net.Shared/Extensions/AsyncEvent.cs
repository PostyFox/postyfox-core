using System.Collections.Immutable;

namespace Twitch.Net.Shared.Extensions;

public class AsyncEvent<T> where T : class
{
    private readonly object _subLock = new();
    private ImmutableArray<T> _subscriptions;

    public IReadOnlyList<T> Subscriptions => _subscriptions;

    public AsyncEvent()
    {
        _subscriptions = ImmutableArray.Create<T>();
    }

    public void Add(T subscriber)
    {
        lock (_subLock)
            _subscriptions = _subscriptions.Add(subscriber);
    }
        
    public void Remove(T subscriber)
    {
        lock (_subLock)
            _subscriptions = _subscriptions.Remove(subscriber);
    }
}

public static class AsyncEventExtensions
{ 
    public static void Invoke(this AsyncEvent<Func<Task>> eventHandler)
    {
        var subscribers = eventHandler.Subscriptions;
        subscribers.ForEach(s => s.Invoke().ConfigureAwait(false));
    }
        
    public static void Invoke<T>(this AsyncEvent<Func<T, Task>> eventHandler, T arg)
    {
        var subscribers = eventHandler.Subscriptions;
        subscribers.ForEach(s => s.Invoke(arg).ConfigureAwait(false));
    }
}