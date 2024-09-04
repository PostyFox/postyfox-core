using Twitch.Net.Api.Apis.Helix.Models;
using Twitch.Net.Shared.Extensions;

namespace Twitch.Net.Api.Apis.Helix;

public class Channels
{
    private readonly IApiBase _apiBase;

    internal Channels(IApiBase apiBase)
    {
        _apiBase = apiBase;
    }
        
    public async Task<HelixChannelsResponse> GetChannelsAsync(
        IReadOnlyList<string> ids = null,
        string token = null)
    {
        var keySet = new List<KeyValuePair<string,string>>();
            
        ids?.Where(s => !string.IsNullOrEmpty(s))
            .ForEach(id => keySet.Add(new KeyValuePair<string, string>("broadcaster_id", id)));

        var channels = new List<HelixChannelResponse>();
        var requests = 0;
        var successful = 0;
        await keySet.SplitList(100)
            .Where(d => d.Count > 0)
            .ForEachAsync(async set =>
            {
                var resp = await _apiBase.GetAsync<HelixChannelsResponse>("/channels", set, token);
                resp.MatchSome(res => channels.AddRange(res.Channels));
                requests++;
                successful += resp.HasValue ? 1 : 0;
            });
        return new HelixChannelsResponse
        {
            Channels = channels,
            Requests = requests,
            Successfully = successful
        };
    }
}