
using XncOptimizerUI.Configuration;
using System.Text.Json;
using System.IO;


namespace XncOptimizerUI.Services
{
    public static class ConfigService
    {
        private static AppOptions _options;
        private static readonly string _path;
        private static JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        static ConfigService()
        {
            var appFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            _path = Path.Combine(Path.GetDirectoryName(appFilePath)!, "configuration.json");

            if (File.Exists(_path))
            {
                string loadedJson = File.ReadAllText(_path);
                _options = JsonSerializer.Deserialize<AppOptions>(loadedJson) ?? InitOptions();
            }
            else
            {
                _options = InitOptions();
            }
        }

        private static AppOptions InitOptions()
        {
            var defaultOptions = new AppOptions();

            var json = JsonSerializer.Serialize(defaultOptions, _jsonOptions);
            File.WriteAllText(_path, json);

            return defaultOptions;
        }

        public static decimal SawWidth
        {
            get { return _options.SawWidth; }
        }

        public static List<string> LabelsToProcess
        {
            get { return _options.LabelsToProcess; }
        }

        private static void SaveOptions()
        {
            var json = JsonSerializer.Serialize(_options, _jsonOptions);
            File.WriteAllText(_path, json);
        }


        public static void AddLabelToProcess(string newLabel)
        {
            if (File.Exists(_path))
            {
                _options.LabelsToProcess.Add(newLabel);
                _options.LastLabelToProcessSelectedIndex = _options.LabelsToProcess.Count - 1;
                SaveOptions();
            }
        }

        public static void DeleteLabelToProcess(string labelToRemove)
        {
            if (File.Exists(_path))
            {
                _options.LabelsToProcess.Remove(labelToRemove);
                _options.LastLabelToProcessSelectedIndex = 0;
                SaveOptions();
            }
        }

        public static void UpdateSawWidth(decimal newWidth)
        {
            if (File.Exists(_path))
            {
                _options.SawWidth = newWidth;
                SaveOptions();
            }
        }

        public static void UpdateLastLabelToProcessSelectedIndex(string label)
        {
            if (File.Exists(_path))
            {
                _options.LastLabelToProcessSelectedIndex = _options.LabelsToProcess.IndexOf(label);
                SaveOptions();
            }
        }

        public static string GetLastLabelToProcessSelected()
        {
            return _options.LabelsToProcess[_options.LastLabelToProcessSelectedIndex];
        }
    }
}
