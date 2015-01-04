#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright © 2007-2015 ShareX Developers

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using ShareX.Properties;
using ShareX.UploadersLib;
using SingleInstanceApplication;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Autofac;

namespace ShareX
{
    internal static class Program
    {
        public static bool IsBeta = false;

        public static string Title
        {
            get
            {
                Version version = Version.Parse(Application.ProductVersion);
                string title = string.Format("ShareX {0}.{1}", version.Major, version.Minor);
                if (version.Build > 0) title += "." + version.Build;
                if (IsPortable) title += " Portable";
                if (IsBeta) title += " Beta";
                return title;
            }
        }

        public static CLIManager CLI { get; private set; }
        public static bool IsMultiInstance { get; private set; }
        public static bool IsPortable { get; private set; }
        public static bool IsSilentRun { get; private set; }
        public static bool IsSandbox { get; private set; }

        public static ApplicationConfig Settings { get; private set; }
        public static TaskSettings DefaultTaskSettings { get; private set; }
        public static UploadersConfig UploadersConfig { get; private set; }
        public static HotkeysConfig HotkeysConfig { get; private set; }

        public static ManualResetEvent UploaderSettingsResetEvent { get; private set; }
        public static ManualResetEvent HotkeySettingsResetEvent { get; private set; }

        public static MainForm MainForm { get; private set; }
        public static Stopwatch StartTimer { get; private set; }
        public static HotkeyManager HotkeyManager { get; set; }
        public static WatchFolderManager WatchFolderManager { get; set; }

        public static ILifetimeScope Container { get; private set; }

        private static IApplicationPaths applicationPaths;

        #region Paths

        [Obsolete("Use IApplicationPaths")]        
        public static string DefaultPersonalPath 
        { 
            get
            {
                return applicationPaths.DefaultPersonalPath;
            }
        }

        private static FileSystemWatcher uploaderConfigWatcher;
        private static WatchFolderDuplicateEventTimer uploaderConfigWatcherTimer;

        [Obsolete("Use IApplicationPaths")]
        public static string PersonalPath
        {
            get
            {
                return applicationPaths.PersonalPath;
            }
        }

        [Obsolete("Use IApplicationPaths")]
        public static string ApplicationConfigFilePath
        {
            get
            {
                return applicationPaths.ApplicationConfigFilePath;
            }
        }

        [Obsolete("Use IApplicationPaths")]
        public static string HotkeysConfigFilePath
        {
            get
            {
                return applicationPaths.HotkeysConfigFilePath;
            }
        }

        [Obsolete("Use IApplicationPaths")]
        public static string HistoryFilePath
        {
            get
            {
                return applicationPaths.HistoryFilePath;
            }
        }

        [Obsolete("Use IApplicationPaths")]
        public static string LogsFilePath
        {
            get
            {
                return applicationPaths.LogsFilePath;
            }
        }

        [Obsolete("Use IApplicationPaths")]
        public static string ScreenshotsParentFolder
        {
            get
            {
                return applicationPaths.ScreenshotsParentFolder;
            }
        }

        [Obsolete("Use IApplicationPaths")]
        public static string ScreenshotsFolder
        {
            get
            {
                return applicationPaths.ScreenshotsFolder;
            }
        }

        [Obsolete("Use IApplicationPaths")]
        public static string ScreenRecorderCacheFilePath
        {
            get
            {
                return applicationPaths.ScreenRecorderCacheFilePath;
            }
        }

        [Obsolete("Use IApplicationPaths")]
        public static string ToolsFolder
        {
            get
            {
                return applicationPaths.ToolsFolder;
            }
        }

        #endregion Paths

        private static bool restarting;

        [STAThread]
        private static void Main(string[] args)
        {
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            StartTimer = Stopwatch.StartNew(); // For be able to show startup time

            CLI = new CLIManager(args);
            CLI.ParseCommands();

            if (CheckAdminTasks()) return; // If ShareX opened just for be able to execute task as Admin

            IsMultiInstance = CLI.IsCommandExist("multi", "m");

            if (IsMultiInstance || ApplicationInstanceManager.CreateSingleInstance(SingleInstanceCallback, args))
            {
                using (Mutex mutex = new Mutex(false, "82E6AC09-0FEF-4390-AD9F-0DD3F5561EFC")) // Required for installer
                {
                    Run();
                }

                if (restarting)
                {
                    Process.Start(Application.ExecutablePath);
                }
            }
        }

        private static void Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Container = BuildContainer();
            applicationPaths = Container.Resolve<IApplicationPaths>();

