// <copyright file="ConversionJob_ExtractCDA.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;
    using System.Linq; // Added for LINQ's Take method
    using System.Threading;
    using System.Runtime.InteropServices; // For OSPlatform
    using System.Threading.Tasks;         // For Task

    using Diagnostics;
    using Ripper;
    using WaveLib;
    using Yeti.MMedia;
    using FileConverter.MacOsIntegration; // For IMacOSCdOperations

    public class ConversionJob_ExtractCDA : ConversionJob
    {
        // Windows-specific
        private Ripper.CDDrive diskDrive;
        private WaveWriter waveWriter;

        // macOS-specific
        private IMacOSCdOperations _macOsCdRipper;
        private string _macOsDevicePath;

        // Common
        private int cdaTrackNumber = -1; // Shared, but parsed differently
        private string intermediateFilePath;
        private ConversionJob compressionConversionJob;
        private System.Threading.Thread compressionThread;

        public ConversionJob_ExtractCDA() : base()
        {
        }

        public ConversionJob_ExtractCDA(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
        }

        protected override InputPostConversionAction InputPostConversionAction
        {
            get
            {
                return InputPostConversionAction.None;
            }
        }

        public override void Cancel()
        {
            base.Cancel();

            this.compressionConversionJob.Cancel();
        }

        protected override void Initialize()
        {
            base.Initialize();

            if (this.ConversionPreset == null)
            {
                throw new Exception("The conversion preset must be valid.");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS-specific initialization
                if (!InputFilePath.StartsWith("cda-macos:"))
                {
                    ConversionFailed("Invalid input format for macOS CDA extraction. Expected 'cda-macos:<devicePath>:<trackNumber>'.");
                    return;
                }

                var parts = InputFilePath.Substring("cda-macos:".Length).Split(':');
                // Last part is track number, everything before is joined back for device path
                if (parts.Length < 2 || !int.TryParse(parts[parts.Length - 1], out this.cdaTrackNumber))
                {
                    ConversionFailed($"Could not parse device path and track number for macOS CDA extraction from '{InputFilePath}'.");
                    return;
                }

                _macOsDevicePath = string.Join(":", parts.Take(parts.Length - 1));

                if (string.IsNullOrEmpty(_macOsDevicePath) || this.cdaTrackNumber <= 0)
                {
                     ConversionFailed($"Invalid device path ('{_macOsDevicePath}') or track number ({this.cdaTrackNumber}) for macOS CDA extraction.");
                     return;
                }
                // _macOsCdRipper (CdparanoiaWrapper) will be instantiated in Convert() for macOS
            }
            else // Windows or other non-OSX platforms (current Ripper.dll logic is Windows-specific)
            {
                // Windows-specific initialization using Ripper.dll
                string pathDriveLetter = PathHelpers.GetPathDriveLetter(this.InputFilePath);
                if (pathDriveLetter.Length == 0)
                {
                    this.ConversionFailed(Properties.Resources.ErrorFailToRetrieveInputPathDriveLetter);
                    return;
                }

                char driveLetter = pathDriveLetter[0];

                this.diskDrive = new Ripper.CDDrive();
                this.diskDrive.CDRemoved += new EventHandler(this.CdDriveCdRemoved); // Event handler only for Windows

                bool driveLetterFound = false;
                char[] driveLetters = Ripper.CDDrive.GetCDDriveLetters();
                for (int index = 0; index < driveLetters.Length; index++)
                {
                    driveLetterFound |= driveLetters[index] == driveLetter;
                }

                if (!driveLetterFound)
                {
                    Debug.Log($"Invalid drive letter {driveLetter}.");
                    this.ConversionFailed(Properties.Resources.ErrorFailToRetrieveInputPathDriveLetter);
                    return;
                }

                // Retrieve and track number using Windows-specific PathHelpers
                try
                {
                    this.cdaTrackNumber = PathHelpers.GetCDATrackNumber(this.InputFilePath);
                }
                catch (Exception)
                {
                    Debug.Log($"Input path: '{this.InputFilePath}'.");
                    this.ConversionFailed(Properties.Resources.ErrorFailToRetrieveTrackNumber);
                    return;
                }

                if (this.diskDrive.IsOpened)
                {
                    this.ConversionFailed(Properties.Resources.ErrorFailToUseCDDriveOpen);
                    return;
                }

                if (!this.diskDrive.Open(driveLetter))
                {
                    this.ConversionFailed(string.Format(Properties.Resources.ErrorFailToReadCDDrive, driveLetter));
                    return;
                }
            }

            // Common initialization for intermediate file path and compression job
            // Generate intermediate file path (always .wav as cdparanoia also outputs wav)
            string fileName = Path.GetFileNameWithoutExtension(this.OutputFilePath); // Use OutputFilePath to derive temp name
            if (string.IsNullOrEmpty(fileName)) fileName = "ripped_track"; // Fallback filename
            string tempPath = Path.GetTempPath();
            this.intermediateFilePath = PathHelpers.GenerateUniquePath(tempPath + fileName + ".wav");

            // Sub conversion job (for compression).
            this.compressionConversionJob = ConversionJobFactory.Create(this.ConversionPreset, this.intermediateFilePath);
            this.compressionConversionJob.PrepareConversion(this.OutputFilePath);
            this.compressionThread = Helpers.InstantiateThread("CDACompressionThread", this.CompressAsync);
        }

        protected override void Convert()
        {
            if (this.ConversionPreset == null)
            {
                throw new Exception("The conversion preset must be valid.");
            }

            Debug.Log("Starting CDA extraction.");
            this.UserState = Properties.Resources.ConversionStateExtraction;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS ripping logic
                _macOsCdRipper = new CdparanoiaWrapper(); // Or get from DI if set up
                Action<double> progressCallback = p => { this.Progress = (float)p; };

                Debug.Log($"Ripping track {this.cdaTrackNumber} from {_macOsDevicePath} to {this.intermediateFilePath} on macOS.");

                // Synchronously waiting for the async task for now, as per subtask note.
                // This will block the calling thread.
                bool ripSuccess = _macOsCdRipper.RipTrackAsync(_macOsDevicePath, this.cdaTrackNumber, this.intermediateFilePath, progressCallback)
                                                .GetAwaiter().GetResult();

                if (!ripSuccess)
                {
                    this.ConversionFailed(Properties.Resources.ErrorCDAExtractionFailed + " (macOS)");
                    return;
                }
            }
            else // Windows or other non-OSX platforms
            {
                // Windows ripping logic using Ripper.dll
                if (!this.diskDrive.IsCDReady())
                {
                    this.ConversionFailed(Properties.Resources.ErrorCDDriveNotReady);
                    return;
                }

                if (!this.diskDrive.Refresh())
                {
                    Debug.Log("Can't refresh CD drive data.");
                    this.ConversionFailed(Properties.Resources.ErrorCDDriveNotReady);
                    return;
                }

                if (!this.diskDrive.LockCD())
                {
                    Debug.Log("Can't lock cd.");
                    this.ConversionFailed(Properties.Resources.ErrorCDDriveNotReady);
                    return;
                }

                WaveFormat waveFormat = new WaveFormat(44100, 16, 2);

                using (Stream waveStream = new FileStream(this.intermediateFilePath, FileMode.Create, FileAccess.Write))
                using (this.waveWriter = new WaveWriter(waveStream, waveFormat, this.diskDrive.TrackSize(this.cdaTrackNumber)))
                {
                    this.diskDrive.ReadTrack(this.cdaTrackNumber, this.WriteWaveData, this.CdReadProgress);
                }

                this.waveWriter = null; // Release for subsequent operations

                this.diskDrive.UnLockCD();
                this.diskDrive.Close();
            }

            this.StateFlags = ConversionFlags.None; // Reset flags after platform-specific block

            if (!File.Exists(this.intermediateFilePath)) // Common check
            {
                this.ConversionFailed(Properties.Resources.ErrorCDAExtractionFailed);
                return;
            }

            Debug.Log($"CDA extracted to {this.intermediateFilePath}.");
            Debug.Log(string.Empty);
            Debug.Log("Start compression.");

            this.UserState = Properties.Resources.ConversionStateConversion;

            this.compressionThread.Start();

            while (this.compressionConversionJob.State != ConversionState.Done &&
                this.compressionConversionJob.State != ConversionState.Failed)
            {
                this.Progress = this.compressionConversionJob.Progress;
            }

            if (this.compressionConversionJob.State == ConversionState.Failed)
            {
                this.ConversionFailed(this.compressionConversionJob.ErrorMessage);
                return;
            }

            Debug.Log(string.Empty);
            Debug.Log($"Delete intermediate file {this.intermediateFilePath}.");

            File.Delete(this.intermediateFilePath);
        }

        private void WriteWaveData(object sender, DataReadEventArgs eventArgs)
        {
            this.waveWriter?.Write(eventArgs.Data, 0, (int)eventArgs.DataSize);
        }

        private void CdReadProgress(object sender, ReadProgressEventArgs eventArgs)
        {
            if (this.CancelIsRequested)
            {
                eventArgs.CancelRead = true;
                return;
            }

            this.Progress = (float)eventArgs.BytesRead / (float)eventArgs.Bytes2Read;

            eventArgs.CancelRead |= this.State != ConversionState.InProgress;
        }

        private void CdDriveCdRemoved(object sender, System.EventArgs eventArgs)
        {
            this.ConversionFailed("The CD has been ejected.");
        }

        private void CompressAsync()
        {
            this.compressionConversionJob.StartConversion();
        }
    }
}
