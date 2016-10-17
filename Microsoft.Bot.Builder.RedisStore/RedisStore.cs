// MIT License
// 
// Copyright (c) 2016 Ankit Sinha
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.Bot.Connector;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using StackExchange.Redis;
using Newtonsoft.Json;
using System.IO.Compression;
using System.IO;

namespace Microsoft.Bot.Builder.Dialogs.Internals
{
    public class RedisStore : IBotDataStore<BotData>
    {
        private const string ETAG_KEY = "etag";
        private const string DATA_KEY = "data";

        private RedisStoreOptions _options;
        private static ConnectionMultiplexer _connection;

        public RedisStore(RedisStoreOptions redisOptions)
        {
            _options = redisOptions;
        }

        private void Connect()
        {
            if (_connection == null)
            {
                _connection = ConnectionMultiplexer.Connect(_options.Configuration);
            }
        }

        public Task<bool> FlushAsync(BotDataKey key, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public async Task<BotData> LoadAsync(BotDataKey key, BotStoreType botStoreType, CancellationToken cancellationToken)
        {
            Connect();

            var database = _connection.GetDatabase(_options.Database);
            var redisKey = GetKey(key, botStoreType);
            var result = await database.HashGetAllAsync(redisKey);
            if (result == null || result.Count() == 0)
            {
                return null;
            }

            var botData = new BotData();
            botData.ETag = result.Where(t => t.Name.Equals(ETAG_KEY)).FirstOrDefault().Value;
            botData.Data = Deserialize((byte[])result.Where(t => t.Name.Equals(DATA_KEY)).FirstOrDefault().Value);
            return botData;
        }

        public async Task SaveAsync(BotDataKey key, BotStoreType botStoreType, BotData data, CancellationToken cancellationToken)
        {
            Connect();

            var redisKey = GetKey(key, botStoreType);
            var serializedData = Serialize(data.Data);

            var database = _connection.GetDatabase(_options.Database);
            var tran = database.CreateTransaction();
            if (data.ETag != "*")
                tran.AddCondition(Condition.HashEqual(redisKey, ETAG_KEY, data.ETag));
            tran.HashSetAsync(redisKey, new HashEntry[]
            {
                new HashEntry(ETAG_KEY, DateTime.UtcNow.Ticks.ToString()),
                new HashEntry(DATA_KEY, serializedData)
            });

            bool committed = await tran.ExecuteAsync();

            if (!committed)
                throw new ConcurrencyException("Inconsistent SaveAsync based on ETag!");
        }

        private string GetKey(BotDataKey key, BotStoreType botStoreType)
        {
            switch (botStoreType)
            {
                case BotStoreType.BotConversationData:
                    return $"conversation:{key.BotId}:{key.ChannelId}:{key.ConversationId}";
                case BotStoreType.BotUserData:
                    return $"user:{key.BotId}:{key.ChannelId}:{key.UserId}";
                case BotStoreType.BotPrivateConversationData:
                    return $"privateConversation:{key.BotId}:{key.ChannelId}:{key.UserId}:{key.ConversationId}";
                default:
                    throw new ArgumentException("Unsupported bot store type!");
            }
        }

        private static byte[] Serialize(object data)
        {
            using (var cmpStream = new MemoryStream())
            using (var stream = new GZipStream(cmpStream, CompressionMode.Compress))
            using (var streamWriter = new StreamWriter(stream))
            {
                var serializedJSon = JsonConvert.SerializeObject(data);
                streamWriter.Write(serializedJSon);
                streamWriter.Close();
                stream.Close();
                return cmpStream.ToArray();
            }
        }

        private static object Deserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var gz = new GZipStream(stream, CompressionMode.Decompress))
            using (var streamReader = new StreamReader(gz))
            {
                return JsonConvert.DeserializeObject(streamReader.ReadToEnd());
            }
        }
    }
}
