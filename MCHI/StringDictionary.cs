using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Timers;
using System.Threading;
using System.Linq;
using DeepL;
using System.Threading.Tasks;

namespace MCHI
{
    class StringDictionary
    {
        private Dictionary<string, string> translations = new Dictionary<string, string>();
        private string dictPath;
        private FileSystemWatcher fileWatcher;
        private Debouncer saveDebouncer;
        private DeepLClient DeepLClient;

        public StringDictionary(string dictPath)
        {
            string API_KEY_FILE = "../../../deepl_api_key.txt";
            if (File.Exists(API_KEY_FILE))
            {
                string apiKey = File.ReadAllText(API_KEY_FILE).Trim();
                DeepLClient = new DeepLClient(apiKey);
            }

            this.dictPath = dictPath;
            saveDebouncer = new Debouncer(200 /* ms */, (Object src, ElapsedEventArgs e) => SaveDict());

            Reload();

            fileWatcher = new FileSystemWatcher();
            fileWatcher.Path = Path.GetDirectoryName(dictPath);
            fileWatcher.Filter = Path.GetFileName(dictPath);
            fileWatcher.Changed += OnDictFileUpdated;

            fileWatcher.EnableRaisingEvents = true;
        }

        private async Task CloudTranslateAsync()
        {
            // Go through our dictionary looking for potential strings to translate.
            var strings = new List<string>();
            foreach (var jp in translations.Keys)
                if (ShouldTranslateString(jp) && translations[jp] == null)
                    strings.Add(jp);
            if (strings.Count == 0)
                return;
            var result = (await DeepLClient.TranslateAsync(strings, Language.Japanese, Language.English, Splitting.None, true)).ToList();
            for (var i = 0; i < strings.Count; i++)
                translations[strings[i]] = result[i].Text;
            SaveDict();
        }

        private bool IsTranslating = false;

        public async void CloudTranslate()
        {
            if (IsTranslating)
                return;

            IsTranslating = true;
            await CloudTranslateAsync();
            IsTranslating = false;
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
                translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            else
            {
                translations.Clear();
            }

#if true
            // purge ASCII keys from dict
            bool removedAny = false;
            foreach (var key in translations.Keys)
            {
                if (!ShouldStoreInTranslationDictionary(key))
                {
                    translations.Remove(key);
                    removedAny = true;
                }
            }

            if (removedAny)
                SaveDict();
#endif
        }

        public void SaveDict()
        {
            using (var fs = File.Open(dictPath, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(
                    new Utf8JsonWriter(fs, new JsonWriterOptions
                    {
                        Indented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }),
                    translations);
            }

            saveDebouncer.Done();
        }

        private string[] specialTranslationPrefixes = new string[] {
            "水平距離", // Horizontal Distance
            "最頂点", // Apex
        };

        private string GetSpecialTranslation(string jp)
        {
            foreach (var prefix in specialTranslationPrefixes)
            {
                if (jp.StartsWith(prefix) && prefix != jp)
                {
                    var rest = jp.Substring(prefix.Length);
                    return translations[prefix] + rest;
                }
            }

            return null;
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

            var specialTranslation = GetSpecialTranslation(jp);
            if (specialTranslation != null)
                return specialTranslation;

            if (translations.TryGetValue(jp, out string en))
            {
                return en ?? jp;
            }
            else
            {
                if (EnsureKey(jp))
                    saveDebouncer.Bounce();

                return jp;
            }
        }

        private bool ShouldStoreInTranslationDictionary(string jp)
        {
            if (GetSpecialTranslation(jp) != null)
                return false;

            return ShouldTranslateString(jp);
        }

        public bool EnsureKey(string jp)
        {
            if (!ShouldStoreInTranslationDictionary(jp))
                return false;

            translations.TryAdd(jp, null);
            return true;
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

    public void Done()
    {
        timer.Stop();
    }
}
