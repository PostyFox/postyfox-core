using Google.Protobuf.WellKnownTypes;
using TL;

namespace PostyFox_NetCore.Helpers
{
    // To be honest, this is a complete an utter hack, and will likely have issues in Function Apps
    // BUT Telegram needs state for a short period during auth, so will have to see if it this is enough.
    internal class StaticState
    {
        /// <summary>
        /// Holds Telegram Clients that are being used for authentication; userId is used as the hash.
        /// </summary>
        internal static Dictionary<string, WTelegram.Client> TelegramClients = new();

        internal static WTelegram.Client GetTelegramClient(int apiId, string apiHash, string userId)
        {
            if (TelegramClients.ContainsKey(userId))
            {
                return TelegramClients[userId];
            } 
            else
            {
                WTelegram.Client client = new WTelegram.Client(apiId, apiHash, userId);
                TelegramClients.Add(userId, client);
                return client;
            }
        }

        internal static void DisposeTelegramClient(string userId)
        {
            if (TelegramClients.ContainsKey(userId))
            {
                TelegramClients[userId].Dispose();
                TelegramClients.Remove(userId);
            }
        }
    }
}
