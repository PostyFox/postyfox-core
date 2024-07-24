using System.Text;
using System.Text.RegularExpressions;
using static System.String;

namespace Twitch.Net.Client.Irc;

/**
     * Could be improved by changing single string to return a const instead
     */
public static class Rfc2812Parser
{
    private static readonly Regex NicknameRegex =
        new(@"^[A-Za-z\[\]\\`_^{|}][A-Za-z0-9\[\]\\`_\-^{|}]+$", RegexOptions.Compiled);

    public static string Mode(string target, string[] newModes, string[] newModeParameters)
    {
        if (newModes == null)
            throw new ArgumentNullException(nameof(newModes));
        if (newModeParameters == null)
            throw new ArgumentNullException(nameof(newModeParameters));
        if (newModes.Length != newModeParameters.Length)
            throw new ArgumentException("newModes and newModeParameters must have the same size.");

        var newMode = new StringBuilder(newModes.Length);
        var newModeParameter = new StringBuilder();

        // as per RFC 3.2.3, maximum is 3 modes changes at once
        var maxModeChanges = 3;
        if (newModes.Length > maxModeChanges)
            throw new ArgumentOutOfRangeException(
                nameof(newModes.Length),
                newModes.Length,
                $"Mode change list is too large (> {maxModeChanges})."
            );

        for (var i = 0; i <= newModes.Length; i += maxModeChanges)
        {
            for (var j = 0; j < maxModeChanges; j++)
            {
                if (i + j >= newModes.Length) break;

                newMode.Append(newModes[i + j]);
            }

            for (var j = 0; j < maxModeChanges; j++)
            {
                if (i + j >= newModeParameters.Length) break;

                newModeParameter.Append(newModeParameters[i + j]);
                newModeParameter.Append(' ');
            }
        }

        if (newModeParameter.Length > 0)
        {
            newModeParameter.Length--; // remove trailing space
            newMode.Append(' ');
            newMode.Append(newModeParameter.ToString());
        }

        return Mode(target, newMode.ToString());
    }

    public static bool IsValidNickname(string nickname)
        => !IsNullOrEmpty(nickname) && NicknameRegex.Match(nickname).Success;

    public static string Pass(string password)
        => $"PASS {password}";

    public static string Nick(string nickname)
        => $"NICK {nickname}";

    public static string User(string username, int userMode, string realName)
        => $"USER {username} {userMode} * :{realName}";

    public static string Oper(string name, string password)
        => $"OPER {name} {password}";

    public static string Privmsg(string destination, string message)
        => $"PRIVMSG {destination} :{message}";

    public static string Notice(string destination, string message)
        => $"NOTICE {destination} :{message}";

    public static string Join(string channel)
        => $"JOIN {channel}";

    public static string Join(string[] channels)
        => $"JOIN {string.Join(",", channels)}";

    public static string Join(string channel, string key)
        => $"JOIN {channel} {key}";

    public static string Join(string[] channels, string[] keys)
        => $"JOIN {string.Join(",", channels)} {string.Join(",", keys)}";
        
    public static string Part(string channel)
        => $"PART {channel}";

    public static string Part(string[] channels)
        => $"PART {string.Join(",", channels)}";

    public static string Part(string channel, string partMessage)
        => "PART " + channel + " :" + partMessage;

    public static string Part(string[] channels, string partMessage)
        => $"PART {string.Join(",", channels)} :{partMessage}";

    public static string Kick(string channel, string nickname)
        => $"KICK {channel} {nickname}";

    public static string Kick(string channel, string nickname, string comment)
        => $"KICK {channel} {nickname} :{comment}";

    public static string Kick(string[] channels, string nickname)
        => $"KICK {string.Join(",", channels)} {nickname}";

    public static string Kick(string[] channels, string nickname, string comment)
        => $"KICK {string.Join(",", channels)} {nickname} :{comment}";

    public static string Kick(string channel, string[] nicknames)
        => $"KICK {channel} {string.Join(",", nicknames)}";

    public static string Kick(string channel, string[] nicknames, string comment)
        => $"KICK {channel} {string.Join(",", nicknames)} :{comment}";

    public static string Kick(string[] channels, string[] nicknames)
        => $"KICK {string.Join(",", channels)} {string.Join(",", nicknames)}";

    public static string Kick(string[] channels, string[] nicknames, string comment)
        => $"KICK {string.Join(",", channels)} {string.Join(",", nicknames)} :{comment}";

    public static string Motd()
        => "MOTD";

    public static string Motd(string target)
        => $"MOTD {target}";

    public static string Lusers()
        => "LUSERS";

    public static string Lusers(string mask)
        => $"LUSER {mask}";

    public static string Lusers(string mask, string target)
        => $"LUSER {mask} {target}";

    public static string Version()
        => "VERSION";

    public static string Version(string target)
        => $"VERSION {target}";

    public static string Stats()
        => "STATS";

    public static string Stats(string query)
        => $"STATS {query}";

    public static string Stats(string query, string target)
        => $"STATS {query} {target}";

    public static string Links()
        => "LINKS";

    public static string Links(string serverMask)
        => $"LINKS {serverMask}";

    public static string Links(string remoteServer, string serverMask)
        => $"LINKS {remoteServer} {serverMask}";

