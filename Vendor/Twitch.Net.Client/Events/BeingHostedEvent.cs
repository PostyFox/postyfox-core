using Twitch.Net.Client.Irc;

namespace Twitch.Net.Client.Events;

public class BeingHostedEvent
{
    public string Channel { get; }
    public string HostedByChannel { get; }
    public bool IsAutoHosted { get; }
    public int Viewers { get; }

    public BeingHostedEvent(IrcMessage ircMessage)
    {
        Channel = ircMessage.Channel;
        HostedByChannel = ircMessage.Message.Split(' ').First();

        if (ircMessage.Message.Contains("up to "))
        {
            var split = ircMessage.Message.Split(new[] { "up to " }, StringSplitOptions.None);
            if (split[1].Contains(" ") && int.TryParse(split[1].Split(' ')[0], out var n))
                Viewers = n;
        }

        if (ircMessage.Message.Contains("auto hosting"))
            IsAutoHosted = true;
    }
}