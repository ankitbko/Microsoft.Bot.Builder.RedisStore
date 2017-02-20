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
// SOFTWARE.using Microsoft.Bot.Connector;

using Autofac;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Bot.Builder.Dialogs.Internals
{
	public class RedisBotCache
	{
		const string ETAG_KEY = "etag";
		const string DATA_KEY = "data";

		public static void Configure(RedisStoreOptions options)
		{
			RedisBotCache.options = options;
			connection = null;
			Register();
		}

		static void Register()
		{
			var builder = new ContainerBuilder();

			builder.Register(c => new RedisBotDataStore())
			   .As<RedisBotDataStore>()
			   .SingleInstance();

			builder.Register(c => new CachingBotDataStore
			(
				c.Resolve<RedisBotDataStore>(),
				CachingBotDataStoreConsistencyPolicy.ETagBasedConsistency
			))
			.As<IBotDataStore<BotData>>()
			.AsSelf()
			.InstancePerLifetimeScope();

			builder.Update(Conversation.Container);
		}


		static RedisStoreOptions options;

		static ConnectionMultiplexer connection;
		static ConnectionMultiplexer Connection
		{
			get
			{
				if (connection == null)
				{
					if (options == null)
					{
						throw new InvalidOperationException(
							$"You have to configure the cache via {nameof(RedisBotCache.Configure)} before using it");
					}
					connection = ConnectionMultiplexer.Connect(options.Configuration);
				}
				return connection;
			}
		}

		public static async Task<BotData> LoadAsync(IAddress key, BotStoreType botStoreType, CancellationToken cancellationToken)
		{
			var database = Connection.GetDatabase(options.Database);
			var redisKey = GetKey(key, botStoreType);
			var result = await database.HashGetAllAsync(redisKey).ConfigureAwait(false);
			if (result == null || result.Count() == 0)
			{
				return null;
			}

			var botData = new BotData()
			{
				ETag = result.Where(t => t.Name.Equals(ETAG_KEY)).Single().Value,
				Data = Deserialize(result.Where(t => t.Name.Equals(DATA_KEY)).Single().Value)
			};
			return botData;
		}

		public static async Task SaveAsync(IAddress key, BotStoreType botStoreType, BotData data, CancellationToken cancellationToken)
		{
			var redisKey = GetKey(key, botStoreType);
			var serializedData = Serialize(data.Data);

			var database = Connection.GetDatabase(options.Database);

			var tran = database.CreateTransaction();
			if (data.ETag != "*")
			{
				tran.AddCondition(Condition.HashEqual(redisKey, ETAG_KEY, data.ETag));
			}

			tran.HashSetAsync(redisKey, new HashEntry[]
			{
				new HashEntry(ETAG_KEY, DateTime.UtcNow.Ticks.ToString()),
				new HashEntry(DATA_KEY, serializedData)
			})
			.ConfigureAwait(false);

			var committed = await tran.ExecuteAsync().ConfigureAwait(false);

			if (!committed)
			{
				throw new ConcurrencyException("Inconsistent SaveAsync based on ETag!");
			}
		}

		static string GetKey(IAddress key, BotStoreType botStoreType)
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

		static byte[] Serialize(object data)
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

		static object Deserialize(byte[] bytes)
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
