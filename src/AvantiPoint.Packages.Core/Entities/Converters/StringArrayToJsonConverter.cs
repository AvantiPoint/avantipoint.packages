using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AvantiPoint.Packages.Core
{
    public class StringArrayToJsonConverter : ValueConverter<string[], string>
    {
        public static readonly StringArrayToJsonConverter Instance = new ();

        public StringArrayToJsonConverter()
            : base(
                v => Serialize(v),
                v => Deserialize(v))
        {
        }

        private static string Serialize(IEnumerable<string> input)
        {
            if(input is null)
                input = Array.Empty<string>();

            input = input.Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x.Trim());

            return JsonSerializer.Serialize(input);
        }

        private static string[] Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return Array.Empty<string>();

            try
            {
                return JsonSerializer.Deserialize<string[]>(json);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
