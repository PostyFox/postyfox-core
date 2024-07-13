using Optional;

namespace Twitch.Net.EventSub.Models;

public class SubscribeResult
{
    /// <summary>
    /// The output when "subscribing" to an EventSub (result from twitch)
    /// </summary>
    public Option<SubscribeResponseModel> Output { get; init; } = Option.None<SubscribeResponseModel>();
        
    /// <summary>
    /// If this is true, it means the event is already registered and status "enabled" on twitchs end.
    /// </summary>
    public bool AlreadyRegistered { get; init; } = false;
}