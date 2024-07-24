using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Client.Extensions;

public static class TimeoutUserExtension
{
    private const int MaxTimeOutTime = 1_209_600;
        
    public static void TimeoutUser(
        this IIrcClient client,
        ChatChannel channel,
        string user,
        TimeSpan duration,
        string reason
        )
        => client.SendMessage(channel, TimeoutCommand(user, duration, reason));

    public static void TimeoutUser(
        this IIrcClient client,
        string channel,
        string user,
        TimeSpan duration,
        string reason
        )
        => client.SendMessage(channel, TimeoutCommand(user, duration, reason));

    private static string TimeoutCommand(string user, TimeSpan duration, string reason)
    {
        if (duration.TotalSeconds > MaxTimeOutTime)
            duration = new TimeSpan(0, 0, MaxTimeOutTime);
        else if (duration.TotalSeconds < 1)
            duration = new TimeSpan(0, 0, 1);
        return $"/timeout {user} {duration.TotalSeconds} {reason}";
    }
}