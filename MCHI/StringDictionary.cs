using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Timers;
using System.Threading;
using System.Linq;

namespace MCHI
{
    class StringDictionary
    {
        private Dictionary<string, string> lut;
        private string dictPath;
        private FileSystemWatcher fileWatcher;
        private Debouncer saveDebouncer;

        public StringDictionary(string dictPath)
        {
            this.dictPath = dictPath;
            saveDebouncer = new Debouncer(200 /* ms */, (Object src, ElapsedEventArgs e) => SaveDict());

            Reload();
            SaveDict();

            fileWatcher = new FileSystemWatcher();
            fileWatcher.Path = Path.GetDirectoryName(dictPath);
            fileWatcher.Filter = Path.GetFileName(dictPath);
            fileWatcher.Changed += OnDictFileUpdated;

            fileWatcher.EnableRaisingEvents = true;
        }

        private void OnDictFileUpdated(object source, FileSystemEventArgs e)
        {
            while (true)
            {
                try
                {
                    Reload();
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(10);
                }
            }
        }

        public void Reload()
        {
            if (File.Exists(dictPath))
            {
                var json = File.ReadAllText(dictPath);
                lut = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            else
            {
                lut = new Dictionary<string, string>();
            }

#if false
            // purge ASCII keys from dict
            foreach (var key in lut.Keys)
                if (!ShouldTranslateString(key))
                    lut.Remove(key);
            SaveDict();
#endif
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

        private bool ShouldTranslateString(string jp)
        {
            // Any strings that match only ASCII shouldn't need translation (probably an internal name).
            return jp.Any(c => c > 0x7F);
        }

        public string Translate(string jp, JORControl context)
        {
            // might need context at some point if we want to deal with ex labels that have wide-ranging %d params
            return Translate(jp);
        }

        public string Translate(string jp)
        {
            if (!ShouldTranslateString(jp))
                return jp;

            if (lut.TryGetValue(jp, out string en))
            {
                return en ?? jp;
            }
            else
            {
                EnsureKey(jp);

                saveDebouncer.Bounce();

                return jp;
            }
        }

        public void EnsureKey(string jp)
        {
            if (!ShouldTranslateString(jp))
                return;

            lut.TryAdd(jp, null);
        }
    }
}

class Debouncer
{
    private System.Timers.Timer timer;
    private double timeout_ms;

    public Debouncer(double timeout_ms, ElapsedEventHandler action)
    {
        this.timeout_ms = timeout_ms;
        timer = new System.Timers.Timer(timeout_ms);
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
