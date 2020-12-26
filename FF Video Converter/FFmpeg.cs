using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace FFVideoConverter
{
    public class FFmpegEngine
    {
        public delegate void ConversionEventHandler(ProgressData progressData);
        public event ConversionEventHandler ProgressChanged;
        public event ConversionEventHandler ConversionCompleted;

        public long PrivateWorkingSet
        {
            get
            {
                try
                {
                    return (long)memoryCounter.NextValue();
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        private readonly Process convertProcess = new Process();
        private readonly PerformanceCounter memoryCounter = new PerformanceCounter();
        private ProgressData progressData, previousProgressData;
        private int i = 0;
        private bool stopped = false, partialProgress = false;
        private SynchronizationContext synchronizationContext;
        private string errorLine;

        public FFmpegEngine()
        {
            convertProcess.StartInfo.FileName = "ffmpeg";
            convertProcess.StartInfo.RedirectStandardOutput = true;
            convertProcess.StartInfo.RedirectStandardError = true;
            convertProcess.StartInfo.UseShellExecute = false;
            convertProcess.StartInfo.CreateNoWindow = true;
            convertProcess.OutputDataReceived += ConvertProcess_OutputDataReceived;
            convertProcess.ErrorDataReceived += ConvertProcess_ErrorDataReceived;

            memoryCounter.CategoryName = "Process";
            memoryCounter.CounterName = "Working Set - Private";
            memoryCounter.InstanceName = "ffmpeg";
        }

        public async void Convert(MediaInfo sourceInfo, string outputPath, ConversionOptions conversionOptions)
        {
            progressData = new ProgressData();

            //Capture the Synchronization Context of the caller, in order to invoke the events on its original thread
            synchronizationContext = SynchronizationContext.Current;

            //Duration
            if (conversionOptions.EncodeSections.Count > 0)
            {
                progressData.TotalTime = conversionOptions.EncodeSections.TotalDuration;
            }
            else
            {
                progressData.TotalTime = sourceInfo.Duration;
            }

            progressData.EncodingMode = conversionOptions.EncodingMode;
            partialProgress = false;

            //2-pass mode
            if (conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass)
            {
                await RunConversionProcess(BuildArgumentsString(sourceInfo, outputPath, conversionOptions), false).ConfigureAwait(false);
                if (!stopped)
                {
                    progressData.EncodingMode = EncodingMode.AverageBitrate_SecondPass;
                    conversionOptions.EncodingMode = EncodingMode.AverageBitrate_SecondPass;
                    await RunConversionProcess(BuildArgumentsString(sourceInfo, outputPath, conversionOptions)).ConfigureAwait(false);
                }
                //Remove 2pass log files
                foreach (var file in Directory.GetFiles(Environment.CurrentDirectory).Where(x => x.Contains("log")))
                {
                    File.Delete(file);
                }
            }
            //Single pass mode
            else
            {
                if (conversionOptions.EncodeSections.Count == 0)
                {
                    await RunConversionProcess(BuildArgumentsString(sourceInfo, outputPath, conversionOptions)).ConfigureAwait(false);
                }
                else if (conversionOptions.EncodeSections.Count == 1)
                {
                    await RunConversionProcess(BuildArgumentsString(sourceInfo, outputPath, conversionOptions, conversionOptions.EncodeSections.Start, conversionOptions.EncodeSections.End)).ConfigureAwait(false);
                }
                else
                {
                    partialProgress = true;
                    string outputDirectory = Path.GetDirectoryName(outputPath);
                    string outputFileName = Path.GetFileNameWithoutExtension(outputPath);
                    for (int i = 0; i < conversionOptions.EncodeSections.Count; i++)
                    {
                        await RunConversionProcess(BuildArgumentsString(sourceInfo, $"{outputDirectory}\\{outputFileName}_part_{i}.mp4", conversionOptions, conversionOptions.EncodeSections[i].Start, conversionOptions.EncodeSections[i].End), false).ConfigureAwait(false);
                        if (stopped) break;
                        previousProgressData = progressData;
                        File.AppendAllText("concat.txt", $"file '{outputDirectory}\\{outputFileName}_part_{i}.mp4'\n");
                    }
                    if (!stopped) await RunConversionProcess($"-y -f concat -safe 0 -i concat.txt -c copy \"{outputPath}\"", false).ConfigureAwait(false);
                    File.Delete("concat.txt");
                    for (int i = 0; i < conversionOptions.EncodeSections.Count; i++)
                    {
                        File.Delete($"{outputDirectory}\\{outputFileName}_part_{i}.mp4");
                    }
                    if (!stopped)
                    {
                        synchronizationContext.Post(new SendOrPostCallback((o) =>
                        {
                            ConversionCompleted?.Invoke(progressData);
                        }), null);
                    }
                }
            }
        }

        public void ExtractAudioTrack(MediaInfo sourceInfo, int streamIndex, string destination, TimeSpan start, TimeSpan end)
        {
            progressData = new ProgressData() { EncodingMode = EncodingMode.NoEncoding };

            //Captures the Synchronization Context of the caller, in order to invoke the events on its original thread
            synchronizationContext = SynchronizationContext.Current;

            //Duration
            if (end != TimeSpan.Zero)
            {
                progressData.TotalTime = end - start;
            }
            else
            {
                progressData.TotalTime = sourceInfo.Duration - start;
            }

            //FFMpeg command string
            StringBuilder sb = new StringBuilder("-y");
            sb.Append($" -ss {start}");
            sb.Append($" -i \"{(sourceInfo.HasExternalAudio ? sourceInfo.ExternalAudioSource : sourceInfo.Source)}\"");
            if (end != TimeSpan.Zero) sb.Append($" -t {end - start}");
            sb.Append($" -map 0:{streamIndex}");
            sb.Append(" -vn -c:a copy");
            sb.Append($" \"{destination}\" -hide_banner");

            _ = RunConversionProcess(sb.ToString());
        }

        public string BuildArgumentsString(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions)
        {
            return BuildArgumentsString(sourceInfo, destination, conversionOptions, TimeSpan.Zero, TimeSpan.Zero);
        }

        public string BuildArgumentsString(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions, TimeSpan start, TimeSpan end)
        {
            StringBuilder sb = new StringBuilder("-y -progress -");
            bool changeVolume = false;

            //Start time
            if (start != TimeSpan.Zero) sb.Append($" -ss {start}");

            //Input path
            sb.Append($" -i \"{sourceInfo.Source}\"");

            //External audio source
            if (sourceInfo.HasExternalAudio && conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                if (start != TimeSpan.Zero) sb.Append($" -ss {start}");
                sb.Append($" -i \"{sourceInfo.ExternalAudioSource}\"");
            }

            //Duration
            if (end != TimeSpan.Zero) sb.Append($" -t {end - start}");

            //Main video track
            sb.Append($" -map 0:v:0");

            //Add or skip audio tracks
            if (!conversionOptions.SkipAudio && conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                foreach (var audioTrack in sourceInfo.AudioTracks)
                {
                    if (audioTrack.Enabled)
                    {
                        sb.Append($" -map {(sourceInfo.HasExternalAudio ? "1" : "0")}:{audioTrack.StreamIndex}");
                        sb.Append($" -disposition:{audioTrack.StreamIndex} {(audioTrack.Default ? "default" : "none")}");
                    }
                    if (audioTrack.Volume != 100) changeVolume = true;
                }
            }

            //Subtitles
            sb.Append(" -map 0:s?");

            //Encoder command line
            sb.Append(" -movflags faststart -c:v " + conversionOptions.Encoder.GetFFMpegCommand(conversionOptions.EncodingMode));

            //Framerate and filters
            if (conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                if (conversionOptions.Framerate > 0) sb.Append(" -r " + conversionOptions.Framerate);
                //Video filters
                if (conversionOptions.Resolution.HasValue() || conversionOptions.CropData.HasValue() || conversionOptions.Rotation.HasValue())
                {
                    sb.Append(" -vf " + ConcatFilters(conversionOptions.Resolution.FilterString, conversionOptions.CropData.FilterString, conversionOptions.Rotation.FilterString));
                }
                //Audio filters
                if (changeVolume)
                {
                    var volumeFilters = sourceInfo.AudioTracks.Where(x => x.Volume != 100).Select(x => "volume=" + (x.Volume / 100).ToString("0.##", CultureInfo.InvariantCulture));
                    sb.Append(" -af " + ConcatFilters(volumeFilters.ToArray()));
                }
            }

            //Audio encoder
            if (conversionOptions.SkipAudio || conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass)
            {
                sb.Append(" -an");
            }
            else if (changeVolume)
            {
                sb.Append($" -c:a aac -b:a 192k");
            }
            else
            {
                sb.Append(" -c:a copy");
            }

            //Output path
            if (conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                sb.Append($" -avoid_negative_ts 1 \"{destination}\" -hide_banner");
            }
            else
            {
                sb.Append($" -f null NUL -hide_banner");
            }

            return sb.ToString();
        }

        private async Task RunConversionProcess(string arguments, bool reportCompletition = true)
        {
            convertProcess.StartInfo.Arguments = arguments;
            convertProcess.Start();
            convertProcess.BeginOutputReadLine();
            convertProcess.BeginErrorReadLine();
            convertProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
            stopped = false;
            i = 0;

            await Task.Run(() =>
            {
                convertProcess.WaitForExit();
            }).ConfigureAwait(false);
            convertProcess.CancelOutputRead();
            convertProcess.CancelErrorRead();

            int exitCode = convertProcess.ExitCode; //0: success; -1: killed; 1: crashed
            synchronizationContext.Post(new SendOrPostCallback((o) =>
            {
                if (exitCode != -1 && exitCode != 1) //Sometimes ffmpegs exits with weird numbers even if there were no errors
                {
                    if (reportCompletition)
                        ConversionCompleted?.Invoke(progressData);
                }
                else if (!stopped)
                {
                    progressData.ErrorMessage = errorLine;
                    ConversionCompleted?.Invoke(progressData);
                }
            }), null);
        }

        private string ConcatFilters(params string[] filters)
        {
            if (filters.Length == 0) return "";

            StringBuilder sb = new StringBuilder();

            sb.Append('"');
            foreach (var filter in filters)
            {
                if (filter.Length != 0)
                {
                    sb.Append(filter);
                    sb.Append(',');
                }    
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append('"');
            return sb.ToString();
        }

        private void ConvertProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                (string key, string value) = e.Data.Split('=').ToCouple();
                if (value != "N/A")
                {
                    /* stdout from ffmpeg:
                    * frame=50
                    * fps=74.74
                    * bitrate=5180.1kbits/s
                    * total_size=1310768
                    * out_time=00:00:02.024313
                    * speed=0.989x
                    * progress=continue
                    */
                    switch (key)
                    {
                        case "fps":
                            progressData.EncodingSpeedFps = System.Convert.ToInt16(System.Convert.ToSingle(value, CultureInfo.InvariantCulture));
                            break;
                        case "bitrate":
                            float currentBitrate = System.Convert.ToSingle(value.Replace("kbits/s", ""), CultureInfo.InvariantCulture);
                            if (progressData.CurrentTime.Seconds > 5) //Skips first 5 seconds to give the encoder time to adjust it's bitrate
                            {
                                progressData.AverageBitrate += (currentBitrate - progressData.AverageBitrate) / ++i;
                            }
                            break;
                        case "total_size":
                            progressData.CurrentByteSize = System.Convert.ToInt64(value);
                            if (partialProgress) progressData.CurrentByteSize += previousProgressData.CurrentByteSize;
                            break;
                        case "out_time":
                            progressData.CurrentTime = TimeSpan.Parse(value);
                            if (partialProgress) progressData.CurrentTime += previousProgressData.CurrentTime;
                            break;
                        case "speed":
                            progressData.EncodingSpeed = System.Convert.ToSingle(value.Substring(0, value.Length - 1), CultureInfo.InvariantCulture);
                            break;
                        case "progress":
                            synchronizationContext.Post(new SendOrPostCallback((o) =>
                            {
                                ProgressChanged?.Invoke(progressData);
                            }), null);
                            break;
                    }
                }
            }
        }

        private void ConvertProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                errorLine = e.Data;
            }
        }

        public void PauseConversion()
        {
            convertProcess.Suspend();
        }

        public void ResumeConversion()
        {
            convertProcess.Resume();
        }

        public void StopConversion()
        {
            try
            {
                if (convertProcess != null && !convertProcess.HasExited)
                {
                    stopped = true;
                    convertProcess.Kill(); 
                    convertProcess.CancelErrorRead();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}