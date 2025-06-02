// Application/FileConverter/MacOsIntegration/DrUtilWrapper.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileConverter.MacOsIntegration
{
    public class DrUtilWrapper : IMacOSCdOperations
    {
        private const string DrUtilPath = "/usr/bin/drutil";

        private async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = DrUtilPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit()); // Offload WaitForExit to a pool thread
                return (process.ExitCode, output, error);
            }
        }

        public async Task<List<MacOsDriveInfo>> ListAudioCdDrivesAsync()
        {
            var drives = new List<MacOsDriveInfo>();
            // It's generally better to first get a list of drives, then query status for each.
            // `drutil list -xml` could provide structured output if available, but sticking to plain text for now.
            var (listExitCode, listOutput, listError) = await RunProcessAsync("list");

            if (listExitCode != 0)
            {
                Console.Error.WriteLine($"drutil list failed: {listError}");
                return drives;
            }

            // Regex to find device paths from 'drutil list' output. Example: "  1: /dev/disk3          MATSHITA DVD-R   UJ-8A8"
            // This needs to be robust for different 'drutil list' formats.
            // A simpler way might be to find lines starting with spaces, a number, colon, then /dev/disk
            var devicePathRegex = new Regex(@"^\s*\d+:\s*(/dev/disk\d+)\s+");
            var foundDevicePaths = new List<string>();

            using (StringReader reader = new StringReader(listOutput))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    Match m = devicePathRegex.Match(line);
                    if (m.Success)
                    {
                        foundDevicePaths.Add(m.Groups[1].Value);
                    }
                }
            }

            foreach (var devicePath in foundDevicePaths.Distinct())
            {
                var (statusExitCode, statusOutput, statusError) = await RunProcessAsync($"status -drive \"{devicePath}\"");
                if (statusExitCode != 0)
                {
                    Console.Error.WriteLine($"drutil status for {devicePath} failed: {statusError}\nOutput: {statusOutput}");
                    continue;
                }

                // Try to parse name and determine if it's an audio CD
                // Example Name line: "   Name: MATSHITADVD-R   UJ-8A8"
                // Example Media State: "   Media: CD-DA                (Audio CD)"
                var nameMatch = Regex.Match(statusOutput, @"Name:\s*(?<name>.+?)\s*$", RegexOptions.Multiline);
                string name = nameMatch.Success ? nameMatch.Groups["name"].Value.Trim() : Path.GetFileName(devicePath); // Fallback to device path name

                // Check for CD-DA (Compact Disc Digital Audio)
                bool isAudioCd = Regex.IsMatch(statusOutput, @"Media:\s*CD-DA", RegexOptions.IgnoreCase) ||
                                 Regex.IsMatch(statusOutput, @"Audio CD", RegexOptions.IgnoreCase);

                // Additional check: if "Type:" line contains CD-R/CD-RW and it's blank, it might be an audio CD.
                // For this example, we rely on explicit CD-DA or "Audio CD" strings.
                // A more thorough check might involve looking at session formats if available.

                if (isAudioCd)
                {
                    drives.Add(new MacOsDriveInfo(devicePath, name, true));
                }
                // Optionally, add all drives but mark HasAudioContent based on checks
                // else
                // {
                //     drives.Add(new MacOsDriveInfo(devicePath, name, false));
                // }
            }
            return drives;
        }

        public async Task<bool> EjectDriveAsync(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) throw new ArgumentNullException(nameof(devicePath));

            // drutil can eject by device path or by a "drive index" from `drutil list`.
            // Using device path is more robust if available.
            var (exitCode, output, error) = await RunProcessAsync($"tray eject -drive \"{devicePath}\"");

            if (exitCode != 0)
            {
                Console.Error.WriteLine($"drutil eject for {devicePath} failed: {error}\nOutput: {output}");
            }
            return exitCode == 0;
        }

        // Methods not implemented by drutil, to be handled by CdparanoiaWrapper
        public Task<List<MacOsTrackInfo>> GetTrackInfoAsync(string devicePath)
        {
            // drutil does not provide detailed audio track info (artist, title, duration per track).
            // This functionality is typically handled by tools like cdparanoia or by querying online databases.
            Console.WriteLine($"[INFO] GetTrackInfoAsync for {devicePath} is not implemented by DrUtilWrapper. This is expected to be handled by CdparanoiaWrapper.");
            return Task.FromResult(new List<MacOsTrackInfo>()); // Return empty list, indicating no info from drutil
        }

        public Task<bool> RipTrackAsync(string devicePath, int trackNumber, string outputFilePath, Action<double> progressCallback)
        {
            // drutil is for drive control and status, not for audio ripping.
            // Ripping is typically done by tools like cdparanoia.
            Console.WriteLine($"[INFO] RipTrackAsync for track {trackNumber} on {devicePath} is not implemented by DrUtilWrapper. This is expected to be handled by CdparanoiaWrapper.");
            return Task.FromResult(false); // Indicate failure or not supported by this wrapper
        }
    }
}
