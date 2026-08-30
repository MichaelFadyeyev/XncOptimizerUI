using System.IO;
using System.Text.Json;
using XncOptimizerUI.Configuration;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Services
{
    public class ConfigService : IConfigService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        private readonly string _path;

        private AppOptions _options;

        public ConfigService() : this(GetDefaultPath())
        {
        }

        /// <summary>
        /// Test seam: lets a test point the service at a temporary file
        /// instead of the real per-user configuration.
        /// </summary>
        public ConfigService(string configFilePath)
        {
            _path = configFilePath;
            _options = LoadConfiguration();
        }

        private static string GetDefaultPath()
        {
            var appConfigFullPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            return Path.Combine(appConfigFullPath, "XncOptimizerUI", "configuration.json");
        }

        private AppOptions LoadConfiguration()
        {
            if (!File.Exists(_path))
            {
                return InitOptions();
            }

            try
            {
                string loadedJson = File.ReadAllText(_path);

                return JsonSerializer.Deserialize<AppOptions>(loadedJson) ?? InitOptions();
            }
            catch (JsonException)
            {
                // A malformed file used to surface as a TypeInitializationException
                // thrown out of the static constructor. Fall back to defaults instead.
                return InitOptions();
            }
        }

        private AppOptions InitOptions()
        {
            _options = new AppOptions();
            SaveOptions();

            return _options;
        }

        private void SaveOptions()
        {
            var appConfigDirectory = Path.GetDirectoryName(_path);

            if (!string.IsNullOrEmpty(appConfigDirectory) && !Directory.Exists(appConfigDirectory))
            {
                Directory.CreateDirectory(appConfigDirectory);
            }

            var json = JsonSerializer.Serialize(_options, _jsonOptions);

            File.WriteAllText(_path, json);
        }

        public decimal SawWidth
        {
            get { return _options.SawWidth; }
        }

        public IReadOnlyList<string> LabelsToProcess
        {
            get { return _options.LabelsToProcess; }
        }

        public void AddLabelToProcess(string newLabel)
        {
            _options.LabelsToProcess.Add(newLabel);
            _options.LastLabelToProcessSelectedIndex = _options.LabelsToProcess.Count - 1;
            SaveOptions();
        }

        public void DeleteLabelToProcess(string labelToRemove)
        {
            if (_options.LabelsToProcess.Count == 1) return;

            _options.LabelsToProcess.Remove(labelToRemove);
            _options.LastLabelToProcessSelectedIndex = 0;
            SaveOptions();
        }

        public void UpdateSawWidth(decimal newWidth)
        {
            _options.SawWidth = newWidth;
            SaveOptions();
        }

        public void UpdateLastLabelToProcessSelectedIndex(string label)
        {
            var index = _options.LabelsToProcess.IndexOf(label);

            // WPF resets SelectedItem to null when it is not present in ItemsSource,
            // which would otherwise persist -1 here and make the next launch throw
            // out of GetLastLabelToProcessSelected().
            if (index < 0) return;

            _options.LastLabelToProcessSelectedIndex = index;
            SaveOptions();
        }

        public string GetLastLabelToProcessSelected()
        {
            return _options.LabelsToProcess[_options.LastLabelToProcessSelectedIndex];
        }
    }
}
