using Azure.Storage.Blobs;
using Google.Protobuf.WellKnownTypes;
using PostyFox_NetCore.Integrations;
using TL;
using PostyFox_DataLayer;

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
        internal static WTelegram.Client GetTelegramClient(int apiId, string apiHash, string userId, BlobServiceClient blobServiceClient, string userPhoneNumber = "")
        {
            if (TelegramClients.ContainsKey(userId))
            {
                if (TelegramClients[userId].Disconnected)
                {
                    TelegramClients[userId].LoginUserIfNeeded();
                }

                return TelegramClients[userId];
            } 
            else
            {
                TelegramStore store = new TelegramStore(userId, blobServiceClient);
                WTelegram.Client client = new WTelegram.Client((val) =>
                {
                    if (val == "api_id") return apiId.ToString();
                    if (val == "api_hash") return apiHash;
                    if (val == "phone_number") return userPhoneNumber;
                    return null;
                }, store);
                var task = client.LoginUserIfNeeded();
                task.Wait();
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