            IsSilentRun = CLI.IsCommandExist("silent", "s");
            IsSandbox = CLI.IsCommandExist("sandbox");

            if (!IsSandbox)
            {
                IsPortable = CLI.IsCommandExist("portable", "p");

                if (IsPortable)
                {
                    applicationPaths.CustomPersonalPath = applicationPaths.PortablePersonalPath;
                }
                else
                {
                    CheckPersonalPathConfig();
                }

                if (!Directory.Exists(applicationPaths.CustomPersonalPath))
                {
                    try
                    {
                        Directory.CreateDirectory(applicationPaths.CustomPersonalPath);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(Resources.Program_Run_Unable_to_create_folder_ + string.Format(" \"{0}\"\r\n\r\n{1}", applicationPaths.PersonalPath, e),
                            "ShareX - " + Resources.Program_Run_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        applicationPaths.CustomPersonalPath = "";
                    }
                }
            }

            DebugHelper.WriteLine("{0} started", Title);
            DebugHelper.WriteLine("Operating system: " + Environment.OSVersion.VersionString);
            DebugHelper.WriteLine("Command line: " + Environment.CommandLine);
            DebugHelper.WriteLine("Personal path: " + applicationPaths.PersonalPath);

            string gitHash = GetGitHash();
            if (!string.IsNullOrEmpty(gitHash))
            {
                DebugHelper.WriteLine("Git: https://github.com/ShareX/ShareX/tree/" + gitHash);
            }

            LoadProgramSettings();

            UploaderSettingsResetEvent = new ManualResetEvent(false);
            HotkeySettingsResetEvent = new ManualResetEvent(false);
            TaskEx.Run(LoadSettings);

            LanguageHelper.ChangeLanguage(Settings.Language);

            DebugHelper.WriteLine("MainForm init started");
            MainForm = Container.Resolve<MainForm>();
            DebugHelper.WriteLine("MainForm init finished");

            Application.Run(MainForm);

            if (WatchFolderManager != null) WatchFolderManager.Dispose();
            SaveSettings();
            BackupSettings();

            DebugHelper.WriteLine("ShareX closing");
            DebugHelper.Logger.SaveLog(LogsFilePath);
        }

        private static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<ApplicationPaths>().As<IApplicationPaths>().SingleInstance();

            builder.RegisterType<MainForm>().AsSelf().SingleInstance();

            return builder.Build();
        }

        public static void Restart()
        {
            restarting = true;
            Application.Exit();
        }

        private static void SingleInstanceCallback(object sender, InstanceCallbackEventArgs args)
        {
            if (WaitFormLoad(5000))
            {
                Action d = () =>
                {
                    if (args.CommandLineArgs == null || args.CommandLineArgs.Length < 1)
                    {
                        if (MainForm.niTray != null && MainForm.niTray.Visible)
                        {
                            // Workaround for Windows startup tray icon bug
                            MainForm.niTray.Visible = false;
                            MainForm.niTray.Visible = true;
                        }

                        MainForm.ShowActivate();
                    }
                    else if (MainForm.Visible)
                    {
                        MainForm.ShowActivate();
                    }

                    CLIManager cli = new CLIManager(args.CommandLineArgs);
                    cli.ParseCommands();
                    MainForm.UseCommandLineArgs(cli.Commands);
                };

                MainForm.InvokeSafe(d);
            }
        }

        private static bool WaitFormLoad(int wait)
        {
            Stopwatch timer = Stopwatch.StartNew();

            while (timer.ElapsedMilliseconds < wait)
            {
                if (MainForm != null && MainForm.IsReady) return true;

                Thread.Sleep(10);
            }

            return false;
        }

        public static void LoadSettings()
        {
            LoadUploadersConfig();
            UploaderSettingsResetEvent.Set();
            LoadHotkeySettings();
            HotkeySettingsResetEvent.Set();

            ConfigureUploadersConfigWatcher();
        }

        public static void LoadProgramSettings()
        {
            Settings = ApplicationConfig.Load(applicationPaths.ApplicationConfigFilePath);
            DefaultTaskSettings = Settings.DefaultTaskSettings;
        }

        public static void LoadUploadersConfig()
        {
            UploadersConfig = UploadersConfig.Load(applicationPaths.UploadersConfigFilePath);
        }

        public static void LoadHotkeySettings()
        {
            HotkeysConfig = HotkeysConfig.Load(applicationPaths.HotkeysConfigFilePath);
        }

