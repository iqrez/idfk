using System;
using System.Text.Json;

namespace WootMouseRemap.Security
{
    public static class SecureJsonDeserializer
    {
        public static T? TryDeserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            
            // Check for suspicious patterns
            if (json.Contains("$type") || json.Contains("__type") || json.Contains("assembly"))
            {
                return null;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                return JsonSerializer.Deserialize<T>(json, options);
            }
            catch
            {
                return null;
            }
        }

        public static T? DeserializeSecure<T>(string json) where T : class
        {
            return TryDeserialize<T>(json);
        }
    }
}