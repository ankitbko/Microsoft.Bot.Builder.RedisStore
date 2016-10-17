using System;
using System.Runtime.Serialization;

namespace Microsoft.Bot.Builder.Dialogs.Internals
{
    [Serializable]
    internal class ConcurrencyException : Exception
    {
        public ConcurrencyException()
        {
        }

        public ConcurrencyException(string message) : base(message)
        {
        }

        public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ConcurrencyException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}