using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Indexing
{
    public static class IndexingExtensions
    {
        private static void Check(JsonToken type, JsonTextReader reader)
        {
            if (reader.TokenType != type)
                throw new Exception("Invalid JSON");
        }

        private static void Check(bool read, JsonTextReader reader)
        {
            if (!read)
                throw new Exception("Invalid JSON");
        }

        public static string EventIndexName(this string eventData)
        {
            var reader = new JsonTextReader(new StringReader(eventData));
            Check(reader.Read(), reader);
            Check(JsonToken.StartObject, reader);

            while(true)
            {
                Check(reader.Read(), reader);
                if (reader.TokenType == JsonToken.EndObject)
                    throw new Exception("Unexpected end of JSON");
                Check(JsonToken.PropertyName, reader);

                if(string.Compare((string)reader.Value, "index_name") != 0) continue;

                Check(reader.Read(), reader);
                Check(JsonToken.String, reader);
                return (string)reader.Value;
            }
        }
    }
}
