using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostyFox_NetCore.Integrations
{
    class TelegramStore : Stream
    {
        private readonly string _sessionName;
        private BlobServiceClient _blobServiceClient;
        private BlobContainerClient _containerClient;
        private BlobClient _blobClient;
        private byte[] _data;
        private int _dataLen;

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

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Copy(_data, 0, buffer, offset, count);
            return count;
        }

        public override void Write(byte[] buffer, int offset, int count) // Write call and buffer modifications are done within a lock()
        {
            _data = buffer; _dataLen = count;
        }

        public override long Length => _dataLen;
        public override long Position { get => 0; set { } }
        public override bool CanSeek => false;
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Flush() 
        {
            if (_data != null && _dataLen > 0)
            {
                // Write the data back to Storage Account
                _blobClient.Upload(this, true);
            }
        }
    }
}
