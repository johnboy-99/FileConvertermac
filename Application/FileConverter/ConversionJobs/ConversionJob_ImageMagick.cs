// <copyright file="ConversionJob_ImageMagick.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO; // Required for Path.GetExtension, Path.Combine, Directory.Exists
    using FileConverter.Diagnostics;
    using ImageMagick;
    using FileConverter.Core; // Added for ExternalToolLocator

    public class ConversionJob_ImageMagick : ConversionJob
    {
        private const float BaseDpiForPdfConversion = 200f;
        private const int PdfSuperSamplingRatio = 1;

        private bool isInputFilePdf;
        private int pageCount;

        public ConversionJob_ImageMagick() : base()
        {
        }

        public ConversionJob_ImageMagick(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
        }

        protected override void Initialize()
        {
            base.Initialize();

            string gsDirectory = ExternalToolLocator.GetGhostscriptDirectory();
            if (!string.IsNullOrEmpty(gsDirectory) && Directory.Exists(gsDirectory))
            {
                MagickNET.SetGhostscriptDirectory(gsDirectory);
                Diagnostics.Debug.Log($"Set Ghostscript directory for Magick.NET to: {gsDirectory}");

                // EXPERIMENTAL: Attempt to set GS_LIB for bundled Ghostscript if 'lib' subdir exists
                // This helps Ghostscript find its own resources (fonts, etc.) when bundled.
                string gsLibPath = Path.Combine(gsDirectory, "lib");
                if (Directory.Exists(gsLibPath))
                {
                    string currentGsLib = Environment.GetEnvironmentVariable("GS_LIB");
                    string newGsLib = gsLibPath;
                    if (!string.IsNullOrEmpty(currentGsLib))
                    {
                        // Prepend our path, so it's searched first, but include existing paths.
                        newGsLib = $"{gsLibPath}{Path.PathSeparator}{currentGsLib}";
                    }
                    Environment.SetEnvironmentVariable("GS_LIB", newGsLib);
                    Diagnostics.Debug.Log($"Set GS_LIB to: {newGsLib}");
                }
                else
                {
                    Diagnostics.Debug.Log($"Ghostscript 'lib' subdirectory not found at '{gsLibPath}'. GS_LIB not set by FileConverter.");
                }
            }
            else
            {
                // If ExternalToolLocator returns empty or invalid path, Magick.NET will try to find Ghostscript in system PATH.
                Diagnostics.Debug.LogWarning("Ghostscript directory from ExternalToolLocator is not valid or not found. ImageMagick PDF/PS processing may fail if Ghostscript is not in system PATH.");
            }

            this.isInputFilePdf = Path.GetExtension(this.InputFilePath).ToLowerInvariant() == ".pdf";

            if (this.ConversionPreset == null)
            {
                throw new Exception("The conversion preset must be valid.");
            }
        }

        protected override int GetOutputFilesCount()
        {
            if (System.IO.Path.GetExtension(this.InputFilePath).ToLowerInvariant() == ".pdf")
            {
                // This part might fail if Ghostscript is not properly configured and found by Magick.NET
                try
                {
                    using (MagickImageCollection images = new MagickImageCollection())
                    {
                        MagickReadSettings settings = new MagickReadSettings();
                        settings.Density = new Density(1, 1); // Use low density for quick page count
                        images.Read(this.InputFilePath, settings);
                        return images.Count > 0 ? images.Count : 1; // Ensure at least 1 if file exists
                    }
                }
                catch (Exception ex)
                {
                    Diagnostics.Debug.LogError($"Failed to read PDF for page count, possibly due to Ghostscript issue: {ex.Message}");
                    return 1; // Fallback to 1 page if reading fails
                }
            }
            return 1;
        }

        protected override void Convert()
        {
            if (this.ConversionPreset == null)
            {
                throw new Exception("The conversion preset must be valid.");
            }

            this.CurrentOutputFilePathIndex = 0;

            if (this.isInputFilePdf)
            {
                this.ConvertPdf();
            }
            else
            {
                this.pageCount = 1;
                MagickReadSettings readSettings = new MagickReadSettings();

                string inputExtension = Path.GetExtension(this.InputFilePath).ToLowerInvariant();
                switch (inputExtension)
                {
                    case ".cr2":
                        readSettings.Format = MagickFormat.Cr2;
                        break;
                    case ".dng":
                        readSettings.Format = MagickFormat.Dng;
                        break;
                    case ".gif":
                        readSettings.FrameIndex = 0; // Get the first frame
                        readSettings.FrameCount = 1; // Process only one frame
                        break;
                    default:
                        break;
                }

                try
                {
                    using (MagickImage image = new MagickImage(this.InputFilePath, readSettings))
                    {
                        Debug.Log($"Load image {this.InputFilePath} succeed.");
                        this.ConvertImage(image);
                    }
                }
                catch (MagickException ex)
                {
                    Debug.LogError($"ImageMagick error processing non-PDF file '{this.InputFilePath}': {ex.Message}");
                    this.ConversionFailed($"ImageMagick error: {ex.Message}");
                }
            }
        }

        private void ConvertPdf()
        {
            MagickReadSettings settings = new MagickReadSettings();

            float dpi = BaseDpiForPdfConversion;
            if (this.ConversionPreset.IsRelevantSetting(ConversionPreset.ConversionSettingKeys.ImageScale))
            {
                float scaleFactor = this.ConversionPreset.GetSettingsValue<float>(ConversionPreset.ConversionSettingKeys.ImageScale);
                if (Math.Abs(scaleFactor - 1f) >= 0.005f)
                {
                    Debug.Log($"Apply scale factor: {scaleFactor * 100}%.");
                    dpi *= scaleFactor;
                }
            }


            Debug.Log($"Density for PDF reading: {dpi}dpi.");
            settings.Density = new Density(dpi * PdfSuperSamplingRatio);

            this.UserState = Properties.Resources.ConversionStateReadDocument;

            try
            {
                using (MagickImageCollection images = new MagickImageCollection())
                {
                    images.Read(this.InputFilePath, settings);
                    Debug.Log($"Load pdf {this.InputFilePath} succeed. Page count: {images.Count}");

                    this.pageCount = images.Count;
                    if (this.pageCount == 0) { // Should not happen if Read succeeds, but as safeguard
                        this.ConversionFailed("PDF file contained no pages or could not be read correctly.");
                        return;
                    }


                    this.UserState = Properties.Resources.ConversionStateConversion;

                    foreach (MagickImage image in images)
                    {
                        Debug.Log($"Write page {this.CurrentOutputFilePathIndex + 1}/{this.pageCount}.");

                        if (PdfSuperSamplingRatio > 1)
                        {
                            image.Scale(new Percentage(100.0 / PdfSuperSamplingRatio));
                        }
                        this.ConvertImage(image, true); // ignoreScale is true because PDF scaling is done via Density
                        this.CurrentOutputFilePathIndex++;
                    }
                }
            }
            catch (MagickException ex)
            {
                Debug.LogError($"ImageMagick error processing PDF '{this.InputFilePath}': {ex.Message}");
                this.ConversionFailed($"ImageMagick (PDF) error: {ex.Message}");
            }
        }

        private void ConvertImage(MagickImage image, bool ignoreScale = false)
        {
            image.Progress += this.Image_Progress;

            try
            {
                if (!ignoreScale && this.ConversionPreset.IsRelevantSetting(ConversionPreset.ConversionSettingKeys.ImageScale))
                {
                    float scaleFactor = this.ConversionPreset.GetSettingsValue<float>(ConversionPreset.ConversionSettingKeys.ImageScale);
                    if (Math.Abs(scaleFactor - 1f) >= 0.005f)
                    {
                        Debug.Log($"Apply scale factor: {scaleFactor * 100}%.");
                        image.Scale(new Percentage(scaleFactor * 100f));
                    }
                }

                if (this.ConversionPreset.IsRelevantSetting(ConversionPreset.ConversionSettingKeys.ImageRotation))
                {
                    float rotateAngleInDegrees = this.ConversionPreset.GetSettingsValue<float>(ConversionPreset.ConversionSettingKeys.ImageRotation);
                    if (Math.Abs(rotateAngleInDegrees - 0f) >= 0.05f)
                    {
                        Debug.Log($"Apply rotation: {rotateAngleInDegrees}°.");
                        image.Rotate(rotateAngleInDegrees);
                    }
                }

                if (this.ConversionPreset.IsRelevantSetting(ConversionPreset.ConversionSettingKeys.ImageClampSizePowerOf2))
                {
                    bool clampSizeToPowerOf2 = this.ConversionPreset.GetSettingsValue<bool>(ConversionPreset.ConversionSettingKeys.ImageClampSizePowerOf2);
                    if (clampSizeToPowerOf2)
                    {
                        uint referenceSize = (uint)System.Math.Min(image.Width, image.Height);
                        uint size = 2;
                        while (size * 2 <= referenceSize && size * 2 > size) // Check for overflow with size * 2 > size
                        {
                            size *= 2;
                        }
                        Debug.Log($"Clamp size to the nearest power of 2 size (from {image.Width}x{image.Height} to {size}x{size}).");
                        image.Scale(new MagickGeometry((int)size, (int)size)); // Resize, not just scale percentage
                    }
                }

                if (this.ConversionPreset.IsRelevantSetting(ConversionPreset.ConversionSettingKeys.ImageMaximumSize))
                {
                    uint maximumSize = this.ConversionPreset.GetSettingsValue<uint>(ConversionPreset.ConversionSettingKeys.ImageMaximumSize);
                    if (maximumSize > 0)
                    {
                        uint width = (uint)System.Math.Min(image.Width, maximumSize);
                        uint height = (uint)System.Math.Min(image.Height, maximumSize);
                        if (image.Width > width || image.Height > height) // Only scale if larger
                        {
                             Debug.Log($"Clamp size to maximum size of {width}x{height} (from {image.Width}x{image.Height}).");
                             image.Scale(new MagickGeometry((int)width, (int)height)); // Resize, not just scale percentage
                        }
                    }
                }

                Debug.Log($"Convert image (output: {this.OutputFilePath}).");
                switch (this.ConversionPreset.OutputType)
                {
                    case OutputType.Png:
                        image.Format = MagickFormat.Png; // Ensure output format
                        image.Quality = 95; // Example for PNG, though it's mostly about compression level
                        break;
                    case OutputType.Jpg:
                        image.Format = MagickFormat.Jpg;
                        image.Quality = this.ConversionPreset.GetSettingsValue<uint>(ConversionPreset.ConversionSettingKeys.ImageQuality);
                        break;
                    case OutputType.Pdf: // Converting an image (or PDF page) to a PDF
                        image.Format = MagickFormat.Pdf;
                        Debug.Log($"Density for PDF output: {BaseDpiForPdfConversion}dpi.");
                        image.Density = new Density(BaseDpiForPdfConversion);
                        break;
                    case OutputType.Webp:
                        image.Format = MagickFormat.WebP;
                        image.Quality = this.ConversionPreset.GetSettingsValue<uint>(ConversionPreset.ConversionSettingKeys.ImageQuality);
                        break;
                    default:
                        this.ConversionFailed(string.Format(Properties.Resources.ErrorUnsupportedOutputFormat, this.ConversionPreset.OutputType));
                        return; // Return after calling ConversionFailed
                }
                image.Write(this.OutputFilePath);
            }
            catch (MagickException ex)
            {
                Debug.LogError($"ImageMagick error during image conversion operation: {ex.Message}");
                this.ConversionFailed($"ImageMagick conversion error: {ex.Message}");
            }
            finally
            {
                 image.Progress -= this.Image_Progress;
            }
        }

        private void Image_Progress(object sender, ProgressEventArgs eventArgs)
        {
            if (this.CancelIsRequested)
            {
                eventArgs.Cancel = true;
                return;
            }
            if (this.pageCount == 0) return; // Avoid division by zero if pageCount isn't set

            float alreadyCompletedPages = this.CurrentOutputFilePathIndex / (float)this.pageCount;
            this.Progress = alreadyCompletedPages + ((float)eventArgs.Progress.ToDouble() / (100f * this.pageCount));
        }
    }
}
