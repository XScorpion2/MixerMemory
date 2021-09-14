using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System;

namespace MixerMemory
{
    public class MatchTypeConverter : JsonConverter
    {
        private readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JToken t = JToken.FromObject(value.ToString());
            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string value = reader.Value as string;
            try
            {
                return Enum.Parse(typeof(MatchType), value);
            }
            catch
            {
                m_Logger.Error("Invalid Rule Type {requestedType} at {jsonPath}. Returning default rule type 'NameIs'.", value, reader.Path);
            }
            return MatchType.NameIs;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(MatchType);
        }
    }
}
