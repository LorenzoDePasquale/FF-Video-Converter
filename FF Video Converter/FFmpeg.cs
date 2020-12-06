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
                return (long)memoryCounter.NextValue();
            }
        }

        private readonly Process convertProcess = new Process();
        private readonly PerformanceCounter memoryCounter = new PerformanceCounter();
        private ProgressData progressData;
        private int i = 0;
        private string outputLine;
        private bool stopped = false;
        private SynchronizationContext synchronizationContext;
        private TimeSpan startTime;

        public FFmpegEngine()
        {
            convertProcess.StartInfo.FileName = "ffmpeg";
            convertProcess.StartInfo.RedirectStandardError = true;
            convertProcess.StartInfo.UseShellExecute = false;
            convertProcess.StartInfo.CreateNoWindow = true;
            convertProcess.ErrorDataReceived += ConvertProcess_ErrorDataReceived;

            memoryCounter.CategoryName = "Process";
            memoryCounter.CounterName = "Working Set - Private";
            memoryCounter.InstanceName = "ffmpeg";
        }

        public async void Convert(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions)
        {
            progressData = new ProgressData();

            //Capture the Synchronization Context of the caller, in order to invoke the events on its original thread
            synchronizationContext = SynchronizationContext.Current;

            //Duration
            if (conversionOptions.End != TimeSpan.Zero)
            {
                progressData.TotalTime = conversionOptions.End - conversionOptions.Start;
            }
            else
            {
                progressData.TotalTime = sourceInfo.Duration - conversionOptions.Start;
            }

            //Get nearest before keyframe (so that source file gets decoded only from here)
            startTime = TimeSpan.Zero;
            if (conversionOptions.Start != TimeSpan.Zero)
            {
                var (before, _, _) = await sourceInfo.GetNearestBeforeAndAfterKeyFrames(conversionOptions.Start.TotalSeconds).ConfigureAwait(false);
                startTime = TimeSpan.FromSeconds(before);
            }

            progressData.EncodingMode = conversionOptions.EncodingMode;
            if (conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass)
            {
                await StartConversionProcess(BuildArgumentsString(sourceInfo, destination, conversionOptions), false).ConfigureAwait(false);
                if (!stopped)
                {
                    progressData.EncodingMode = EncodingMode.AverageBitrate_SecondPass;
                    conversionOptions.EncodingMode = EncodingMode.AverageBitrate_SecondPass;
                    await StartConversionProcess(BuildArgumentsString(sourceInfo, destination, conversionOptions)).ConfigureAwait(false);
                }
                //Remove 2pass log files
                foreach (var file in Directory.GetFiles(Environment.CurrentDirectory).Where(x => x.Contains("log")))
                {
                    File.Delete(file);
                }
            }
            else
            {
                await StartConversionProcess(BuildArgumentsString(sourceInfo, destination, conversionOptions)).ConfigureAwait(false);
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

            StartConversionProcess(sb.ToString());
        }

        public string BuildArgumentsString(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions)
        {
            StringBuilder sb = new StringBuilder("-y");

            //Decoding start time
            if (conversionOptions.Start != TimeSpan.Zero) sb.Append($" -ss {startTime}");

            //Input path
            sb.Append($" -i \"{sourceInfo.Source}\"");

            //External audio source
            if (sourceInfo.HasExternalAudio && conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                if (conversionOptions.Start != TimeSpan.Zero) sb.Append($" -ss {startTime}");
                sb.Append($" -i \"{sourceInfo.ExternalAudioSource}\"");
            }

            //Duration
            if (conversionOptions.End != TimeSpan.Zero) sb.Append($" -t {conversionOptions.End - conversionOptions.Start}");

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
                if (conversionOptions.Resolution.HasValue() || conversionOptions.CropData.HasValue() || conversionOptions.Rotation.HasValue())
                {
                    sb.Append(" -vf " + ConcatFilters(conversionOptions.Resolution.FilterString, conversionOptions.CropData.FilterString, conversionOptions.Rotation.FilterString));
                }
            }

            //Audio encoder
            sb.Append(conversionOptions.SkipAudio || conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass ? " -an" : " -c:a copy");

            //Encoding start time
            if (conversionOptions.Start != TimeSpan.Zero) sb.Append($" -ss {conversionOptions.Start - startTime}");

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

        private async Task StartConversionProcess(string arguments, bool reportCompletition = true)
        {
            convertProcess.StartInfo.Arguments = arguments;
            convertProcess.Start();
            convertProcess.BeginErrorReadLine();
            convertProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
            stopped = false;

            await Task.Run(() =>
            {
                convertProcess.WaitForExit();
            }).ConfigureAwait(false);
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
                    progressData.ErrorMessage = outputLine;
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

        private void ConvertProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            outputLine = e.Data ?? outputLine;

            if (e.Data != null)
            {
                //Conversion
                if (outputLine.StartsWith("frame")) //frame=   47 fps=0.0 q=0.0 size=       0kB time=00:00:00.00 bitrate=N/A speed=   0x    
                {
                    progressData.CurrentFrame = System.Convert.ToUInt32(outputLine.Remove(outputLine.IndexOf(" fps")).Remove(0, 6));
                    progressData.EncodingSpeedFps = System.Convert.ToInt16(outputLine.Remove(outputLine.IndexOf(" q")).Remove(0, outputLine.IndexOf("fps") + 4).Replace(".", ""));
                    progressData.CurrentByteSize = outputLine.Contains("size=N/A") ? 0 : System.Convert.ToInt32(outputLine.Remove(outputLine.IndexOf(" time") - 2).Remove(0, outputLine.IndexOf("size") + 5)) * 1000L;
                    progressData.CurrentTime = TimeSpan.Parse(outputLine.Remove(outputLine.IndexOf(" bit")).Remove(0, outputLine.IndexOf("time") + 5));
                    float currentBitrate = outputLine.Contains("bitrate=N/A") ? 0 : System.Convert.ToSingle(outputLine.Remove(outputLine.IndexOf("kbits")).Remove(0, outputLine.IndexOf("bitrate") + 8), CultureInfo.InvariantCulture);
                    if (progressData.CurrentTime.Seconds > 5) //Skips first 5 seconds to give the encoder time to adjust it's bitrate
                    {
                        progressData.AverageBitrate += (currentBitrate - progressData.AverageBitrate) / ++i;
                    }

                    if (outputLine.EndsWith("N/A    ") || outputLine.EndsWith("x")) progressData.EncodingSpeed = 0;
                    else progressData.EncodingSpeed = System.Convert.ToSingle(outputLine.Remove(outputLine.IndexOf('x')).Remove(0, outputLine.IndexOf("speed") + 6), CultureInfo.InvariantCulture);

                    synchronizationContext.Post(new SendOrPostCallback((o) =>
                    {
                        ProgressChanged?.Invoke(progressData);
                    }), null);
                }
                //Download
                else if (outputLine.StartsWith("size")) //size=    9943kB time=00:10:02.26 bitrate= 135.2kbits/s speed= 340x
                {
                    progressData.CurrentByteSize = System.Convert.ToInt32(outputLine.Remove(outputLine.IndexOf(" time") - 2).Remove(0, 5)) * 1000L;
                    progressData.CurrentTime = TimeSpan.Parse(outputLine.Remove(outputLine.IndexOf(" bit")).Remove(0, outputLine.IndexOf("time") + 5));
                    float currentBitrate = outputLine.Contains("bitrate=N/A") ? 0 : System.Convert.ToSingle(outputLine.Remove(outputLine.IndexOf("kbits")).Remove(0, outputLine.IndexOf("bitrate") + 8), CultureInfo.InvariantCulture);
                    if (progressData.CurrentTime.Seconds > 5) //Skips first 5 seconds to give the encoder time to adjust it's bitrate
                    {
                        progressData.AverageBitrate += (currentBitrate - progressData.AverageBitrate) / ++i;
                    }

                    if (outputLine.EndsWith("N/A    ") || outputLine.EndsWith("x")) progressData.EncodingSpeed = 0;
                    else progressData.EncodingSpeed = System.Convert.ToSingle(outputLine.Remove(outputLine.IndexOf('x')).Remove(0, outputLine.IndexOf("speed") + 6), CultureInfo.InvariantCulture);

                    synchronizationContext.Post(new SendOrPostCallback((o) =>
                    {
                        ProgressChanged?.Invoke(progressData);
                    }), null);
                }
                else if (outputLine.StartsWith("Error"))
                {
                    try
                    {
                        convertProcess.Kill();
                    }
                    catch (Exception)
                    { }
                }
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