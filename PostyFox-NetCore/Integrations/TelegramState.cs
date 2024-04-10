using Azure.Storage.Blobs;

namespace PostyFox_NetCore.Integrations
{
    class TelegramStore : MemoryStream
    {
        private readonly string _sessionName;
        private BlobServiceClient _blobServiceClient;
        private BlobContainerClient _containerClient;
        private BlobClient _blobClient;

        public TelegramStore(string sessionName, BlobServiceClient blobServiceClient)
        {
            _sessionName = sessionName;
            _blobServiceClient = blobServiceClient;
            _containerClient = _blobServiceClient.GetBlobContainerClient("telegram");
            _containerClient.CreateIfNotExists();
            _blobClient = _containerClient.GetBlobClient(_sessionName);
            if (_blobClient.Exists().Value)
            {
                // Fetch the blob from store and load existing session data
                _blobClient.DownloadTo(this);
            }
        }

        protected override void Dispose(bool disposing)
        {
            // Force flush
            Flush();
        }

        public override void Flush() 
        {
            if (this.Length > 0)
            {
                // Write the data back to Storage Account
                _blobClient.Upload(this, true);
            }
        }

    }
}
