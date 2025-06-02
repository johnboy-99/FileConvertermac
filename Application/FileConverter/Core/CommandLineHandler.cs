// Application/FileConverter/Core/CommandLineHandler.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection; // Required for IServiceProvider

using FileConverter.ConversionJobs;
using FileConverter.Services;
using FileConverter.Diagnostics; // For Debug
// FileConverter.ViewModels and FileConverter.Views are not needed here as this is for backend logic.

namespace FileConverter.Core
{
    public class CommandLineArgumentException : Exception
    {
        public CommandLineArgumentException(string message) : base(message) { }
    }

    public class CommandLineHandler
    {
        // Comment: Service registration (ServiceCollection, Ioc.Default.ConfigureServices)
        // and DI container building should occur in the actual application entry point
        // (e.g., Xamarin.Mac AppDelegate.DidFinishLaunching or Program.cs for a console app).
        // This handler expects an already configured IServiceProvider.

        public async Task HandleArgumentsAsync(string[] args, IServiceProvider serviceProvider)
        {
            // TODO: macOS lifecycle events (like termination warnings) should be handled by AppDelegate
            // and communicated to ViewModels if necessary via a shared service or events.
            // This handler focuses on command-line initiated backend tasks.

            // Retrieve required services
            var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            var conversionService = serviceProvider.GetRequiredService<IConversionService>();
            var upgradeService = serviceProvider.GetRequiredService<IUpgradeService>();
            // var navigationService = serviceProvider.GetRequiredService<INavigationService>(); // Not used directly for backend tasks

            if (settingsService.Settings == null)
            {
                // This check was originally in RunConversions, good to have it early if settings are critical.
                // However, some args like --version might not need settings.
                // For now, keeping it simple. If settings are essential for most operations, this is fine.
                Debug.LogError(errorCode: 0x04, "File Converter settings could not be loaded. Please check configuration.");
                // Consider throwing an exception or having a specific return type/enum for status.
                return;
            }

            // Log version (similar to original Initialize)
#if BUILD32 // This preprocessor might not be defined in .NET 8 SDK-style unless explicitly set.
            Debug.Log("File Converter Core v" + FileConverter.Version.CurrentVersion.ToString() + " (32 bits processing path)");
#else
            Debug.Log("File Converter Core v" + FileConverter.Version.CurrentVersion.ToString() + " (64 bits processing path)");
#endif

            Debug.Log("Processing arguments...");
            for (int i = 0; i < args.Length; i++) // Start from 0 if actual app args, 1 if Environment.GetCommandLineArgs()
            {
                Debug.Log($"Arg{i}: {args[i]}");
            }
            Debug.Log(string.Empty);

            if (args.Length == 0 || (args.Length == 1 && args[0] == System.Reflection.Assembly.GetEntryAssembly()?.Location))
            {
                // No actual arguments provided beyond executable name.
                // Original app showed help. Here, we might log or do nothing for a library.
                // For a CLI tool, this might show help. For library use, this might be normal.
                Debug.Log("No command-line arguments provided for backend handler (excluding executable path if present).");
                // If this handler is meant to be called only when there ARE args, this could be an error.
                return;
            }

            List<string> filePaths = new List<string>();
            string conversionPresetName = null;
            bool verbose = false;
            // bool showSettings = false; // UI concern
            // bool showHelp = false; // UI concern

            // Argument parsing (adapted from Application.xaml.cs Initialize)
            // Assuming 'args' passed here are the actual arguments, not Environment.GetCommandLineArgs()
            // which includes the executable path as the first element.
            // If args[0] IS the exe path, loop should start from 1.
            // For robustness, let's assume args are purely arguments after the exe name.
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (string.IsNullOrEmpty(argument)) continue;

                if (argument.StartsWith("--"))
                {
                    string parameterTitle = argument.Substring(2).ToLowerInvariant();
                    switch (parameterTitle)
                    {
                        // Windows-specific shell extension commands are excluded.
                        // --post-install-init is also excluded (Windows installer specific).

                        case "version":
                            // For a library, writing to console might be okay, or it could return/log.
                            Console.WriteLine(FileConverter.Version.CurrentVersion.ToString());
                            // Original app would shutdown. Here, we just return as the task is done.
                            return;

                        // case "settings": // UI concern, removed
                        //     showSettings = true;
                        //     break;

                        case "conversion-preset":
                            if (index >= args.Length - 1)
                            {
                                Debug.LogError(errorCode: 0x01, "Invalid format for --conversion-preset: Missing preset name.");
                                throw new CommandLineArgumentException("Missing preset name for --conversion-preset.");
                            }
                            conversionPresetName = args[index + 1];
                            index++; // Consume the preset name argument
                            break;

                        case "input-files":
                            if (index >= args.Length - 1)
                            {
                                Debug.LogError(errorCode: 0x02, "Invalid format for --input-files: Missing file list path.");
                                throw new CommandLineArgumentException("Missing file list path for --input-files.");
                            }
                            string fileListPath = args[index + 1];
                            try
                            {
                                using (FileStream file = File.OpenRead(fileListPath))
                                using (StreamReader reader = new StreamReader(file))
                                {
                                    while (!reader.EndOfStream)
                                    {
                                        filePaths.Add(reader.ReadLine());
                                    }
                                }
                            }
                            catch (Exception exception)
                            {
                                Debug.LogError(errorCode: 0x03, $"Can't read input files list '{fileListPath}': {exception.Message}");
                                throw new CommandLineArgumentException($"Error reading input files list: {exception.Message}");
                            }
                            index++; // Consume the file list path argument
                            break;

                        case "verbose":
                            verbose = true; // Verbose logging might be handled by a logging service configured elsewhere.
                                            // For now, just noting it. UI might show diagnostics.
                            Debug.Log("Verbose mode requested by command line.");
                            break;

                        default:
                            Debug.LogError($"Unknown application argument: '--{parameterTitle}'.");
                            // Consider throwing for unknown critical arguments.
                            break;
                    }
                }
                else
                {
                    // Argument is not starting with --, assume it's an input file path directly.
                    filePaths.Add(argument);
                }
            }

