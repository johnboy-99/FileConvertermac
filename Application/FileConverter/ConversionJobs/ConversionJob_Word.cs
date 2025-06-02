// <copyright file="ConversionJob_Word.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using FileConverter.Diagnostics;

#if NETFRAMEWORK
    using Word = Microsoft.Office.Interop.Word;
#endif

    public class ConversionJob_Word : ConversionJob_Office
    {
#if NETFRAMEWORK
        private Word.Document document;
        private Word.Application application;
#endif
        private string intermediateFilePath = string.Empty;
        private ConversionJob pdf2ImageConversionJob = null;

        public ConversionJob_Word() : base()
        {
        }

        public ConversionJob_Word(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
        }

        protected override ApplicationName Application => ApplicationName.Word;

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

            if (this.document == null) return 1; // Document not available

            try
            {
                int pagesCount = this.document.ComputeStatistics(Word.WdStatistic.wdStatisticPages);
                return pagesCount > 0 ? pagesCount : 1; // Ensure at least 1
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error getting page count from Word: {ex.Message}");
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

            try
            {
                // Make this document the active document.
                this.document.Activate();
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error activating Word document: {ex.Message}");
                this.ConversionFailed(Properties.Resources.ErrorUnableToUseMicrosoftOffice + " (activation failed)");
                return;
            }

            this.UserState = Properties.Resources.ConversionStateConversion;

            Debug.Log("Convert word document to pdf.");
            try
            {
                this.document.ExportAsFixedFormat(this.intermediateFilePath,
                    Word.WdExportFormat.wdExportFormatPDF,
                    false, // OpenAfterExport
                    Word.WdExportOptimizeFor.wdExportOptimizeForPrint,
                    Word.WdExportRange.wdExportAllDocument,
                    1, // From page
                    1, // To page (actually means all pages when wdExportAllDocument is used)
                    Word.WdExportItem.wdExportDocumentContent,
                    true, // IncludeDocProps
                    true, // KeepIRM
                    Word.WdExportCreateBookmarks.wdExportCreateHeadingBookmarks,
                    true); // DocStructureTags
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error exporting Word document to PDF: {ex.Message}");
                this.ConversionFailed(Properties.Resources.ErrorUnableToUseMicrosoftOffice + " (export to PDF failed)");
                return;
            }

            Debug.Log($"Close word document '{this.InputFilePath}'.");
            try
            {
                this.document.Close(Word.WdSaveOptions.wdDoNotSaveChanges);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error closing Word document: {ex.Message}");
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

            // Initialize word application.
            Debug.Log("Instantiate word application via interop.");
            try
            {
                this.application = new Word.Application
                {
                    Visible = false
                    // application.DisplayAlerts = Word.WdAlertLevel.wdAlertsNone; // Example
                };
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Failed to instantiate Word application: {ex.Message}");
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
                    Diagnostics.Debug.Log("Quit word application via interop.");
                    this.application.Quit(Word.WdSaveOptions.wdDoNotSaveChanges);
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    Debug.Log($"Error quitting Word application: {ex.Message}");
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
                Debug.Log($"Unexpected error during InitializeOfficeApplicationInstanceIfNecessary (Word): {exception.ToString()}");
                 if(this.application != null && !(this.application is Word.Application)) this.application = null;
            }

            if (this.application == null)
            {
                Debug.Log("Word application instance is null, cannot load document.");
                return false;
            }

            if (this.document == null)
            {
                Debug.Log($"Load word document '{this.InputFilePath}'.");
                try
                {
                    // Parameters for Documents.Open: FileName, ConfirmConversions, ReadOnly, AddToRecentFiles, PasswordDocument, ...
                    this.document = this.application.Documents.Open(
                        FileName: this.InputFilePath,
                        ConfirmConversions: false,
                        ReadOnly: true,
                        AddToRecentFiles: false
                        // Other parameters are optional and Type.Missing can be used.
                        );
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    Debug.Log($"Failed to open Word document '{this.InputFilePath}': {ex.Message}");
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