        public static void SaveSettings()
        {
            if (Settings != null) Settings.Save(applicationPaths.ApplicationConfigFilePath);
            if (UploadersConfig != null) UploadersConfig.Save(applicationPaths.UploadersConfigFilePath);
            if (HotkeysConfig != null) HotkeysConfig.Save(applicationPaths.HotkeysConfigFilePath);
        }

        public static void BackupSettings()
        {
            Helpers.BackupFileWeekly(applicationPaths.ApplicationConfigFilePath, applicationPaths.BackupFolder);
            Helpers.BackupFileWeekly(applicationPaths.HotkeysConfigFilePath, applicationPaths.BackupFolder);
            Helpers.BackupFileWeekly(applicationPaths.UploadersConfigFilePath, applicationPaths.BackupFolder);
            Helpers.BackupFileWeekly(applicationPaths.HistoryFilePath, applicationPaths.BackupFolder);
        }

        private static void CheckPersonalPathConfig()
        {
            string customPersonalPath = ReadPersonalPathConfig();

            if (!string.IsNullOrEmpty(customPersonalPath))
            {
                applicationPaths.CustomPersonalPath = Helpers.GetAbsolutePath(customPersonalPath);

                if (applicationPaths.CustomPersonalPath.Equals(applicationPaths.PortablePersonalPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    IsPortable = true;
                }
            }
        }

        public static string ReadPersonalPathConfig()
        {
            if (File.Exists(applicationPaths.PersonalPathConfig))
            {
                return File.ReadAllText(applicationPaths.PersonalPathConfig, Encoding.UTF8).Trim();
            }

            return string.Empty;
        }

        public static void WritePersonalPathConfig(string path)
        {
            if (path == null)
            {
                path = string.Empty;
            }
            else
            {
                path = path.Trim();
            }

            bool isDefaultPath = string.IsNullOrEmpty(path) && !File.Exists(applicationPaths.PersonalPathConfig);

            if (!isDefaultPath)
            {
                string currentPath = ReadPersonalPathConfig();

                if (!path.Equals(currentPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        File.WriteAllText(applicationPaths.PersonalPathConfig, path, Encoding.UTF8);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        MessageBox.Show(string.Format(Resources.Program_WritePersonalPathConfig_Cant_access_to_file, applicationPaths.PersonalPathConfig),
                            "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            OnError(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            OnError((Exception)e.ExceptionObject);
        }

        private static void OnError(Exception e)
        {
            using (ErrorForm errorForm = new ErrorForm(e, LogsFilePath, Links.URL_ISSUES))
            {
                errorForm.ShowDialog();
            }
        }

        public static void ConfigureUploadersConfigWatcher()
        {
            if (Settings.DetectUploaderConfigFileChanges && uploaderConfigWatcher == null)
            {
                uploaderConfigWatcher = new FileSystemWatcher(Path.GetDirectoryName(applicationPaths.UploadersConfigFilePath), Path.GetFileName(applicationPaths.UploadersConfigFilePath));
                uploaderConfigWatcher.Changed += uploaderConfigWatcher_Changed;
                uploaderConfigWatcherTimer = new WatchFolderDuplicateEventTimer(applicationPaths.UploadersConfigFilePath);
                uploaderConfigWatcher.EnableRaisingEvents = true;
            }
            else if (uploaderConfigWatcher != null)
            {
                uploaderConfigWatcher.Dispose();
                uploaderConfigWatcher = null;
            }
        }

        private static void uploaderConfigWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (!uploaderConfigWatcherTimer.IsDuplicateEvent(e.FullPath))
            {
                Action onCompleted = () => ReloadUploadersConfig(e.FullPath);
                Helpers.WaitWhileAsync(() => Helpers.IsFileLocked(e.FullPath), 250, 5000, onCompleted, 1000);
                uploaderConfigWatcherTimer = new WatchFolderDuplicateEventTimer(e.FullPath);
            }
        }

        private static void ReloadUploadersConfig(string filePath)
        {
            UploadersConfig = UploadersConfig.Load(filePath);
        }

        public static void UploadersConfigSaveAsync()
        {
            if (uploaderConfigWatcher != null) uploaderConfigWatcher.EnableRaisingEvents = false;

            TaskEx.Run(() =>
            {
                UploadersConfig.Save(applicationPaths.UploadersConfigFilePath);
            },
            () =>
            {
                if (uploaderConfigWatcher != null) uploaderConfigWatcher.EnableRaisingEvents = true;
            });
        }

        public static string GetGitHash()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ShareX.GitHash.txt"))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadLine();
            }
        }

        private static bool CheckAdminTasks()
        {
            if (CLI.IsCommandExist("dnschanger"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new DNSChangerForm());
                return true;
            }

            return false;
        }
    }
}