    public static string Time()
        => "TIME";

    public static string Time(string target)
        => $"TIME {target}";

    public static string Connect(string targetServer, string port)
        => $"CONNECT {targetServer} {port}";

    public static string Connect(string targetServer, string port, string remoteServer)
        => $"CONNECT {targetServer} {port} {remoteServer}";

    public static string Trace()
        => "TRACE";

    public static string Trace(string target)
        => $"TRACE {target}";
        
    public static string Admin()
        => "ADMIN";
        
    public static string Admin(string target)
        => $"ADMIN {target}";

    public static string Info()
        => "INFO";

    public static string Info(string target)
        => $"INFO {target}";

    public static string Servlist()
        => "SERVLIST";

    public static string Servlist(string mask)
        => $"SERVLIST {mask}";

    public static string Servlist(string mask, string type)
        => $"SERVLIST {mask} {type}";

    public static string Squery(string serviceName, string serviceText)
        => $"SQUERY {serviceName} :{serviceText}";

    public static string List()
        => "LIST";

    public static string List(string channel)
        => $"LIST {channel}";

    public static string List(string[] channels)
        => $"LIST {string.Join(",", channels)}";

    public static string List(string channel, string target)
        => $"LIST {channel} {target}";

    public static string List(string[] channels, string target)
        => $"LIST {string.Join(",", channels)} {target}";

    public static string Names()
        => "NAMES";

    public static string Names(string channel)
        => $"NAMES {channel}";

    public static string Names(string[] channels)
        => $"NAMES {string.Join(",", channels)}";

    public static string Names(string channel, string target)
        => $"NAMES {channel} {target}";

    public static string Names(string[] channels, string target)
        => $"NAMES {string.Join(",", channels)} {target}";

    public static string Topic(string channel)
        => $"TOPIC {channel}";

    public static string Topic(string channel, string newTopic)
        => $"TOPIC {channel} :{newTopic}";

    public static string Mode(string target)
        => $"MODE {target}";

    public static string Mode(string target, string newMode)
        => $"MODE {target} {newMode}";

    public static string Service(string nickname, string distribution, string info)
        => $"SERVICE {nickname} * {distribution} * * :{info}";

    public static string Invite(string nickname, string channel)
        => $"INVITE {nickname} {channel}";

    public static string Who()
        => "WHO";

    public static string Who(string mask)
        => $"WHO {mask}";
        
    public static string Who(string mask, bool irCop) 
        => irCop ? $"WHO {mask} o" : $"WHO {mask}";

    public static string Whois(string mask)
        => $"WHOIS {mask}";

    public static string Whois(string[] masks)
        => $"WHOIS {string.Join(",", masks)}";
        
    public static string Whois(string target, string mask)
        => $"WHOIS {target} {mask}";

    public static string Whois(string target, string[] masks)
        => $"WHOIS {target} {string.Join(",", masks)}";

    public static string Whowas(string nickname)
        => $"WHOWAS {nickname}";

    public static string Whowas(string[] nicknames)
        => $"WHOWAS {string.Join(",", nicknames)}";

    public static string Whowas(string nickname, string count)
        => $"WHOWAS {nickname} {count} ";

    public static string Whowas(string[] nicknames, string count)
        => $"WHOWAS {string.Join(",", nicknames)} {count} ";

    public static string Whowas(string nickname, string count, string target)
        => $"WHOWAS {nickname} {count} {target}";

    public static string Whowas(string[] nicknames, string count, string target)
        => $"WHOWAS {string.Join(",", nicknames)} {count} {target}";

    public static string Kill(string nickname, string comment)
        => $"KILL {nickname} :{comment}";

    public static string Ping(string server)
        => $"PING {server}";

    public static string Ping(string server, string server2)
        => $"PING {server} {server2}";

    public static string Pong(string server)
        => $"PONG {server}";

    public static string Pong(string server, string server2)
        => $"PONG {server} {server2}";

    public static string Error(string errormessage)
        => $"ERROR :{errormessage}";

    public static string Away()
        => "AWAY";

    public static string Away(string awayText)
        => $"AWAY :{awayText}";

    public static string Rehash()
        => "REHASH";

    public static string Die()
        => "DIE";

    public static string Restart()
        => "RESTART";

    public static string Summon(string user)
        => $"SUMMON {user}";

    public static string Summon(string user, string target)
        => $"SUMMON {user} {target}";

    public static string Summon(string user, string target, string channel)
        => $"SUMMON {user} {target} {channel}";

    public static string Users()
        => "USERS";

    public static string Users(string target)
        => $"USERS {target}";

    public static string Wallops(string wallOpsText)
        => $"WALLOPS :{wallOpsText}";

    public static string Userhost(string nickname)
        => $"USERHOST {nickname}";

    public static string Userhost(string[] nicknames)
        => $"USERHOST {string.Join(" ", nicknames)}";

    public static string Ison(string nickname)
        => $"ISON {nickname}";

    public static string Ison(string[] nicknames)
        => $"ISON {string.Join(" ", nicknames)}";

    public static string Quit()
        => "QUIT";

    public static string Quit(string quitMessage)
        => $"QUIT :{quitMessage}";

    public static string Squit(string server, string comment)
        => $"SQUIT {server} :{comment}";
}