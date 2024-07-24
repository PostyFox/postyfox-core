using Twitch.Net.Api.Apis.Helix.Models;
using Twitch.Net.Shared.Extensions;

namespace Twitch.Net.Api.Apis.Helix;

public class Users
{
    private readonly IApiBase _apiBase;

    internal Users(IApiBase apiBase)
    {
        _apiBase = apiBase;
    }
        
    public async Task<HelixUsersResponse> GetUsersAsync(
        IReadOnlyList<string> ids = null,
        IReadOnlyList<string> logins = null,
        string token = null)
    {
        var keySet = new List<KeyValuePair<string,string>>();
            
        ids?.Where(s => !string.IsNullOrEmpty(s))
            .ForEach(id => keySet.Add(new KeyValuePair<string, string>("id", id)));
            
        logins?.Where(s => !string.IsNullOrEmpty(s))
            .ForEach(login => keySet.Add(new KeyValuePair<string, string>("login", login)));

        var users = new List<HelixUserResponse>();
        var requests = 0;
        var successful = 0;
        await keySet.SplitList(100)
            .Where(d => d.Count > 0)
            .ForEachAsync(async set =>
            {
                var resp = await _apiBase.GetAsync<HelixUsersResponse>("/users", set, token);
                resp.MatchSome(res => users.AddRange(res.Users));
                requests++;
                successful += resp.HasValue ? 1 : 0;
            });
        return new HelixUsersResponse
        {
            Users = users,
            Requests = requests,
            Successfully = successful
        };
    }
}