﻿using Azure.Storage.Blobs;

namespace PostyFox_DataLayer
{
    public class TelegramStore : MemoryStream
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
                Position = 0;
            }
        }

        protected override void Dispose(bool disposing)
        {
            // Force flush
            Flush();
        }

        public override void Flush() 
        {
            if (Length > 0)
            {
                // Write the data back to Storage Account
                long currentPosition = Position;
                lock (this)
                {
                    Position = 0;
                    _blobClient.Upload(this, true);
                }
                Position = currentPosition;
            }
        }

    }
}
