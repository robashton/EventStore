using System;
using System.Runtime.Serialization;

namespace EventStore.Projections.Core.Indexing
{
    public class LuceneException : Exception, ISerializable
    {
        public LuceneException() : this("Something bad went wrong with Lucene")
        {
        }

        public LuceneException(string message) : this(message, null)
        {

        }

        public LuceneException(string message, Exception inner) : base(message, inner)
        {

        }

        protected LuceneException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }
    }
}
