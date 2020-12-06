using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Timers;

namespace MCHI
{
    class StringDictionary
    {
        private Dictionary<string, string> lut;
        private string dictPath;

        public StringDictionary(string dictPath)
        {
            this.dictPath = dictPath;
            saveDebouncer = new Debouncer(200 /* ms */, (Object src, ElapsedEventArgs e) => SaveDict());
            if (File.Exists(dictPath))
            {
                var json = File.ReadAllText(dictPath);
                lut = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                SaveDict(); // reformat
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

        public string Translate(string jp, JORControl context)
        {
            // might need context at some point if we want to deal with ex labels that have wide-ranging %d params
            return Translate(jp);
        }

        private Debouncer saveDebouncer;
        public string Translate(string jp)
        {
            string en;
            if (lut.TryGetValue(jp, out en))
            {
                return en ?? jp;
            }
            else
            {
                InsertUntranslated(jp);

                saveDebouncer.Bounce();

                return jp;
            }
        }

        public void InsertUntranslated(string jp)
        {
            lut.TryAdd(jp, null);
        }
    }
}

class Debouncer
{
    private Timer timer;
    private double timeout_ms;

    public Debouncer(double timeout_ms, ElapsedEventHandler action)
    {
        this.timeout_ms = timeout_ms;
        timer = new Timer(timeout_ms);
        timer.AutoReset = false;
        timer.Elapsed += action;
    }

    public void Bounce()
    {
        // reset timer
        timer.Stop();
        timer.Interval = timeout_ms;
        timer.Start();
    }
}
