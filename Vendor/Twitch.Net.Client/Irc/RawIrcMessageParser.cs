namespace Twitch.Net.Client.Irc;

/**
     * Credits:
     * 3ventic - For the base of the parser
     * Syzuna - Fixing a missing state regarding trail
     * swiftyspiffy - Added missing CLEARMSG
     *
     * Some minor modification made to clean up & follow rest of the code pattern in rest of the project
     */
internal static class RawIrcMessageParser
{
    public static IrcMessage ParseRawIrcMessage(string raw)
    {
        var tags = new Dictionary<string, string>();

        var state = ParserState.StateNone;
        var starts = new[] { 0, 0, 0, 0, 0, 0 };
        var lens = new[] { 0, 0, 0, 0, 0, 0 };
        for (var i = 0; i < raw.Length; ++i)
        {
            lens[(int)state] = i - starts[(int)state] - 1;
            if (state == ParserState.StateNone && raw[i] == '@')
            {
                state = ParserState.StateV3;
                starts[(int)state] = ++i;

                var start = i;
                string key = null;
                for (; i < raw.Length; ++i)
                {
                    switch (raw[i])
                    {
                        case '=':
                            key = raw.Substring(start, i - start);
                            start = ++i;
                            break;
                        case ';':
                        case ' ':
                            tags[key ?? raw.Substring(start, i - start)] =
                                key == null ? "1" : raw.Substring(start, i - start);
                            break;
                    }

                    if (raw[i] == ';')
                        start = ++i;
                    else if (raw[i] == ' ')
                        break;
                }
            }
            else if (state < ParserState.StatePrefix && raw[i] == ':')
            {
                state = ParserState.StatePrefix;
                starts[(int)state] = ++i;
            }
            else if (state < ParserState.StateCommand)
            {
                state = ParserState.StateCommand;
                starts[(int)state] = i;
            }
            else if (state < ParserState.StateTrailing && raw[i] == ':')
            {
                state = ParserState.StateTrailing;
                starts[(int)state] = ++i;
                break;
            }
            else if (state < ParserState.StateTrailing && raw[i] == '+' || state < ParserState.StateTrailing && raw[i] == '-')
            {
                state = ParserState.StateTrailing;
                starts[(int)state] = i;
                break;
            }
            else if (state == ParserState.StateCommand)
            {
                state = ParserState.StateParam;
                starts[(int)state] = i;
            }

            while (i < raw.Length && raw[i] != ' ')
                ++i;
        }

        lens[(int)state] = raw.Length - starts[(int)state];
        var cmd = raw.Substring(starts[(int)ParserState.StateCommand],
            lens[(int)ParserState.StateCommand]);

        var command = ParseCommandType(cmd);

        var parameters = raw.Substring(starts[(int)ParserState.StateParam],
            lens[(int)ParserState.StateParam]);
        var message = raw.Substring(starts[(int)ParserState.StateTrailing],
            lens[(int)ParserState.StateTrailing]);
        var hostMask = raw.Substring(starts[(int)ParserState.StatePrefix],
            lens[(int)ParserState.StatePrefix]);
        return new IrcMessage(command, new[] { parameters, message }, hostMask, tags);
    }
        
    private static IrcCommand ParseCommandType(string type) 
        => type switch
        {
            "PRIVMSG" => IrcCommand.PrivMsg,
            "NOTICE" => IrcCommand.Notice,
            "PING" => IrcCommand.Ping,
            "PONG" => IrcCommand.Pong,
            "HOSTTARGET" => IrcCommand.HostTarget,
            "CLEARCHAT" => IrcCommand.ClearChat,
            "CLEARMSG" => IrcCommand.ClearMsg,
            "USERSTATE" => IrcCommand.UserState,
            "GLOBALUSERSTATE" => IrcCommand.GlobalUserState,
            "NICK" => IrcCommand.Nick,
            "JOIN" => IrcCommand.Join,
            "PART" => IrcCommand.Part,
            "PASS" => IrcCommand.Pass,
            "CAP" => IrcCommand.Cap,
            "001" => IrcCommand.Rpl001,
            "002" => IrcCommand.Rpl002,
            "003" => IrcCommand.Rpl003,
            "004" => IrcCommand.Rpl004,
            "353" => IrcCommand.Rpl353,
            "366" => IrcCommand.Rpl366,
            "372" => IrcCommand.Rpl372,
            "375" => IrcCommand.Rpl375,
            "376" => IrcCommand.Rpl376,
            "WHISPER" => IrcCommand.Whisper,
            "SERVERCHANGE" => IrcCommand.ServerChange,
            "RECONNECT" => IrcCommand.Reconnect,
            "ROOMSTATE" => IrcCommand.RoomState,
            "USERNOTICE" => IrcCommand.UserNotice,
            "MODE" => IrcCommand.Mode,
            _ => IrcCommand.Unknown
        };

    private enum ParserState
    {
        StateNone,
        StateV3,
        StatePrefix,
        StateCommand,
        StateParam,
        StateTrailing
    };
}