using System.Collections.Generic;
using System.IO;
using FFVideoConverter.Encoders;


namespace FFVideoConverter
{
    public enum JobState
    {
        Queued, Running, Paused, Canceled, Completed, Failed 
    }

    public enum JobType
    {
        Conversion, FastCut, Download, Remux, AudioExport 
    }

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
        public JobState State { get; set; }
        public JobType JobType { get; }
        public List<ConversioResult> ConversionResults { get; }
        public AudioTrack AudioTrack { get; }


        public Job()
        {
        }

        public Job(MediaInfo sourceInfo, string destination, ConversionOptions conversionOptions)
        {
            SourceInfo = sourceInfo;
            Destination = destination;
            ConversionOptions = conversionOptions;
            State = JobState.Queued;

            if (conversionOptions.Encoder is CopyEncoder && conversionOptions.AudioConversionOptions.Count == 0)
            {
                if (conversionOptions.EncodeSections?.Count > 0)
                {
                    JobType = JobType.FastCut;
                }
                else if (!sourceInfo.IsLocal)
                {
                    JobType = JobType.Download;
                }
                else
                {
                    JobType = JobType.Remux;
                }
            }
            else
            {
                JobType = JobType.Conversion;
            }

            ConversionResults = new List<ConversioResult>();
        }

        public Job(MediaInfo sourceInfo, string destination, AudioTrack audioTrack)
        {
            SourceInfo = sourceInfo;
            Destination = destination;
            State = JobState.Queued;
            JobType = JobType.AudioExport;
            ConversionResults = new List<ConversioResult>();
            AudioTrack = audioTrack;
        }
    }
}