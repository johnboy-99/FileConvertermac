// <copyright file="ConversionJob_PowerPoint.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using FileConverter.Diagnostics;

#if NETFRAMEWORK
    using Microsoft.Office.Core;
    using PowerPoint = Microsoft.Office.Interop.PowerPoint;
#endif

    public class ConversionJob_PowerPoint : ConversionJob_Office
    {
#if NETFRAMEWORK
        private PowerPoint.Presentation document;
        private PowerPoint.Application application;
#endif
        private string intermediateFilePath = string.Empty;
        private ConversionJob pdf2ImageConversionJob = null;

        public ConversionJob_PowerPoint() : base()
        {
        }

        public ConversionJob_PowerPoint(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
        }

        protected override ApplicationName Application => ApplicationName.PowerPoint;

        protected override bool IsCancelable() => false;

        protected override int GetOutputFilesCount()
        {
#if NETFRAMEWORK
            if (this.ConversionPreset.OutputType == OutputType.Pdf)
            {
                return 1;
            }

            if (!this.TryLoadDocumentIfNecessary())
            {
                return 1; // If loading fails, assume 1 page
            }

            if (this.document == null || this.document.Slides == null) return 1; // Document or slides collection not available

            try
            {
                int pagesCount = this.document.Slides.Count;
                return pagesCount > 0 ? pagesCount : 1; // Ensure at least 1
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error getting slide count from PowerPoint: {ex.Message}");
                return 1; // Assume 1 page on error
            }
#else
            return 1; // Office Interop not available
#endif
        }

        protected override void Initialize()
        {
            base.Initialize();

            if (this.State == ConversionState.Failed)
            {
                return;
            }

            if (this.ConversionPreset == null)
            {
                throw new System.Exception("The conversion preset must be valid.");
            }
#if NETFRAMEWORK
            // Initialize converters.
            if (this.ConversionPreset.OutputType == OutputType.Pdf)
            {
                this.intermediateFilePath = this.OutputFilePath;
            }
            else
            {
                // Generate intermediate file path.
                string fileName = Path.GetFileNameWithoutExtension(this.InputFilePath);
                string tempPath = Path.GetTempPath();
                this.intermediateFilePath = PathHelpers.GenerateUniquePath(tempPath + fileName + ".pdf");

                ConversionPreset intermediatePreset = new ConversionPreset("Pdf to image", this.ConversionPreset, "pdf");
                this.pdf2ImageConversionJob = ConversionJobFactory.Create(intermediatePreset, this.intermediateFilePath);
                this.pdf2ImageConversionJob.PrepareConversion(this.OutputFilePaths);
            }
        }

        protected override void Convert()
        {
            if (this.ConversionPreset == null)
            {
                throw new System.Exception("The conversion preset must be valid.");
            }
#if NETFRAMEWORK
            this.UserState = Properties.Resources.ConversionStateReadDocument;

            if (!this.TryLoadDocumentIfNecessary())
            {
                this.ConversionFailed(Properties.Resources.ErrorUnableToUseMicrosoftOffice);
                return;
            }

            this.UserState = Properties.Resources.ConversionStateConversion;

            Debug.Log("Convert PowerPoint document to pdf.");
            try
            {
                this.document.ExportAsFixedFormat(this.intermediateFilePath, PowerPoint.PpFixedFormatType.ppFixedFormatTypePDF);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error exporting PowerPoint document to PDF: {ex.Message}");
                this.ConversionFailed(Properties.Resources.ErrorUnableToUseMicrosoftOffice + " (export to PDF failed)");
                return;
            }

            Debug.Log($"Close PowerPoint document '{this.InputFilePath}'.");
            try
            {
                this.document.Close();
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error closing PowerPoint document: {ex.Message}");
                // Continue to release app instance
            }
            finally
            {
                this.document = null;
            }

            this.ReleaseOfficeApplicationInstanceIfNeeded();
            
            if (this.pdf2ImageConversionJob != null)
            {
                if (!System.IO.File.Exists(this.intermediateFilePath))
                {
                    this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                    return;
                }

                Task updateProgress = this.UpdateProgress();

                Debug.Log("Convert pdf to images.");

                this.pdf2ImageConversionJob.StartConversion();

                if (this.pdf2ImageConversionJob.State != ConversionState.Done)
                {
                    this.ConversionFailed(this.pdf2ImageConversionJob.ErrorMessage);
                    return;
                }

                if (!string.IsNullOrEmpty(this.intermediateFilePath))
                {
                    Debug.Log($"Delete intermediate file {this.intermediateFilePath}.");

                    File.Delete(this.intermediateFilePath);
                }

                updateProgress.Wait();
            }
        }

#else
            this.ConversionFailed(Properties.Resources.ErrorMicrosoftOfficeIsNotAvailable + " (Not available on this platform)");
#endif
        }

        protected override void InitializeOfficeApplicationInstanceIfNecessary()
        {
#if NETFRAMEWORK
            if (this.application != null)
            {
                return;
            }

            // Initialize PowerPoint application.
            Debug.Log("Instantiate PowerPoint application via interop.");
            try
            {
                this.application = new PowerPoint.Application();
                // Unlike Word/Excel, PowerPoint Application object doesn't have a Visible property directly.
                // Visibility is usually controlled when opening/creating presentations.
                // For automation, it's typically kept non-visible by not creating/showing windows.
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Failed to instantiate PowerPoint application: {ex.Message}");
                this.application = null;
            }
#endif
        }

        protected override void ReleaseOfficeApplicationInstanceIfNeeded()
        {
#if NETFRAMEWORK
            if (this.application != null)
            {
                try
                {
                    Diagnostics.Debug.Log("Quit PowerPoint application via interop.");
                    this.application.Quit();
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                     Debug.Log($"Error quitting PowerPoint application: {ex.Message}");
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(this.application);
                    this.application = null;
                }
            }
#endif
        }

        private async Task UpdateProgress()
        {
            while (this.pdf2ImageConversionJob.State != ConversionState.Done &&
                   this.pdf2ImageConversionJob.State != ConversionState.Failed)
            {
                if (this.pdf2ImageConversionJob != null && this.pdf2ImageConversionJob.State == ConversionState.InProgress)
                {
                    this.Progress = this.pdf2ImageConversionJob.Progress;
                }

                if (this.pdf2ImageConversionJob != null && this.pdf2ImageConversionJob.State == ConversionState.InProgress)
                {
                    this.Progress = this.pdf2ImageConversionJob.Progress;
                    this.UserState = this.pdf2ImageConversionJob.UserState;
                }

                await Task.Delay(40);
            }
        }
        
        private bool TryLoadDocumentIfNecessary()
        {
#if NETFRAMEWORK
            try
            {
                this.InitializeOfficeApplicationInstanceIfNecessary();
            }
            catch (Exception exception)
            {
                Debug.Log($"Unexpected error during InitializeOfficeApplicationInstanceIfNecessary (PowerPoint): {exception.ToString()}");
                if(this.application != null && !(this.application is PowerPoint.Application)) this.application = null;
            }

            if (this.application == null)
            {
                Debug.Log("PowerPoint application instance is null, cannot load document.");
                return false;
            }

            if (this.document == null)
            {
                Debug.Log($"Load PowerPoint document '{this.InputFilePath}'.");
                try
                {
                    // Parameters for Open: FileName, ReadOnly, Untitled, WithWindow
                    this.document = this.application.Presentations.Open(
                        this.InputFilePath,
                        Microsoft.Office.Core.MsoTriState.msoTrue,  // ReadOnly
                        Microsoft.Office.Core.MsoTriState.msoFalse, // Untitled (don't create a new untitled presentation if file not found)
                        Microsoft.Office.Core.MsoTriState.msoFalse  // WithWindow (don't show UI window)
                    );
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    Debug.Log($"Failed to open PowerPoint document '{this.InputFilePath}': {ex.Message}");
                    this.document = null;
                }
            }
            return this.document != null;
#else
            return false; // Office Interop not available
#endif
        }
    }
}
