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
        private DateTime _lastWrite;
        private Task _delayedWrite;

        public TelegramStore(string storageAccount, string containerName, string sessionName, IAzureClientFactory<BlobServiceClient> clientFactory)
        {
            _sessionName = sessionName;
            _blobServiceClient = clientFactory.CreateClient("StorageAccount");
            _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            _containerClient.CreateIfNotExists();
            _blobClient = _containerClient.GetBlobClient(sessionName);
            if (_blobClient.Exists())
            {
                // Fetch the blob from store and load existing session data
            }


            //using var cmd = new NpgsqlCommand($"SELECT data FROM WTelegram_sessions WHERE name = '{_sessionName}'", _sql);
            //using var rdr = cmd.ExecuteReader();
            //if (rdr.Read())
            //    _dataLen = (_data = rdr[0] as byte[]).Length;
        }

        protected override void Dispose(bool disposing)
        {
            //_delayedWrite?.Wait();
            //_sql.Dispose();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Copy(_data, 0, buffer, offset, count);
            return count;
        }

        public override void Write(byte[] buffer, int offset, int count) // Write call and buffer modifications are done within a lock()
        {
            //_data = buffer; _dataLen = count;
            //if (_delayedWrite != null) return;
            //var left = 1000 - (int)(DateTime.UtcNow - _lastWrite).TotalMilliseconds;
            //if (left < 0)
            //{
            //    using var cmd = new NpgsqlCommand($"INSERT INTO WTelegram_sessions (name, data) VALUES ('{_sessionName}', @data) ON CONFLICT (name) DO UPDATE SET data = EXCLUDED.data", _sql);
            //    cmd.Parameters.AddWithValue("data", count == buffer.Length ? buffer : buffer[offset..(offset + count)]);
            //    cmd.ExecuteNonQuery();
            //    _lastWrite = DateTime.UtcNow;
            //}
            //else // delay writings for a full second
            //    _delayedWrite = Task.Delay(left).ContinueWith(t => { lock (this) { _delayedWrite = null; Write(_data, 0, _dataLen); } });
        }

        public override long Length => _dataLen;
        public override long Position { get => 0; set { } }
        public override bool CanSeek => false;
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Flush() { }
    }
}
