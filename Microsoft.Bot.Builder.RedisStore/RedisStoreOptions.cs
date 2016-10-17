namespace Microsoft.Bot.Builder.Dialogs.Internals
{
    /// <summary>
    /// Configuration options for <see cref="RedisStore"/>.
    /// </summary>
    public class RedisStoreOptions
    {
        /// <summary>
        /// The configuration used to connect to Redis.
        /// </summary>
        public string Configuration { get; set; }

        /// <summary>
        /// Database number to connect to.
        /// </summary>
        public int Database { get; set; } = -1;

    }
}