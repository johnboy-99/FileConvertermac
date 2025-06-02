// Application/FileConverter/MacOsIntegration/MacOsDriveInfo.cs
namespace FileConverter.MacOsIntegration
{
    public class MacOsDriveInfo
    {
        public string DevicePath { get; } // e.g., /dev/disk3
        public string Name { get; }       // e.g., "SuperDrive" or Volume Name if mounted
        public bool HasAudioContent { get; } // Indicates if it appears to be an audio CD

        public MacOsDriveInfo(string devicePath, string name, bool hasAudioContent)
        {
            DevicePath = devicePath;
            Name = name;
            HasAudioContent = hasAudioContent;
        }
    }
}
