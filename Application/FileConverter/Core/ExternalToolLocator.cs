// Application/FileConverter/Core/ExternalToolLocator.cs
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FileConverter.Core
{
    public static class ExternalToolLocator
    {
        private static string _appBundleResourcesPath = null;

        // This method should be called by the Xamarin.Mac application startup
        // (e.g., in AppDelegate) to set the correct resources path from the app bundle.
        public static void Initialize(string appBundleResourcesPath)
        {
            _appBundleResourcesPath = appBundleResourcesPath;
            Console.WriteLine($"ExternalToolLocator initialized with AppBundleResourcesPath: {_appBundleResourcesPath}");
        }

        private static string GetBasePath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !string.IsNullOrEmpty(_appBundleResourcesPath))
            {
                // Path provided by Xamarin.Mac app (e.g., NSBundle.MainBundle.ResourcePath)
                // This is the preferred path when running as a bundled macOS app.
                return _appBundleResourcesPath;
            }

            // Fallback for when not in a Xamarin.Mac bundle or if Initialize wasn't called.
            // This will point to the directory of the executing assembly.
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(assemblyLocation)) // Should not happen in normal scenarios
            {
                assemblyLocation = Directory.GetCurrentDirectory(); // Last resort
                Console.WriteLine($"Warning: Assembly location is null, falling back to CurrentDirectory: {assemblyLocation}");
            }


            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && assemblyLocation.Contains(".app/Contents/MacOS"))
            {
                // If running from MacOS folder (e.g., during development or non-bundled execution),
                // try to navigate to a typical Resources folder structure.
                // A common structure for bundled tools might be Contents/Resources/ExternalTools
                string resourcesPath = Path.GetFullPath(Path.Combine(assemblyLocation, "../Resources"));
                if (Directory.Exists(resourcesPath))
                {
                    return resourcesPath;
                }
            }
            // For Windows or non-bundled macOS, or if ../Resources doesn't exist from MacOS dir,
            // using the assembly location itself is a common approach for tools placed alongside the exe.
            return assemblyLocation;
        }

        public static string GetFFmpegPath()
        {
            string basePath = GetBasePath();
            string ffmpegExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

            // Search order:
            // 1. [BasePath]/ExternalTools/ffmpeg/[bin/]ffmpeg (common for complex toolchains)
            // 2. [BasePath]/ExternalTools/ffmpeg (simpler bundling)
            // 3. [BasePath]/ffmpeg (tool directly alongside main app/library)
            // 4. Fallback to PATH

            string[] possibleRelativePaths = {
                Path.Combine("ExternalTools", "ffmpeg", "bin", ffmpegExecutableName), // e.g., ExternalTools/ffmpeg/bin/ffmpeg
                Path.Combine("ExternalTools", "ffmpeg", ffmpegExecutableName),      // e.g., ExternalTools/ffmpeg
                ffmpegExecutableName                                             // e.g., ffmpeg (directly in BasePath)
            };

            foreach (var relPath in possibleRelativePaths)
            {
                string fullPath = Path.Combine(basePath, relPath);
                if (File.Exists(fullPath))
                {
                    Console.WriteLine($"FFmpeg found at: {fullPath}");
                    return fullPath;
                }
            }

            Console.WriteLine($"Warning: FFmpeg not found in bundled paths relative to '{basePath}'. Assuming '{ffmpegExecutableName}' is in system PATH.");
            return ffmpegExecutableName; // Fallback: assume it's in PATH
        }

        public static string GetGhostscriptDirectory()
        {
            // Magick.NET typically needs the directory that contains the 'bin' (with 'gs') and 'lib' subdirectories for Ghostscript.
            // Assumed structure: [BasePath]/ExternalTools/ghostscript/
            // where 'bin/gs' (or 'bin/gs.exe') and 'lib/' reside.
            string basePath = GetBasePath();
            string gsBaseDir = Path.Combine(basePath, "ExternalTools", "ghostscript");
            string gsExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gswin64c.exe" : "gs"; // Or gswin32c.exe

            // Check if the 'gs' executable exists within a 'bin' subdirectory of the presumed gsBaseDir
            if (File.Exists(Path.Combine(gsBaseDir, "bin", gsExecutableName)))
            {
                Console.WriteLine($"Ghostscript found in: {gsBaseDir} (via bin/{gsExecutableName})");
                return gsBaseDir; // MagickNET.SetGhostscriptDirectory usually wants this base directory
            }
            // Some simpler bundles might place 'gs' directly in gsBaseDir
            if (File.Exists(Path.Combine(gsBaseDir, gsExecutableName)))
            {
                 Console.WriteLine($"Ghostscript found in: {gsBaseDir} (executable directly in folder)");
                return gsBaseDir;
            }

            Console.WriteLine($"Warning: Ghostscript directory structure not found at '{gsBaseDir}'. Magick.NET might fail to find '{gsExecutableName}'. Assuming it's in system PATH or Magick.NET has other means.");
            // If not found in the expected bundled location, Magick.NET will try to find 'gs' in the system PATH.
            // Returning null or string.Empty would force Magick.NET to rely solely on system PATH.
            // Returning the expected path (even if not fully validated here) allows Magick.NET to try it.
            return string.Empty; // Let Magick.NET search PATH if bundled not found.
        }
    }
}
