using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;

namespace MCHI
{
    class StringDictionary
    {
        private Dictionary<string, string> lut;
        private string dictPath;

        public StringDictionary(string dictPath)
        {
            this.dictPath = dictPath;
            if (File.Exists(dictPath))
            {
                var json = File.ReadAllText(dictPath);
                lut = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            else
            {
                lut = new Dictionary<string, string>();
                SaveDict();
            }
        }

        public void SaveDict()
        {
            using (var fs = File.Open(dictPath, FileMode.OpenOrCreate))
            {
                JsonSerializer.Serialize(
                    new Utf8JsonWriter(fs, new JsonWriterOptions
                    {
                        Indented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }),
                    lut);
            }
        }

        public string Translate(string jp)
        {
            string en;
            if (lut.TryGetValue(jp, out en))
            {
                return en;
            }
            else
            {
                InsertUntranslated(jp);
                SaveDict();
                return jp;
            }
        }

        public void InsertUntranslated(string jp)
        {
            lut.Add(jp, null);
        }
    }
}
