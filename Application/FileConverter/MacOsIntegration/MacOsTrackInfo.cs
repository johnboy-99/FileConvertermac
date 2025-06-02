// Application/FileConverter/MacOsIntegration/MacOsTrackInfo.cs
using System;

namespace FileConverter.MacOsIntegration
{
    public class MacOsTrackInfo
    {
        public int TrackNumber { get; }
        public TimeSpan Duration { get; } // Optional, might not always be available
        public string Artist { get; }    // Optional, from CD-Text
        public string Title { get; }     // Optional, from CD-Text

        public MacOsTrackInfo(int trackNumber, TimeSpan duration = default, string artist = null, string title = null)
        {
            TrackNumber = trackNumber;
            Duration = duration;
            Artist = artist ?? string.Empty;
            Title = title ?? string.Empty;
        }
    }
}
