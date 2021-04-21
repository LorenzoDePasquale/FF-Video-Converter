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
using System.Windows.Media;
using System.Windows.Media.Animation;


namespace FFVideoConverter
{
    public partial class UpdaterWindow : Window
    {
        private static HttpClient httpClient = new HttpClient();
        private string downloadUrl;

        public UpdaterWindow()
        {
            InitializeComponent();

            Height -= 30;
        }

        public static async Task<bool> UpdateAvaiable()
        {
            Version latestVersion, currentVersion;
            try
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "LorenzoDePasquale");
                using Stream stream = await httpClient.GetStreamAsync("https://api.github.com/repos/lorenzodepasquale/FF-Video-Converter/releases").ConfigureAwait(false);
                using JsonDocument document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                latestVersion = Version.Parse(document.RootElement[0].GetProperty("tag_name").GetString()); //Releases are sorted by most recent
            }
            catch (Exception)
            {
                return false;
            }
            System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
            currentVersion = new Version(v.Major, v.Minor, v.Build);
            return currentVersion < latestVersion;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
            labelledTextBlockCurrentVersion.Text = $"v{v.Major}.{v.Minor}";
            if (v.Build > 0) labelledTextBlockCurrentVersion.Text += $".{v.Build}";
            labelledTextBlockNewVersion.Text = $"Searching for new versions...";

            try
            {
                using Stream stream = await httpClient.GetStreamAsync("https://api.github.com/repos/lorenzodepasquale/FF-Video-Converter/releases");
                using JsonDocument document = await JsonDocument.ParseAsync(stream);
                Version newVersion = Version.Parse(document.RootElement[0].GetProperty("tag_name").GetString()); //Releases are sorted by most recent
                DateTime date = DateTime.Parse(document.RootElement[0].GetProperty("published_at").GetString());
                labelledTextBlockNewVersion.Text = $"v{newVersion.Major}.{newVersion.Minor}";
                if (newVersion.Build > 0) labelledTextBlockNewVersion.Text += $".{newVersion.Build}";
                labelledTextBlockNewVersion.Text += $"  ({date:dd/MM/yyyy})";
                downloadUrl = document.RootElement[0].GetProperty("assets")[0].GetProperty("browser_download_url").GetString();

                for (int i = 0; i < Math.Min(document.RootElement.GetArrayLength(), 10); i++)
                {
                    date = DateTime.Parse(document.RootElement[i].GetProperty("published_at").GetString());
                    textBlockPatchNotes.Inlines.Add(new Run($"{document.RootElement[i].GetProperty("tag_name").GetString()} ({date.ToString("dd/MM/yyyy")}):\n") { Foreground = new BrushConverter().ConvertFromString("#FF2669DE") as SolidColorBrush });
                    textBlockPatchNotes.Inlines.Add(new Run(document.RootElement[i].GetProperty("body").GetString() + "\n\n"));
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

        private void UpdateProgress((float percentage, long currentBytes, long totalBytes) progress)
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
                InstallUpdate();
            }
        }

        private async void InstallUpdate()
        {
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