using System;
using System.IO;

namespace FFVideoConverter
{
    public enum JobState { NotStarted, Running, Paused, Canceled, Completed }
    public enum JobType { Conversion, FastCut, Download }


    public class Job
    {
        public MediaInfo SourceInfo { get; }
        public string Title { get { return SourceInfo.Title; } }
        public string Destination { get; }
        public string DestinationFileName { get { return Path.GetFileName(Destination); } }
        public ConversionOptions ConversionOptions { get; }
        public JobState State { get; set; }
        public JobType JobType { get; }

        public Job(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions)
        {
            SourceInfo = sourceInfo;
            Destination = destination;
            ConversionOptions = conversionOptions;
            State = JobState.NotStarted;
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
        }
    }
}