using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using ShareX.HelpersLib;

namespace ShareX
{
    public interface IApplicationPaths
    {
        string CustomPersonalPath { get; set; }
        string PersonalPath { get; }
        string ApplicationConfigFilePath { get; }
        string UploadersConfigFilePath { get; }
        string HotkeysConfigFilePath { get; }
        string HistoryFilePath { get; }
        string LogsFilePath { get; }
        string ScreenshotsParentFolder { get; }
        string ScreenshotsFolder { get; }
        string ScreenRecorderCacheFilePath { get; }
        string ToolsFolder { get; }
        string DefaultPersonalPath { get; }
        string BackupFolder { get; }
        string UploadersConfigFolder { get; }
        string HotkeysConfigFolder { get; }
        string LogsFolder { get; }
    }

    [Localizable(false)]
    public class ApplicationPaths : IApplicationPaths
    {
        private static readonly string StartupPath = Application.StartupPath;

        private readonly string defaultPersonalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShareX");
        private readonly string portablePersonalPath = Path.Combine(StartupPath, "ShareX");
        private readonly string personalPathConfig = Path.Combine(StartupPath, "PersonalPath.cfg");
        
        private readonly string ApplicationConfigFilename = "ApplicationConfig.json";
        private readonly string UploadersConfigFilename = "UploadersConfig.json";
        private readonly string HotkeysConfigFilename = "HotkeysConfig.json";
        private readonly string HistoryFilename = "History.xml";
        private readonly string LogFileName = "ShareX-Log-{0:yyyy-MM}.txt";

        public string CustomPersonalPath { get; set; }

        public string PersonalPath
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPersonalPath))
                {
                    return CustomPersonalPath;
                }

                return defaultPersonalPath;
            }
        }

        public string ApplicationConfigFilePath
        {
            get
            {
                if (!Program.IsSandbox)
                {
                    return Path.Combine(PersonalPath, ApplicationConfigFilename);
                }

                return null;
            }
        }

        public string UploadersConfigFolder
        {
            get
            {
                if (Program.Settings != null && !string.IsNullOrEmpty(Program.Settings.CustomUploadersConfigPath))
                {
                    return Program.Settings.CustomUploadersConfigPath;
                }

                return PersonalPath;
            }
        }

        public string UploadersConfigFilePath
        {
            get
            {
                if (!Program.IsSandbox)
                {
                    return Path.Combine(UploadersConfigFolder, UploadersConfigFilename);
                }

                return null;
            }
        }

        public string HotkeysConfigFolder
        {
            get
            {
                if (Program.Settings != null && !string.IsNullOrEmpty(Program.Settings.CustomHotkeysConfigPath))
                {
                    return Program.Settings.CustomHotkeysConfigPath;
                }

                return PersonalPath;
            }
        }

        public string HotkeysConfigFilePath
        {
            get
            {
                if (!Program.IsSandbox)
                {
                    return Path.Combine(HotkeysConfigFolder, HotkeysConfigFilename);
                }

                return null;
            }
        }

        public string HistoryFilePath
        {
            get
            {
                if (!Program.IsSandbox)
                {
                    return Path.Combine(PersonalPath, HistoryFilename);
                }

                return null;
            }
        }

        public string LogsFolder
        {
            get
            {
                return Path.Combine(PersonalPath, "Logs");
            }
        }

        public string LogsFilePath
        {
            get
            {
                string filename = string.Format(LogFileName, FastDateTime.Now);
                return Path.Combine(LogsFolder, filename);
            }
        }

        public string ScreenshotsParentFolder
        {
            get
            {
                if (Program.Settings != null && Program.Settings.UseCustomScreenshotsPath && !string.IsNullOrEmpty(Program.Settings.CustomScreenshotsPath))
                {
                    return Program.Settings.CustomScreenshotsPath;
                }

                return Path.Combine(PersonalPath, "Screenshots");
            }
        }

        public string ScreenshotsFolder
        {
            get
            {
                string subFolderName = NameParser.Parse(NameParserType.FolderPath, Program.Settings.SaveImageSubFolderPattern);
                return Path.Combine(ScreenshotsParentFolder, subFolderName);
            }
        }

        public string ScreenRecorderCacheFilePath
        {
            get
            {
                return Path.Combine(PersonalPath, "ScreenRecorder.avi");
            }
        }

        public string BackupFolder
        {
            get
            {
                return Path.Combine(PersonalPath, "Backup");
            }
        }

        public string ToolsFolder
        {
            get
            {
                return Path.Combine(PersonalPath, "Tools");
            }
        }
        public string DefaultPersonalPath
        {
            get
            {
                return defaultPersonalPath;
            }
        }
        public string PortablePersonalPath
        {
            get
            {
                return portablePersonalPath;
            }
        }
        public string PersonalPathConfig
        {
            get
            {
                return personalPathConfig;
            }
        }
    }
}