using RateLimiter;
using Twitch.Net.Client.Irc;
using Twitch.Net.Client.Models;
using Twitch.Net.Shared.RateLimits;

namespace Twitch.Net.Client.Client.Handlers.Join;

internal class JoinQueueHandler
{
    private CancellationTokenSource _tokenSource;
    private readonly IrcClient _client;
    private TimeLimiter _rateLimiter;

    public JoinQueueHandler(
        IrcClient client, 
        IUserAccountStatusResolver accountStatusResolver
        )
    {
        _tokenSource = new CancellationTokenSource();
        _client = client;
        _rateLimiter = TimeLimiter.GetFromMaxCountByInterval(
            20, // base 20 as docs says for none verified accounts
            new TimeSpan(10)
        );

        Task.Run(async () =>
        {
            var status = await accountStatusResolver.ResolveUserAccountStatusAsync();
            if (status.IsVerifiedBot)
                _rateLimiter = TimeLimiter.GetFromMaxCountByInterval(
                    2000, // rate limits from docs for verified bots
                    new TimeSpan(10)
                );
        });
    }

    public void Enqueue(ChatChannel channel, Action failure) =>
        Task.Run(() => // fire and forget
        {
            if (!_client.IsConnected)
            {
                failure();
                return; // if we are not connected we wont even try
            }

            channel.SetConnectionState(ChatChannelConnectionState.Queued);
            var result = _rateLimiter.Enqueue(() => PerformJoin(channel, failure), _tokenSource.Token);
            if (result.IsCanceled || result.IsFaulted)
                failure();
        });

    private void PerformJoin(ChatChannel channel, Action failure)
    {
        channel.SetConnectionState(ChatChannelConnectionState.Connecting);
        // if we fail sending it, then we just directly failure it
        if (!_client.SendRaw(Rfc2812Parser.Join($"#{channel.ChannelName.ToLower()}")))
        {
            failure();
            return;
        }
            
        Task.Run(async () =>
        {
            // if the status of the channel does not update from connecting to connected within 10 seconds
            // then we will just assume it failed to join the channel ( Connected state is set by MessageHandler )
            await Task.Delay(10000);
            if (channel.ConnectionState == ChatChannelConnectionState.Connecting)
                failure();
        });
    }

    public void Reset()
    {
        _tokenSource.Cancel();
        _tokenSource = new CancellationTokenSource();
    }
}