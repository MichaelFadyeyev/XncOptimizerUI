
using XncOptimizerUI.Configuration;
using System.Text.Json;
using System.IO;
using System.ComponentModel.DataAnnotations;


namespace XncOptimizerUI.Services
{
    public static class ConfigService
    {

        private static AppOptions? _options;

        private static string? _path;

        private static JsonSerializerOptions? _jsonOptions;

        static ConfigService()
        {
            LoadConfiguration();
        }

        public static void LoadConfiguration()
        {
            var appConfigFullPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _path = Path.Combine(appConfigFullPath, "XncOptimizerUI", "configuration.json");
            _jsonOptions = new() { WriteIndented = true };

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
            var appConfigDirectory = Path.GetDirectoryName(_path ?? "XncOptimizerUI");

            if (!Directory.Exists(appConfigDirectory!))
            {
                Directory.CreateDirectory(appConfigDirectory!);
            }

            File.WriteAllText(_path ?? "configuration.json", json);

            return defaultOptions;
        }

        public static decimal SawWidth
        {
            get { return _options!.SawWidth; }
        }

        public static List<string> LabelsToProcess
        {
            get { return _options!.LabelsToProcess; }
        }

        private static void SaveOptions()
        {
            var json = JsonSerializer.Serialize(_options, _jsonOptions);
            File.WriteAllText(_path!, json);
        }

        public static void AddLabelToProcess(string newLabel)
        {
            _options!.LabelsToProcess.Add(newLabel);
            _options.LastLabelToProcessSelectedIndex = _options.LabelsToProcess.Count - 1;
            SaveOptions();

        }

        public static void DeleteLabelToProcess(string labelToRemove)
        {
            if (_options!.LabelsToProcess.Count == 1) return;

            _options!.LabelsToProcess.Remove(labelToRemove);
            _options.LastLabelToProcessSelectedIndex = 0;
            SaveOptions();
        }

        public static void UpdateSawWidth(decimal newWidth)
        {
            _options!.SawWidth = newWidth;
            SaveOptions();
        }

        public static void UpdateLastLabelToProcessSelectedIndex(string label)
        {
            _options!.LastLabelToProcessSelectedIndex = _options.LabelsToProcess.IndexOf(label);
            SaveOptions();

        }

        public static string GetLastLabelToProcessSelected()
        {
            return _options!.LabelsToProcess[_options.LastLabelToProcessSelectedIndex];
        }
    }
}
