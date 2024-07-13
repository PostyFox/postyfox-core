namespace Twitch.Net.Shared.Extensions;

public static class EnumerableExtensions
{
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
    {
        foreach(var item in enumeration)
            action(item);
    }
        
    public static IEnumerable<List<T>> SplitList<T>(this List<T> locations, int nSize)  
    {        
        for (var i = 0; i < locations.Count; i += nSize) 
            yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i)); 
    } 
        
    public static string Join(this IEnumerable<string> source, char separator) 
        => string.Join(separator, source);
        
    public static async Task ForEachAsync<T>(this IEnumerable<T> list, Func<T, Task> func)
    {
        foreach (var value in list)
            await func(value);
    }
}