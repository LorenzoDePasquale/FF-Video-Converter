using System;
using System.Collections.Generic;
using System.IO;

namespace FFVideoConverter
{
    public enum JobState { Queued, Running, Paused, Canceled, Completed, Failed }
    public enum JobType { Conversion, FastCut, Download }

    public struct ConversioResult
    {
        public string Label { get; }
        public string Content { get; }

        public ConversioResult(string label, string content)
        {
            Label = label;
            Content = content;
        }
    }

    public class Job
    {
        public MediaInfo SourceInfo { get; }
        public string Destination { get; }
        public string DestinationFileName => Path.GetFileName(Destination);
        public ConversionOptions ConversionOptions { get; }
        public float OutputFramerate { get; }
        public JobState State { get; set; }
        public JobType JobType { get; }
        public List<ConversioResult> ConversionResults { get; }

        public Job(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions)
        {
            SourceInfo = sourceInfo;
            Destination = destination;
            ConversionOptions = conversionOptions;
            State = JobState.Queued;
            if (conversionOptions.Encoder is NativeEncoder)
            {
                if (conversionOptions.Start != TimeSpan.Zero || conversionOptions.End != TimeSpan.Zero)
                {
                    JobType = JobType.FastCut;
                }
                else
                {
                    JobType = JobType.Download;
                }
            }
            else
            {
                JobType = JobType.Conversion;
            }
            OutputFramerate = (float)(conversionOptions.Framerate > 0 ? conversionOptions.Framerate : sourceInfo.Framerate);
            ConversionResults = new List<ConversioResult>();
        }
    }
}