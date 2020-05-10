using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;


namespace FFVideoConverter
{
    public partial class UpdaterWindow : Window
    {
        private readonly struct Version
        {
            public int Major { get; }
            public int Minor { get; }
            public int Build { get; }

            public Version(int major, int minor, int build = 0)
            {
                Major = major;
                Minor = minor;
                Build = build;
            }

            public static Version Parse(string version)  //format: v0.1-beta or v1.2 or v1.2.1
            {
                version = version.Substring(1);
                if (version.Contains("-"))
                {
                    version = version.Split('-')[0];
                }
                string[] versionNumbers = version.Split('.');
                return new Version(Convert.ToInt32(versionNumbers[0]), Convert.ToInt32(versionNumbers[1]), versionNumbers.Length > 2 ? Convert.ToInt32(versionNumbers[2]) : 0);
            }

            public static bool operator >(Version v1, Version v2)
            {
                if (v1.Major > v2.Major)
                {
                    return true;
                }
                else if (v1.Major == v2.Major && v1.Minor > v2.Minor)
                {
                    return true;
                }
                else if (v1.Minor == v2.Minor && v1.Build > v2.Build)
                {
                    return true;
                }
                return false;
            }

            public static bool operator <(Version v1, Version v2)
            {
                if (v1.Major < v2.Major)
                {
                    return true;
                }
                else if (v1.Major == v2.Major && v1.Minor < v2.Minor)
                {
                    return true;
                }
                else if (v1.Minor == v2.Minor && v1.Build < v2.Build)
                {
                    return true;
                }
                return false;
            }
        }

        private static readonly HttpClient httpClient = new HttpClient();
        private string downloadUrl;

        public UpdaterWindow()
        {
            InitializeComponent();

            Height -= 30;
        }

        #region "Title Bar controls"

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        public static async Task<bool> UpdateAvaiable()
        {
            Version latestVersion, currentVersion;
            try
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "LorenzoDePasquale");
                using (Stream stream = await httpClient.GetStreamAsync("https://api.github.com/repos/lorenzodepasquale/FF-Video-Converter/releases").ConfigureAwait(false))
                using (JsonDocument document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false))
                {
                    latestVersion = Version.Parse(document.RootElement[0].GetProperty("tag_name").GetString()); //Releases are sorted by most recent
                }
            }
            catch (Exception)
            {
                return false; //If update check fails, return as there was no update; no point in bothering the user
            }
            System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
            currentVersion = new Version(v.Major, v.Minor, v.Build);
            return currentVersion < latestVersion;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
            textBlockCurrentVersion.Text = $"v{v.Major}.{v.Minor}";
            if (v.Build > 0) textBlockCurrentVersion.Text += $".{v.Build}";
            textBlockNewVersion.Text = $"Searching for new versions...";

            try
            {
                using (Stream stream = await httpClient.GetStreamAsync("https://api.github.com/repos/lorenzodepasquale/FF-Video-Converter/releases"))
                using (JsonDocument document = await JsonDocument.ParseAsync(stream))
                {
                    Version newVersion = Version.Parse(document.RootElement[0].GetProperty("tag_name").GetString()); //Releases are sorted by most recent
                    DateTime date = DateTime.Parse(document.RootElement[0].GetProperty("published_at").GetString());
                    textBlockNewVersion.Text = $"v{newVersion.Major}.{newVersion.Minor}";
                    if (newVersion.Build > 0) textBlockNewVersion.Text += $".{newVersion.Build}";
                    textBlockNewVersion.Text += $"  ({date:dd/MM/yyyy})";
                    downloadUrl = document.RootElement[0].GetProperty("assets")[0].GetProperty("browser_download_url").GetString();

                    for (int i = 0; i < Math.Min(document.RootElement.GetArrayLength(), 10); i++)
                    {
                        date = DateTime.Parse(document.RootElement[i].GetProperty("published_at").GetString());
                        textBlockPatchNotes.Inlines.Add(new Run($"{document.RootElement[i].GetProperty("tag_name").GetString()} ({date.ToString("dd/MM/yyyy")}):\n") { Foreground = new BrushConverter().ConvertFromString("#FF2669DE") as SolidColorBrush });
                        textBlockPatchNotes.Inlines.Add(new Run(document.RootElement[i].GetProperty("body").GetString() + "\n\n"));
                    }
                }
            }
            catch (Exception)
            {
                new MessageBoxWindow("Failed to connect to Github servers", "Error").ShowDialog();
                Close();
            }
        }

        private async void ButtonUpdate_Click(object sender, RoutedEventArgs e)
        {
            buttonUpdate.IsEnabled = false;
            textBlockUpdateProgress.Text = "Starting download...";
            try
            {
                using (FileStream fileStream = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "update.zip", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await httpClient.DownloadAsync(downloadUrl, fileStream, new Progress<(float, long, long)>(UpdateProgress));
                }
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException)
                {
                    new MessageBoxWindow("Can't write to current directory; make sure this application has write access to it's directory", "Failed to write to Disk").ShowDialog();
                    Close();
                }
                else
                {
                    new MessageBoxWindow(ex.Message, "Failed to download update").ShowDialog();
                    Close();
                }
            }
        }

        private async void UpdateProgress((float percentage, long currentBytes, long totalBytes) progress)
        {
            DoubleAnimation progressAnimation = new DoubleAnimation(progress.percentage * 100, TimeSpan.FromSeconds(0.5));
            progressBarUpdateProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
            if (progress.percentage < 1)
            {
                textBlockUpdateProgress.Text = $"Downloading update: {progress.currentBytes.ToBytesString()} / {progress.totalBytes.ToBytesString()} ({(progress.percentage * 100).ToString("F0")}%)";
            }
            else
            {
                progressBarUpdateProgress.Value = 99.4f; //To avoid progress bar turning green
                textBlockUpdateProgress.Text = "Extracting update...";
                try
                {
                    await Task.Run(() =>
                    {
                        File.Move(AppDomain.CurrentDomain.BaseDirectory + "FFVideoConverter.exe", AppDomain.CurrentDomain.BaseDirectory + "FFVideoConverterOld.exe");
                        ZipFile.ExtractToDirectory(AppDomain.CurrentDomain.BaseDirectory + "update.zip", AppDomain.CurrentDomain.BaseDirectory + "update");
                        foreach (string file in Directory.EnumerateFiles(AppDomain.CurrentDomain.BaseDirectory + "update"))
                        {
                            File.Copy(file, AppDomain.CurrentDomain.BaseDirectory + Path.GetFileName(file), true);
                        }
                    });
                    //Restart the application
                    Application.Current.Shutdown();
                    Process.Start(AppDomain.CurrentDomain.BaseDirectory + "FFVideoConverter.exe");
                }
                catch (Exception ex)
                {
                    new MessageBoxWindow("Couldn't install downloaded update; try updating manually.\n\nError message:\n" + ex.Message, "Error").ShowDialog();
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "FFVideoConverterOld.exe"))
                    {
                        //Restores old file name in case of error
                        File.Move(AppDomain.CurrentDomain.BaseDirectory + "FFVideoConverterOld.exe", AppDomain.CurrentDomain.BaseDirectory + "FFVideoConverter.exe");
                    }
                    Close();
                }
            }
        }
    }
}