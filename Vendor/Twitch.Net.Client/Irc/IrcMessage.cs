using System.Text;

namespace Twitch.Net.Client.Irc;

public class IrcMessage
{
    private readonly string[] _parameters;
        
    public string Channel 
        => Params.StartsWith("#") 
            ? Params.Remove(0, 1) 
            : Params;
        
    public string Params
        => _parameters != null && _parameters.Length > 0 
            ? _parameters[0] 
            : string.Empty;
        
    public string Trailing => 
        _parameters != null && _parameters.Length > 1 
            ? _parameters[^1] 
            : string.Empty;
        
    public string Message => Trailing;
    public string User { get; }
    public string HostMask { get; }
    public IrcCommand Command { get; }
    internal Dictionary<string, string> InternalTags { get; }
    public IReadOnlyDictionary<string, string> Tags => InternalTags;
        
    internal IrcMessage(string user)
    {
        _parameters = Array.Empty<string>();
        User = user;
        HostMask = string.Empty;
        Command = IrcCommand.Unknown;
        InternalTags = new Dictionary<string, string>();
    }

    internal IrcMessage(
        IrcCommand command,
        string[] parameters,
        string hostMask,
        Dictionary<string, string> tags = null)
    {
        var idx = hostMask.IndexOf('!');
        User = idx != -1 ? hostMask.Substring(0, idx) : hostMask;
        HostMask = hostMask;
        _parameters = parameters;
        Command = command;
        InternalTags = tags;
    }

    public override string ToString()
    {
        var raw = new StringBuilder(32);
        if (Tags != null)
        {
            var tags = new string[Tags.Count];
            var i = 0;
            foreach (var (key, value) in Tags)
                tags[i++] = $"{key}={value}";

            if (tags.Length > 0)
                raw.Append('@').Append(string.Join(";", tags)).Append(' ');
        }

        if (!string.IsNullOrEmpty(HostMask))
            raw.Append(':').Append(HostMask).Append(' ');

        raw.Append(Command.ToString().ToUpper().Replace("RPL_", ""));
            
        if (_parameters.Length <= 0)
            return raw.ToString();

        if (_parameters[0] != null && _parameters[0].Length > 0)
            raw.Append(' ').Append(_parameters[0]);

        if (_parameters.Length > 1 && _parameters[1] != null && _parameters[1].Length > 0)
            raw.Append(" :").Append(_parameters[1]);

        return raw.ToString();
    }
}