            // Logic from RunConversions
            if (settingsService.Settings == null) // Double check, though done at start
            {
                Debug.LogError(errorCode: 0x04, "File Converter settings are not loaded. Cannot run conversions.");
                throw new InvalidOperationException("Settings not loaded, cannot run conversions.");
            }

            // Check for upgrade (backend part)
            if (settingsService.Settings.CheckUpgradeAtStartup)
            {
                // The original event handler showed UI. Here, we might just log or allow service to handle notifications differently.
                // upgradeService.NewVersionAvailable += SomeBackendUpgradeNotificationHandler; // If needed
                await upgradeService.CheckForUpgrade(); // Perform the check
            }

            if (!string.IsNullOrEmpty(conversionPresetName) && filePaths.Any())
            {
                ConversionPreset conversionPreset = settingsService.Settings.GetPresetFromName(conversionPresetName);
                if (conversionPreset == null)
                {
                    Debug.LogError(errorCode: 0x02, $"Invalid conversion preset name '{conversionPresetName}'.");
                    throw new CommandLineArgumentException($"Invalid conversion preset name: {conversionPresetName}");
                }

                Debug.Log($"Creating jobs for conversion preset: '{conversionPreset.FullName}' for {filePaths.Count} files.");
                try
                {
                    foreach (string inputFilePath in filePaths)
                    {
                        if (!File.Exists(inputFilePath) && !inputFilePath.StartsWith("cda-macos:")) // cda-macos are special identifiers
                        {
                             Debug.LogError($"Input file not found: {inputFilePath}. Skipping.");
                             continue; // Skip non-existent files
                        }
                        ConversionJob conversionJob = ConversionJobFactory.Create(conversionPreset, inputFilePath);
                        conversionService.RegisterConversionJob(conversionJob);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Error creating conversion jobs: {exception.Message}");
                    throw; // Rethrow if job creation itself fails critically
                }

                if (conversionService.ConversionJobs.Any())
                {
                    // The original OnStartup then showed Main page and started conversions.
                    // Here, we just start the conversions. The calling host handles UI.
                    // ConversionService_ConversionJobsTerminated and auto-exit logic is removed.
                    // The host application will decide what to do after conversions (e.g., by awaiting this task or using callbacks).
                    await Task.Run(() => conversionService.ConvertFilesAsync()); // Assuming ConvertFilesAsync starts a thread and returns.
                                                                                // If ConvertFilesAsync is truly async, it can be awaited directly.
                                                                                // For this refactor, keeping it similar to original threading model.
                    Debug.Log("Conversion tasks initiated by command line handler.");
                }
                else
                {
                    Debug.Log("No valid conversion jobs were created from the provided arguments.");
                }
            }
            else if (!string.IsNullOrEmpty(conversionPresetName) && !filePaths.Any())
            {
                Debug.LogWarning("Conversion preset specified, but no input files provided.");
            }
            // Other cases (e.g., only file paths, no preset) are implicitly handled by not creating jobs.
        }
    }
}
