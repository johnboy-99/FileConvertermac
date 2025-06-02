// Application/FileConverter/MacOsIntegration/CdparanoiaWrapper.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileConverter.MacOsIntegration
{
    public class CdparanoiaWrapper : IMacOSCdOperations
    {
        private const string CdparanoiaPath = "/usr/local/bin/cdparanoia"; // Common Homebrew path; adjust if needed or make configurable

        private async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string arguments, Action<string> errorDataReceivedCallback = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = CdparanoiaPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                StringWriter outputWriter = new StringWriter();
                StringWriter errorWriter = new StringWriter(); // Collect all error output

                process.OutputDataReceived += (sender, args) => { if (args.Data != null) outputWriter.WriteLine(args.Data); };

                if (errorDataReceivedCallback != null)
                {
                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            errorWriter.WriteLine(args.Data); // Still collect for final error string
                            errorDataReceivedCallback(args.Data);
                        }
                    };
                }
                else
                {
                    process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorWriter.WriteLine(args.Data); };
                }

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());
                return (process.ExitCode, outputWriter.ToString(), errorWriter.ToString());
            }
        }

        public async Task<List<MacOsTrackInfo>> GetTrackInfoAsync(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) throw new ArgumentNullException(nameof(devicePath));

            var tracks = new List<MacOsTrackInfo>();
            // cdparanoia outputs track listing to stderr
            // The "-sQ" flags are: s = search (for tracks), Q = quiet (no progress, just summary)
            var (exitCode, output, errorOutput) = await RunProcessAsync($"-sQ -d \"{devicePath}\"");

            if (exitCode != 0)
            {
                Console.Error.WriteLine($"cdparanoia -sQ for {devicePath} failed. ExitCode: {exitCode}\nStdOut: {output}\nStdErr: {errorOutput}");
                // Attempt to parse errorOutput anyway, as -sQ might still list tracks on stderr even with some errors.
            }

            // Parse the stderr output from cdparanoia -sQ
            // Example output:
            // Mesaage from cdparanoia:
            // Track    Start Status  ( Length    ) Ripped Length  Copy  Pre-emphasis  Filename
            // --------------------------------------------------------------------------------
            //   1    000000   OK    ( 03:36.00  )
            //   2    016200   OK    ( 04:55.00  )
            // ...
            // TOTAL                     ( 08:31.00  )
            // More commonly, the output for track listing is simpler from just `cdparanoia -Q` or `cdparanoia -Q -d /dev/diskX`
            // Let's adjust regex for a common format of track listing from `cdparanoia -Q` or `-sQ`
            // Example: " 1.    16200 [03:36.00]       (AUDIO)" or "Track  1: (03:36.00) 16200 sectors"
            var trackRegex = new Regex(@"^\s*Track\s+(?<track>\d+):\s+\((?<mins>\d{2}):(?<secs>\d{2})\.(?<frames>\d{2})\)\s+\d+\s+sectors", RegexOptions.Multiline);
            // Fallback regex if the above doesn't match, for simpler "1. [MM:SS.FF]" format
            var simplerTrackRegex = new Regex(@"^\s*(?<track>\d+)\.\s+(?:\d+\s+)?\[(?<mins>\d{2}):(?<secs>\d{2})\.(?<hundredths>\d{2})\]", RegexOptions.Multiline);


            using (StringReader reader = new StringReader(errorOutput)) // Track info is on stderr
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    Match match = trackRegex.Match(line);
                    if (!match.Success) // Try simpler regex if first one fails
                    {
                        match = simplerTrackRegex.Match(line);
                    }

                    if (match.Success)
                    {
                        int trackNum = int.Parse(match.Groups["track"].Value);
                        int mins = int.Parse(match.Groups["mins"].Value);
                        int secs = int.Parse(match.Groups["secs"].Value);
                        // "frames" or "hundredths" group name depends on which regex matched
                        string frameGroup = match.Groups["frames"].Success ? match.Groups["frames"].Value : match.Groups["hundredths"].Value;
                        int frames = int.Parse(frameGroup);

                        // cdparanoia's time is [MM:SS.FF] where FF is frames (1/75th of a second typically for CD audio)
                        // Convert FF to milliseconds: (FF * 1000 / 75)
                        int milliseconds = (int)Math.Round(frames * 1000.0 / 75.0);
                        milliseconds = Math.Min(milliseconds, 999); // Cap at 999 for TimeSpan

                        tracks.Add(new MacOsTrackInfo(trackNum, new TimeSpan(0, 0, mins, secs, milliseconds)));
                    }
                }
            }

            // If parsing errorOutput yielded no tracks, but exitCode was 0, it's an issue.
            // If exitCode was non-zero, we might have partial info or truly no tracks.
            if (tracks.Count == 0 && exitCode == 0 && !string.IsNullOrWhiteSpace(errorOutput))
            {
                 Console.Error.WriteLine($"cdparanoia -sQ for {devicePath} had exit code 0 but no tracks were parsed. Error output was:\n{errorOutput}");
            }
            else if (tracks.Count == 0 && exitCode !=0)
            {
                 Console.Error.WriteLine($"cdparanoia -sQ for {devicePath} failed and no tracks were parsed. Error output was:\n{errorOutput}");
            }

            return tracks;
        }

        public async Task<bool> RipTrackAsync(string devicePath, int trackNumber, string outputFilePath, Action<double> progressCallback)
        {
            if (string.IsNullOrEmpty(devicePath)) throw new ArgumentNullException(nameof(devicePath));
            if (trackNumber <= 0) throw new ArgumentOutOfRangeException(nameof(trackNumber));
            if (string.IsNullOrEmpty(outputFilePath)) throw new ArgumentNullException(nameof(outputFilePath));

            // Ensure directory for outputFilePath exists
            string outputDir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // cdparanoia rips to .wav by default if output filename doesn't specify other supported format (like .aiff)
            // Arguments: -d <device> <tracknumber> <outputfile>
            // Adding -B for batch mode (no interactive prompts, good for scripting)
            // Adding -Z to disable paranoia checks for faster ripping if desired (remove for higher quality assurance)
            string arguments = $"-B -d \"{devicePath}\" {trackNumber} \"{outputFilePath}\"";

            // Regex for cdparanoia progress from stderr.
            // Example: "output_千切り.cdda.wav: 05.3% done, ETA 00:04:12." (percentage)
            // Or: "Ripping from sector  16200 (03:36) /   38324 (08:30)   (42%)" (sector based)
            var percentProgressRegex = new Regex(@"(\d{1,2}\.\d{1,2})\s*%\s*done"); // Matches "XX.Y % done" or "XX.YY % done"
            var sectorProgressRegex = new Regex(@"sector\s+(?<current>\d+)\s+\([\d:.]+\)\s+/\s+(?<total>\d+)\s+\((?<percent>\d+)%\)");


            Action<string> errorHandler = (errorLine) =>
            {
                if (progressCallback != null)
                {
                    Match percentMatch = percentProgressRegex.Match(errorLine);
                    if (percentMatch.Success)
                    {
                        // CultureInfo.InvariantCulture is important for parsing decimals like "05.3"
                        if (double.TryParse(percentMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double percent))
                        {
                            progressCallback(percent / 100.0);
                        }
                        return;
                    }

                    Match sectorMatch = sectorProgressRegex.Match(errorLine);
                    if (sectorMatch.Success)
                    {
                         // Use the percentage from sector mode if available, as it's more direct
                        if (double.TryParse(sectorMatch.Groups["percent"].Value, CultureInfo.InvariantCulture, out double percentVal))
                        {
                            progressCallback(percentVal / 100.0);
                        }
                        else // Fallback to calculating from sectors if percent group isn't there (shouldn't happen with this regex)
                        {
                            double current = double.Parse(sectorMatch.Groups["current"].Value, CultureInfo.InvariantCulture);
                            double total = double.Parse(sectorMatch.Groups["total"].Value, CultureInfo.InvariantCulture);
                            if (total > 0)
                            {
                                 progressCallback(current / total);
                            }
                        }
                        return;
                    }
                }
                // Non-progress lines on stderr could be actual errors or informational messages.
                // Console.WriteLine($"cdparanoia stderr: {errorLine}"); // Optional: log non-progress stderr
            };

            var (exitCode, output, errorText) = await RunProcessAsync(arguments, errorHandler);

            if (exitCode != 0)
            {
                Console.Error.WriteLine($"cdparanoia rip for track {trackNumber} on {devicePath} failed. Exit Code: {exitCode}\nStdOut: {output}\nStdErr: {errorText}");
                progressCallback?.Invoke(0.0); // Reset progress on failure
                return false;
            }

            // Ensure the file was actually created and has size, as cdparanoia might exit 0 on some "soft" errors.
            if (!File.Exists(outputFilePath) || new FileInfo(outputFilePath).Length == 0)
            {
                Console.Error.WriteLine($"cdparanoia rip for track {trackNumber} on {devicePath} reported success (exit code 0), but output file '{outputFilePath}' is missing or empty.");
                Console.Error.WriteLine($"StdOut: {output}\nStdErr: {errorText}");
                progressCallback?.Invoke(0.0);
                return false;
            }

            progressCallback?.Invoke(1.0); // Ensure completion progress is reported
            return true;
        }

        // Methods not implemented by cdparanoia, expected to be handled by DrUtilWrapper
        public Task<List<MacOsDriveInfo>> ListAudioCdDrivesAsync()
        {
            Console.WriteLine("[INFO] ListAudioCdDrivesAsync is not implemented by CdparanoiaWrapper. Use DrUtilWrapper for listing drives.");
            return Task.FromResult(new List<MacOsDriveInfo>());
        }

        public Task<bool> EjectDriveAsync(string devicePath)
        {
            Console.WriteLine("[INFO] EjectDriveAsync is not implemented by CdparanoiaWrapper. Use DrUtilWrapper for ejecting drives.");
            return Task.FromResult(false);
        }
    }
}
