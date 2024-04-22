using AMSMigrate.Contracts;

namespace AMSMigrate.Transform;

public static class TransformUtils
{
    public const string MEDIA_FILE = ".mp4";
    public const string DASH_MANIFEST = ".mpd";
    public const string HLS_MANIFEST = ".m3u8";
    public const string VTT_FILE = ".vtt";
    public const string CMFT_FILE = ".cmft";
    public const string TRANSCRIPT_SOURCE = "transcriptsrc";

    //Todo: add some tests for this
    public static List<string> GenerateOutputs(List<Track> selectedTracks)
    {
        var outputs = new List<string>();
        var tracksByType = selectedTracks.GroupBy(t => t.Type);
        foreach (var grouping in tracksByType)
        {
            outputs.AddRange(grouping.Select((t, i) =>
            {
                var ext = t is TextTrack ? VTT_FILE : MEDIA_FILE;
                // TODO: if you want to keep original file names.
                // var baseName = Path.GetFileNameWithoutExtension(t.Source);
                return $"{t.Type.ToString().ToLowerInvariant()}{(grouping.Count() > 1 ? $"_{i}" : string.Empty)}{ext}";
            }).ToList());
        }
        
        // foreach (var grouping in tracksByType.Where(t => t.Key == StreamType.Video))
        // {
        //     outputs.AddRange(grouping.Select((t, i) =>
        //     {
        //         var ext = t is TextTrack ? VTT_FILE : MEDIA_FILE;
        //         // TODO: if you want to keep original file names.
        //         // var baseName = Path.GetFileNameWithoutExtension(t.Source);
        //         return $"trick_{t.Type.ToString().ToLowerInvariant()}{(grouping.Count() > 1 ? $"_{i}" : string.Empty)}{ext}";
        //     }).ToList());
        // }
        
        return outputs;
    }
}