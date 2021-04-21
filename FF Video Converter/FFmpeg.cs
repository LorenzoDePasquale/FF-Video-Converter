using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFVideoConverter.Encoders;

namespace FFVideoConverter
{
    public class FFmpegEngine
    {
        public event Action<ProgressData> ProgressChanged;
        public event Action ConversionCompleted;
        public event Action<string> ConversionAborted;

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

        Process convertProcess = new Process();
        PerformanceCounter memoryCounter = new PerformanceCounter();
        ProgressData progressData, previousProgressData;
        int i = 0;
        bool stopped = false, partialProgress = false;
        SynchronizationContext synchronizationContext;
        string errorLine;


        public FFmpegEngine()
        {
            convertProcess.StartInfo.FileName = "ffmpeg.exe";
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

        public void Convert(MediaInfo sourceInfo, string outputPath, ConversionOptions conversionOptions)
        {
            progressData = new ProgressData();
            previousProgressData = new ProgressData();
            errorLine = "";

            //Capture the Synchronization Context of the caller, in order to invoke the events in its original thread
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

            //Gets the total number of output frames
            Filters.FpsFilter fpsFilter = conversionOptions.Filters.FirstOrDefault(f => f is Filters.FpsFilter) as Filters.FpsFilter;
            double outputFps = fpsFilter?.Framerate ?? sourceInfo.Framerate;
            progressData.TotalFrames = System.Convert.ToInt32(progressData.TotalTime.TotalSeconds * outputFps);

            //Get the total output file size (if using constant bitrate)
            if (conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass || conversionOptions.EncodingMode == EncodingMode.AverageBitrate_SinglePass)
            {
                progressData.TotalByteSize = conversionOptions.Encoder.Bitrate.Bps / 8 * System.Convert.ToInt64(progressData.TotalTime.TotalSeconds);
                foreach (var audioTrack in sourceInfo.AudioTracks)
                {
                    if (conversionOptions.AudioConversionOptions.ContainsKey(audioTrack.StreamIndex))
                    {
                        progressData.TotalByteSize += conversionOptions.AudioConversionOptions[audioTrack.StreamIndex].Encoder.Bitrate.Bps / 8 * System.Convert.ToInt64(progressData.TotalTime.TotalSeconds);
                    }
                    else
                    {
                        progressData.TotalByteSize += audioTrack.Bitrate.Bps / 8 * System.Convert.ToInt64(progressData.TotalTime.TotalSeconds);
                    }
                }
            }

            partialProgress = false;
            progressData.EncodingMode = conversionOptions.EncodingMode;
            stopped = false;

            //Starts the conversion task
            Task<bool> conversionTask;
            if (conversionOptions.EncodeSections.Count == 0)
            {
                conversionTask = Convert_SingleSegment(sourceInfo, outputPath, conversionOptions, TimeSpan.Zero, TimeSpan.Zero);
            }
            else if (conversionOptions.EncodeSections.Count == 1)
            {
                conversionTask = Convert_SingleSegment(sourceInfo, outputPath, conversionOptions, conversionOptions.EncodeSections.ActualStart, conversionOptions.EncodeSections.ActualEnd);
            }
            else
            {
                conversionTask = Convert_MultipleSegments(sourceInfo, outputPath, conversionOptions);
            }

            //Reports the conversion result to the original caller
            conversionTask.ContinueWith(result => ReportCompletition(result.Result));
        }

        public void ExtractAudioTrack(MediaInfo sourceInfo, int streamIndex, string destination, TimeSpan start, TimeSpan end)
        {
            progressData = new ProgressData() { EncodingMode = EncodingMode.Copy };

            //Captures the Synchronization Context of the caller, in order to invoke the events in its original thread
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
            StringBuilder sb = new StringBuilder("-y -progress -");
            sb.Append($" -ss {start}");
            sb.Append($" -i \"{(sourceInfo.HasExternalAudio ? sourceInfo.ExternalAudioSource : sourceInfo.Source)}\"");
            if (end != TimeSpan.Zero) sb.Append($" -t {end - start}");
            sb.Append($" -map 0:{streamIndex}");
            sb.Append(" -vn -c:a copy");
            sb.Append($" \"{destination}\" -loglevel error -stats");

            Task.Run(() => RunConversionProcess(sb.ToString())).ContinueWith(success => ReportCompletition(success.Result));
        }

        public void PauseConversion()
        {
            convertProcess.Suspend();
        }

        public void ResumeConversion()
        {
            convertProcess.Resume();
        }

        public async Task CancelConversion()
        {
            try
            {
                if (convertProcess != null)
                {
                    stopped = true;
                    convertProcess.CancelOutputRead();
                    convertProcess.CancelErrorRead();

                    AttachConsole((uint)convertProcess.Id);
                    SetConsoleCtrlHandler(null, true);
                    try
                    {
                        GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
                        await Task.Run(convertProcess.WaitForExit).ConfigureAwait(false);
                    }
                    finally
                    {
                        SetConsoleCtrlHandler(null, false);
                        FreeConsole();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public void StopConversion()
        {
            try
            {
                if (convertProcess != null)
                {
                    stopped = true;
                    convertProcess.CancelOutputRead();
                    convertProcess.CancelErrorRead();
                    convertProcess.Kill();
                }
            }
            catch (Exception)
            {
            }
        }

        private async Task<bool> Convert_SingleSegment(MediaInfo sourceInfo, string outputPath, ConversionOptions conversionOptions, TimeSpan start, TimeSpan end)
        {
            bool success = false;
            if (conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass)
            {
                success = await RunConversionProcess(BuildArgumentsString(sourceInfo, outputPath, conversionOptions, start, end)).ConfigureAwait(false);
                if (!stopped && success)
                {
                    progressData.EncodingMode = EncodingMode.AverageBitrate_SecondPass;
                    conversionOptions.EncodingMode = EncodingMode.AverageBitrate_SecondPass;
                }
                else return success;
            }

            success = await RunConversionProcess(BuildArgumentsString(sourceInfo, outputPath, conversionOptions, start, end)).ConfigureAwait(false);

            //Removes 2pass log files, if they exist
            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory).Where(x => x.Contains("log")))
            {
                File.Delete(file);
            }

            return success;
        }

        private async Task<bool> Convert_MultipleSegments(MediaInfo sourceInfo, string outputPath, ConversionOptions conversionOptions)
        {
            bool success = false;
            partialProgress = true;
            string outputDirectory = Path.GetDirectoryName(outputPath);
            string outputFileName = Path.GetFileNameWithoutExtension(outputPath);

            if (conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass)
            {
                for (int i = 0; i < conversionOptions.EncodeSections.Count; i++)
                {
                    string arguments = BuildArgumentsString(sourceInfo, outputPath, conversionOptions, conversionOptions.EncodeSections[i].Start, conversionOptions.EncodeSections[i].End, "segment" + i.ToString());
                    success = await RunConversionProcess(arguments).ConfigureAwait(false);
                    if (stopped || !success) return success;
                }

                progressData.EncodingMode = EncodingMode.AverageBitrate_SecondPass;
                conversionOptions.EncodingMode = EncodingMode.AverageBitrate_SecondPass;
                previousProgressData.CurrentTime = TimeSpan.Zero;
                previousProgressData.CurrentFrames = 0;
            }

            for (int i = 0; i < conversionOptions.EncodeSections.Count; i++)
            {
                bool fadeStart = conversionOptions.FadeEffect && i != 0;
                bool fadeEnd = conversionOptions.FadeEffect && i < conversionOptions.EncodeSections.Count - 1;
                string destination = $"{outputDirectory}\\{outputFileName}_part_{i}.mp4";
                string arguments = BuildArgumentsString(sourceInfo, destination, conversionOptions, conversionOptions.EncodeSections[i].Start, conversionOptions.EncodeSections[i].End, "segment" + i.ToString(), fadeStart, fadeEnd);
                success = await RunConversionProcess(arguments).ConfigureAwait(false);
                if (stopped || !success) break;
                File.AppendAllText("concat.txt", $"file '{destination}'\n");
            }

            //Joins all segments toghether
            if (!stopped && success)
            {
                success = await RunConversionProcess($"-y -f concat -safe 0 -i concat.txt -c copy \"{outputPath}\"").ConfigureAwait(false);
                File.Delete("concat.txt");
            }

            //Removes segments
            for (int i = 0; i < conversionOptions.EncodeSections.Count; i++)
            {
                string partName = $"{outputDirectory}\\{outputFileName}_part_{i}.mp4";
                if (File.Exists(partName)) File.Delete(partName);
            }

            //Removes 2pass log files, if they exist
            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory).Where(x => x.Contains("log")))
            {
                File.Delete(file);
            }

            return success;
        }

        private string BuildArgumentsString(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions, TimeSpan start, TimeSpan end, string twopasslogfile = "", bool fadeStart = false, bool fadeEnd = false)
        {
            StringBuilder sb = new StringBuilder("-y -progress -");

            //Start time
            if (start != TimeSpan.Zero) sb.Append($" -ss {start}");

            //Input path
            sb.Append($" -i \"{sourceInfo.Source}\"");

            //Duration
            if (end != TimeSpan.Zero) sb.Append($" -t {end - start}");

            //External audio source
            if (sourceInfo.HasExternalAudio && !conversionOptions.NoAudio && conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                if (start != TimeSpan.Zero) sb.Append($" -ss {start}");
                sb.Append($" -i \"{sourceInfo.ExternalAudioSource}\"");
                if (end != TimeSpan.Zero) sb.Append($" -t {end - start}");
            }

            //Main video track
            sb.Append($" -map 0:v:0");

            //Add or skip audio tracks
            var audioTracks = sourceInfo.AudioTracks.Where(at => at.Enabled == true).ToList();
            if (!conversionOptions.NoAudio && conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                foreach (var audioTrack in audioTracks)
                {
                    sb.Append($" -map {(sourceInfo.HasExternalAudio ? "1" : $"0:{audioTrack.StreamIndex}")}");
                    sb.Append($" -disposition:{(sourceInfo.HasExternalAudio ? "1" : audioTrack.StreamIndex)} {(audioTrack.Default ? "default" : "none")}");
                    if (conversionOptions.AudioConversionOptions.ContainsKey(audioTrack.StreamIndex))
                    {
                        sb.Append($" -metadata:s:{(sourceInfo.HasExternalAudio ? "1" : audioTrack.StreamIndex)} title=\"{conversionOptions.AudioConversionOptions[audioTrack.StreamIndex].Title}\"");
                        sb.Append($" -metadata:s:{(sourceInfo.HasExternalAudio ? "1" : audioTrack.StreamIndex)} handler_name=\"{conversionOptions.AudioConversionOptions[audioTrack.StreamIndex].Title}\"");
                    }
                }
            }

            //Subtitles
            if (conversionOptions.EncodingMode != EncodingMode.NoEncoding)
            {
                sb.Append(" -map 0:s?");
            }

            //Filters
            if (conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                Filtergraph filtergraph = new Filtergraph();

                //Video filters
                if (conversionOptions.Filters.Count > 0)
                {
                    filtergraph.AddFilters(conversionOptions.Filters, 0, 0);
                }
                if (fadeStart)
                {
                    filtergraph.AddFilter(new Filters.FadeFilter(Filters.FadeMode.In, 0.2), 0, 0);
                }
                if (fadeEnd)
                {
                    TimeSpan actualEnd = end != TimeSpan.Zero ? end : sourceInfo.Duration;
                    filtergraph.AddFilter(new Filters.FadeFilter(Filters.FadeMode.Out, 0.2, actualEnd.TotalSeconds - start.TotalSeconds - 0.2), 0, 0); 
                }

                //Audio filters
                /* Currently disabled as it requires dedicated mapping which is not supported
                foreach (var item in conversionOptions.AudioConversionOptions)
                {
                    if (item.Value.Filters.Count > 0)
                    {
                        filtergraph.AddFilters(item.Value.Filters, sourceInfo.HasExternalAudio ? 1 : 0, item.Key);
                    }
                }*/

                if (filtergraph.Count > 0)
                {
                    sb.Append($" -filter_complex \"{filtergraph.GenerateFiltergraph()}\"");
                }
            }

            //Video encoder
            if (conversionOptions.EncodingMode != EncodingMode.NoEncoding)
            {
                sb.Append("  -c:v " + conversionOptions.Encoder.GetFFMpegCommand(conversionOptions.EncodingMode));
                if ((conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass || conversionOptions.EncodingMode == EncodingMode.AverageBitrate_SecondPass) && twopasslogfile != "")
                {
                    sb.Append(" -passlogfile " + twopasslogfile);
                }
                //When cutting without encoding, "-avoid_negative_ts make_zero" allows to cut at the nearest keyframe before the start position
                //Without this flag audio would be cut at the start position, but video would start playing only after the next keyframe 
                sb.Append(" -avoid_negative_ts make_zero");
            }

            //Audio encoders
            if (conversionOptions.NoAudio || conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass)
            {
                sb.Append(" -an");
            }
            else
            {
                foreach (var audioTrack in audioTracks)
                {
                    if (conversionOptions.AudioConversionOptions.ContainsKey(audioTrack.StreamIndex))
                    {
                        AudioConversionOptions audioConversionOptions = conversionOptions.AudioConversionOptions[audioTrack.StreamIndex];
                        sb.Append($" -c:{(sourceInfo.HasExternalAudio ? "1" : audioTrack.StreamIndex)} {audioConversionOptions.Encoder.GetFFMpegCommand(sourceInfo.HasExternalAudio ? 1 : audioTrack.StreamIndex)}");
                        sb.Append($" -ac:{(sourceInfo.HasExternalAudio ? "1" : audioTrack.StreamIndex)} {audioConversionOptions.Channels}");
                    }
                    else
                    {
                        sb.Append($" -c:{(sourceInfo.HasExternalAudio ? "1" : audioTrack.StreamIndex)} copy");
                    }
                }
            }

            //MP4 specific stuff
            if (Path.GetExtension(destination) == "mp4")
            {
                //Subtitle encoder (necessary to convert mkv subtitles to mp4 format)
                sb.Append(" -c:s mov_text");
                //Moves moov atom to the beginning of the file; ignored by the mkv muxer (does it do the same thing automatically? No answer on the web...)
                sb.Append(" -movflags +faststart");
            }


            //Output path
            if (conversionOptions.EncodingMode != EncodingMode.AverageBitrate_FirstPass)
            {
                //max_muxing_queue_size is necessary to allow ffmpeg to allocate more memory to muxing, so that it's enough for very big streams
                sb.Append($" -max_muxing_queue_size 2048 \"{destination}\" -loglevel error -stats");
            }
            else
            {
                sb.Append($" -f null NUL -loglevel error -stats");
            }

#if DEBUG
            Thread thread = new Thread(() => System.Windows.Clipboard.SetText(sb.ToString()));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
#endif

            return sb.ToString();
        }

        private async Task<bool> RunConversionProcess(string arguments)
        {
            convertProcess.StartInfo.Arguments = arguments;
            convertProcess.Start();
            convertProcess.BeginOutputReadLine();
            convertProcess.BeginErrorReadLine();
            convertProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
            stopped = false;
            i = 0;

            await Task.Run(convertProcess.WaitForExit).ConfigureAwait(false);
            convertProcess.CancelOutputRead();
            convertProcess.CancelErrorRead();
            previousProgressData = progressData;

            int exitCode = convertProcess.ExitCode; //-1: killed; 1: crashed; everything else: success
            if (exitCode == -1) stopped = true;
            return exitCode != 1; 
        }

        private void ReportCompletition(bool success)
        {
            if (!stopped)
            {
                synchronizationContext.Post(new SendOrPostCallback((o) =>
                {
                    if (!success)
                        ConversionAborted?.Invoke(errorLine);
                    else
                        ConversionCompleted?.Invoke();
                }), null);
            }
        }

        private void ConvertProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                (string key, string value) = ToCouple(e.Data.Split('='));
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
                        case "frame":
                            progressData.CurrentFrames = System.Convert.ToInt32(value);
                            if (partialProgress) 
                                progressData.CurrentFrames += previousProgressData.CurrentFrames;
                            progressData.Percentage = progressData.TotalFrames != 0 ? progressData.CurrentFrames * 100 / progressData.TotalFrames : (float)(progressData.CurrentTime.TotalSeconds * 100 / progressData.TotalTime.TotalSeconds);
                            if (progressData.EncodingMode == EncodingMode.AverageBitrate_FirstPass)
                                progressData.Percentage /= 2;
                            else if (progressData.EncodingMode == EncodingMode.AverageBitrate_SecondPass)
                                progressData.Percentage = progressData.Percentage / 2 + 50;
                            break;
                        case "fps":
                            progressData.EncodingSpeedFps = System.Convert.ToInt16(System.Convert.ToSingle(value));
                            break;
                        case "bitrate":
                            Bitrate currentBitrate = new Bitrate(System.Convert.ToSingle(value.Replace("kbits/s", "")));
                            if (progressData.Percentage > 5) //Skips first seconds to give the encoder time to adjust it's bitrate
                            {
                                progressData.AverageBitrate = new Bitrate(progressData.AverageBitrate.Kbps + ((currentBitrate.Kbps - progressData.AverageBitrate.Kbps) / ++i));
                            }
                            break;
                        case "total_size":
                            progressData.CurrentByteSize = System.Convert.ToInt64(value);
                            if (partialProgress) 
                                progressData.CurrentByteSize += previousProgressData.CurrentByteSize;
                            break;
                        case "out_time":
                            progressData.CurrentTime = TimeSpan.Parse(value);
                            if (partialProgress) 
                                progressData.CurrentTime += previousProgressData.CurrentTime;
                            break;
                        case "speed":
                            progressData.EncodingSpeed = System.Convert.ToSingle(value[0..^1]);
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

            (string, string) ToCouple(string[] array) => (array[0], array[1]);
        }

        private void ConvertProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                errorLine += e.Data + "\n";
            }
        }


        #region Native methods

        internal const int CTRL_C_EVENT = 0;
        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        delegate bool ConsoleCtrlDelegate(uint CtrlType);

        #endregion
    }
}