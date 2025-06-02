// Application/FileConverter/MacOsIntegration/IMacOSCdOperations.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileConverter.MacOsIntegration
{
    public interface IMacOSCdOperations
    {
        /// <summary>
        /// Lists CD/DVD drives that potentially contain audio.
        /// </summary>
        Task<List<MacOsDriveInfo>> ListAudioCdDrivesAsync();

        /// <summary>
        /// Gets track information for a specific audio CD.
        /// </summary>
        /// <param name="devicePath">The device path of the CD drive (e.g., /dev/diskX).</param>
        Task<List<MacOsTrackInfo>> GetTrackInfoAsync(string devicePath);

        /// <summary>
        /// Rips a specific audio track from a CD.
        /// </summary>
        /// <param name="devicePath">The device path of the CD drive.</param>
        /// <param name="trackNumber">The 1-based track number to rip.</param>
        /// <param name="outputFilePath">The full path where the ripped audio file (e.g., WAV) should be saved.</param>
        /// <param name="progressCallback">An action to report progress (0.0 to 1.0).</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> RipTrackAsync(string devicePath, int trackNumber, string outputFilePath, Action<double> progressCallback);

        /// <summary>
        /// Ejects the specified CD/DVD drive.
        /// </summary>
        /// <param name="devicePath">The device path of the CD drive.</param>
        Task<bool> EjectDriveAsync(string devicePath);
    }
}
