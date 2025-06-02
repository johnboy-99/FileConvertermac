// <copyright file="PathHelpers.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter
{
    using System;
    using System.Collections.Generic;
    using System.IO; // Added for System.IO.Path and System.IO.Directory
    using System.Text;
    using System.Text.RegularExpressions;

    using FileConverter.Diagnostics;

    public static class PathHelpers
    {
        // Windows-specific drive letter regex - commented out for cross-platform compatibility
        // private static Regex driveLetterRegex = new Regex(@"[a-zA-Z]:\\");

        // Modified CDA track number regex to be more generic; actual parsing will need platform-specific logic.
        // This regex now looks for "track" followed by digits, then ".cda", not tied to Windows paths.
        // For non-Windows, a path like "cd://track01" might be used.
        private static Regex cdaTrackNumberRegex = new Regex(@"track([0-9]+)(?:\.cda)?", RegexOptions.IgnoreCase);

        // Windows-specific path validation regex - commented out, use System.IO.Path methods instead
        // private static Regex pathRegex = new Regex(@"^(?:\\\\[^\\/:*?""<>|\r\n]+\\|[a-zA-Z]:\\)(?:[^\\/:*?""<>|\r\n]+\\)*[^\.\\/:*?""<>|\r\n][^\\/:*?""<>|\r\n]*$");

        // Windows-specific filename regex - commented out, use System.IO.Path.GetFileName instead
        // private static Regex filenameRegex = new Regex(@"[^\\]*", RegexOptions.RightToLeft);

        // Windows-specific directory regex - commented out, use System.IO.Path.GetDirectoryName and System.IO.Path.GetPathRoot instead
        // private static Regex directoryRegex = new Regex(@"^(?<drive>\\\\[^\\/:*?""""<>|\r\n]+\\|[A-Za-z]:\\)(?:(?<folders>[^\\]*)\\)*");

        private static Regex dateRegex = new Regex(@"\(d:(?<format>[^)]*)\)");

        // This method is Windows-specific and needs to be refactored for cross-platform.
        // public static bool IsPathDriveLetterValid(string path)
        // {
        //     return PathHelpers.driveLetterRegex.IsMatch(path);
        // }

        // This method is Windows-specific and needs to be refactored for cross-platform.
        // public static string GetPathDriveLetter(string path)
        // {
        //     return PathHelpers.driveLetterRegex.Match(path).Groups[0].Value;
        // }

        public static bool IsOnCDDrive(string path)
        {
            // TODO: Implement cross-platform CD drive detection.
            // On Windows, this relied on drive letters.
            // On macOS/Linux, device mount points or other mechanisms would be needed.
            // For now, returning false as a placeholder.
            if (string.IsNullOrEmpty(path)) return false;

            // Placeholder for Windows-like check until proper implementation
            // string pathDriveLetter = GetPathDriveLetter(path); // This was Windows-specific
            // if (string.IsNullOrEmpty(pathDriveLetter))
            // {
            //     return false;
            // }
            // char driveLetter = pathDriveLetter[0];

            // The Ripper.CDDrive.GetCDDriveLetters() is also Windows-specific.
            // This whole method needs a complete rethink for cross-platform.
            // char[] driveLetters = Ripper.CDDrive.GetCDDriveLetters();
            // for (int index = 0; index < driveLetters.Length; index++)
            // {
            //     if (driveLetters[index] == driveLetter)
            //     {
            //         return true;
            //     }
            // }
            return false;
        }

        public static int GetCDATrackNumber(string path)
        {
            // TODO: This method needs a redesign for cross-platform CDA track identification.
            // Windows uses paths like "D:\Track01.cda". Other OSes might use "cd://track01" or similar.
            // The regex is made more generic but the input 'path' itself will differ.
            if (string.IsNullOrEmpty(path)) return -1; // Or throw an error

            Match match = PathHelpers.cdaTrackNumberRegex.Match(path);
            if (match.Success && match.Groups.Count > 1)
            {
                string stringNumber = match.Groups[1].Value;
                if (int.TryParse(stringNumber, out int trackNumber))
                {
                    return trackNumber;
                }
            }
            // Fallback or error for unrecognized format
            Debug.Log($"Could not parse track number from CDA path: {path}");
            return -1;
        }

        public static bool IsPathValid(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Attempt to get full path to check validity. This doesn't guarantee writability or existence.
                // Path.GetFullPath will throw an exception for invalid path characters on most OSes.
                // For stricter validation (e.g. ensuring it's not a UNC path if not allowed, or specific root),
                // more checks would be needed.
                string fullPath = Path.GetFullPath(path);
                return !string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)); // A valid path should have a filename or be a directory root
            }
            catch (ArgumentException)
            {
                // Path contains invalid characters or is empty.
                return false;
            }
            catch (NotSupportedException)
            {
                // Path is in an invalid format (e.g., contains a colon in the middle on Windows).
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }
            // Other exceptions like SecurityException could also occur.
        }

        // Replaced with System.IO.Path.GetFileName
        // public static string GetFileName(string path)
        // {
        //     MatchCollection matchCollection = PathHelpers.filenameRegex.Matches(path);
        //     Match filenameMatch = matchCollection.Count > 0 ? matchCollection[0] : null;
        //     return filenameMatch?.Groups[0].Value;
        // }

        // This method was Windows-specific (drive letters). Use Path.GetPathRoot for a cross-platform equivalent of getting the root.
        // public static string GetDrive(string path)
        // {
        //     MatchCollection matchCollection = PathHelpers.directoryRegex.Matches(path);
        //     Match match = matchCollection.Count > 0 ? matchCollection[0] : null;
        //
        //     Group matchGroup = match?.Groups["drive"];
        //     return matchGroup?.Captures[0].Value;
        // }

        // Replaced with System.IO.Path.GetDirectoryName and string splitting if needed for individual components.
        // public static IEnumerable<string> GetDirectories(string path)
        // {
        //     MatchCollection matchCollection = PathHelpers.directoryRegex.Matches(path);
        //     Match match = matchCollection.Count > 0 ? matchCollection[0] : null;
        //
        //     Group matchGroup = match?.Groups["folders"];
        //     if (matchGroup == null)
        //     {
        //         yield break;
        //     }
        //
        //     for (int index = 0; index < matchGroup.Captures.Count; index++)
        //     {
        //         yield return matchGroup.Captures[index].Value;
        //     }
        // }

        public static string GenerateUniquePath(string path, params string[] blacklist)
        {
            string baseExtension = System.IO.Path.GetExtension(path);
            string basePath = path.Substring(0, path.Length - baseExtension.Length);
            int index = 2;
            while (System.IO.File.Exists(path) ||
                (blacklist != null && System.Array.Exists(blacklist, match => match == path)))
            {
                path = $"{basePath} ({index}){baseExtension}";
                index++;
            }

            return path;
        }

        public static bool CreateFolders(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Debug.Log("Cannot create folders for an empty file path.");
                return false;
            }

            string directoryName = Path.GetDirectoryName(filePath);

            if (string.IsNullOrWhiteSpace(directoryName))
            {
                // File is in the root or path is just a filename; no directory to create.
                // Or it could be an invalid path. IsPathValid should ideally catch this.
                return true;
            }

            if (Directory.Exists(directoryName))
            {
                return true;
            }

            try
            {
                Directory.CreateDirectory(directoryName); // Creates all intermediate directories
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Can't create directories for path {filePath}. Exception: {ex.Message}");
                return false;
            }
        }

        public static string GenerateFilePathFromTemplate(string inputFilePath, OutputType outputFileExtension, string outputFilePathTemplate, int numberIndex, int numberMax)
        {
            if (string.IsNullOrEmpty(inputFilePath))
            {
                // Consider throwing ArgumentNullException for clearer error handling upstream.
                return "Invalid input file path (argument 0).";
            }

            string inputFilenameWithoutExtension = Path.GetFileNameWithoutExtension(inputFilePath);
            // Ensure inputExtension does not start with a dot, consistent with previous logic.
            string inputExtension = Path.GetExtension(inputFilePath);
            if (!string.IsNullOrEmpty(inputExtension) && inputExtension.StartsWith("."))
            {
                inputExtension = inputExtension.Substring(1);
            }
            inputExtension = inputExtension.ToLowerInvariant(); // Match previous behavior

            string outputExtension = outputFileExtension.ToString().ToLowerInvariant();

            if (string.IsNullOrEmpty(outputFilePathTemplate))
            {
                // Default output path: input path with new extension.
                return Path.ChangeExtension(inputFilePath, "." + outputExtension);
            }

            string parentDirectory = Path.GetDirectoryName(inputFilePath);
            if (parentDirectory == null) // Should not happen for valid full paths but good to check
            {
                parentDirectory = string.Empty; // Or handle as an error
            }

            // Ensure parentDirectory ends with a separator for (path) and (p) replacements if they expect a trailing slash.
            // However, using Path.Combine later is safer.
            // For (path) and (p) that expect a trailing separator, we might need to adjust if Path.GetDirectoryName doesn't provide one.
            // For now, let's assume the template handles this or we use Path.Combine.
            string parentDirectoryWithSeparator = parentDirectory;
            if (!string.IsNullOrEmpty(parentDirectory) && !parentDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                parentDirectoryWithSeparator += Path.DirectorySeparatorChar;
            }


            // Get directory components for (d0), (d1), etc.
            // This needs careful handling for cross-platform paths.
            List<string> directoryParts = new List<string>();
            string currentPathForParts = parentDirectory;
            while (!string.IsNullOrEmpty(currentPathForParts))
            {
                directoryParts.Add(Path.GetFileName(currentPathForParts)); // Gets the last part
                string parentOfCurrent = Path.GetDirectoryName(currentPathForParts);
                if (parentOfCurrent == currentPathForParts) break; // Reached root
                currentPathForParts = parentOfCurrent;
            }
            directoryParts.Reverse(); // To get d0 as outermost after root.

            // Generate output path from template.
            string outputPath = outputFilePathTemplate;

            // (path) and (p) should represent the full directory path of the input file.
            outputPath = outputPath.Replace("(path)", parentDirectoryWithSeparator); // Original logic added separator
            outputPath = outputPath.Replace("(p)", parentDirectoryWithSeparator);    // Original logic added separator

            outputPath = outputPath.Replace("(filename)", inputFilenameWithoutExtension);
            outputPath = outputPath.Replace("(f)", inputFilenameWithoutExtension);
            outputPath = outputPath.Replace("(F)", inputFilenameWithoutExtension.ToUpperInvariant());

            outputPath = outputPath.Replace("(outputext)", outputExtension);
            outputPath = outputPath.Replace("(o)", outputExtension);
            outputPath = outputPath.Replace("(O)", outputExtension.ToUpperInvariant());

            outputPath = outputPath.Replace("(inputext)", inputExtension);
            outputPath = outputPath.Replace("(i)", inputExtension);
            outputPath = outputPath.Replace("(I)", inputExtension.ToUpperInvariant());

            // For special folders, Path.Combine is safer than manual concatenation.
            // The template replacements like (p:d) should result in a directory path.
            // If the template is just "(p:d)(f)", then Path.Combine(myDocumentsFolder, fileName + "." + outputExtension)
            string myDocumentsFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            outputPath = outputPath.Replace("(p:d)", myDocumentsFolder + Path.DirectorySeparatorChar);
            outputPath = outputPath.Replace("(p:documents)", myDocumentsFolder + Path.DirectorySeparatorChar);

            string myMusicFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyMusic);
            outputPath = outputPath.Replace("(p:m)", myMusicFolder + Path.DirectorySeparatorChar);
            outputPath = outputPath.Replace("(p:music)", myMusicFolder + Path.DirectorySeparatorChar);

            string myVideoFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos);
            outputPath = outputPath.Replace("(p:v)", myVideoFolder + Path.DirectorySeparatorChar);
            outputPath = outputPath.Replace("(p:videos)", myVideoFolder + Path.DirectorySeparatorChar);

            string myPictureFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
            outputPath = outputPath.Replace("(p:p)", myPictureFolder + Path.DirectorySeparatorChar);
            outputPath = outputPath.Replace("(p:pictures)", myPictureFolder + Path.DirectorySeparatorChar);

            // Replace directory components (d0, d1, etc.)
            // (d0) is the directory closest to the root, (d<n-1>) is closest to the file.
            // Original logic was (d{directories.Length - index - 1}), which meant d0 was the one closest to file.
            // Let's keep original logic for (d<N>) meaning: (d0) is parent, (d1) is grandparent etc.
            for (int i = 0; i < directoryParts.Count; i++)
            {
                // directories[index] in original was (d<length-1-index>)
                // If directories = ["Users", "Name", "Documents"], d0="Documents", d1="Name", d2="Users"
                // Our directoryParts is ["Users", "Name", "Documents"]
                // So, (d_i) from original logic maps to directoryParts[directoryParts.Count - 1 - i]
                if (directoryParts.Count - 1 - i >= 0)
                {
                    string dirPart = directoryParts[directoryParts.Count - 1 - i];
                    outputPath = outputPath.Replace($"(d{i})", dirPart);
                    outputPath = outputPath.Replace($"(D{i})", dirPart.ToUpperInvariant());
                }
            }

            outputPath = outputPath.Replace("(n:i)", numberIndex.ToString());
            outputPath = outputPath.Replace("(n:c)", numberMax.ToString());

            outputPath = dateRegex.Replace(outputPath, match => DateTime.Now.ToString(match.Groups["format"].Value).Replace(Path.DirectorySeparatorChar, '-').Replace(':', '\''));

            // Ensure the final path uses correct separators if constructed from parts,
            // and then append the extension.
            // However, if the template itself is supposed to define the full structure including extension:
            if (!outputPath.EndsWith("." + outputExtension, StringComparison.OrdinalIgnoreCase))
            {
                 outputPath += "." + outputExtension;
            }

            // Normalize the path to ensure correct directory separators for the current OS
            // and resolve any relative path segments if the template could produce them (e.g. "..\folder").
            try
            {
                // Check if outputPath is already a full path resulting from template e.g. (p:d)
                if (!Path.IsPathRooted(outputPath) && !string.IsNullOrEmpty(parentDirectory))
                {
                    // If not rooted, assume it's relative to the input file's directory or a special folder.
                    // This part is tricky as template could be "(f)" meaning filename in current dir,
                    // or could be "C:\output\(f)".
                    // If special folders like (p:d) are used, they provide an absolute base.
                    // If (p) or (path) is used, it's also absolute.
                    // If only (f) is used, it might be intended for current working directory or input's dir.
                    // The original code didn't explicitly anchor (f)-only templates.
                    // For safety, if not rooted and parentDirectory is known, combine with it.
                    // This might need adjustment based on expected template behavior.
                    // Let's assume if not rooted by template, it's relative to input file's dir.
                    if (Path.IsPathRooted(inputFilePath)) // only if inputFilePath was absolute
                    {
                        outputPath = Path.Combine(parentDirectory, outputPath);
                    }
                }
                return Path.GetFullPath(outputPath); // Normalizes separators and resolves relative paths.
            }
            catch (Exception ex)
            {
                Debug.Log($"Error normalizing generated path '{outputPath}': {ex.Message}");
                // Return the potentially problematic path, or handle error as appropriate.
                return outputPath;
            }
        }
    }
}
