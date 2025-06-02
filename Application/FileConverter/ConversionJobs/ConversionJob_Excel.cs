// <copyright file="ConversionJob_Excel.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using FileConverter.Diagnostics;

#if NETFRAMEWORK
    using Excel = Microsoft.Office.Interop.Excel;
#endif

    public class ConversionJob_Excel : ConversionJob_Office
    {
#if NETFRAMEWORK
        private Excel.Workbook document;
        private Excel.Application application;
#endif
        private string intermediateFilePath = string.Empty;
        private ConversionJob pdf2ImageConversionJob = null;

        public ConversionJob_Excel() : base()
        {
        }

        public ConversionJob_Excel(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
        }

        protected override ApplicationName Application => ApplicationName.Excel;

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
                // If loading fails (e.g. Office not available, or other interop issue),
                // we can't determine page count, so assume 1 to avoid further errors.
                return 1;
            }

            // document should be non-null if TryLoadDocumentIfNecessary succeeded
            if (this.document == null) return 1;


            int pagesCount = 0;
            foreach (object sheet in this.document.Sheets)
            {
                Excel.Worksheet worksheet = sheet as Excel.Worksheet;
                if (worksheet != null)
                {
                    // This can be complex if a sheet is not a standard worksheet (e.g. chart sheet)
                    // or if PageSetup is not available.
                    try
                    {
                        if (worksheet.PageSetup != null && worksheet.PageSetup.Pages != null)
                        {
                             pagesCount += worksheet.PageSetup.Pages.Count;
                        }
                        else
                        {
                            // If PageSetup or Pages is null, assume 1 page for this sheet or log warning.
                            // For simplicity here, count it as 1 page if it's a worksheet.
                            pagesCount += 1;
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        // Handle COM errors, e.g. if a sheet doesn't support PageSetup.Pages
                        Debug.Log($"Error getting page count for worksheet: {ex.Message}");
                        pagesCount +=1; // Assume 1 page on error
                    }
                }
            }
             return pagesCount > 0 ? pagesCount : 1; // Ensure at least 1
#else
            // On non-Windows, Office Interop is not available.
            // If output is PDF, it's 1 file. Otherwise, we can't determine page count for images.
            // The job will likely fail in Initialize or Convert if Office is required.
            return 1;
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

            // Make this document the active document.
            // This might throw if the document or application is not in a valid state.
            try
            {
                this.document.Activate();
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error activating Excel document: {ex.Message}");
                this.ConversionFailed(Properties.Resources.ErrorUnableToUseMicrosoftOffice + " (activation failed)");
                return;
            }


            this.UserState = Properties.Resources.ConversionStateConversion;

            Debug.Log("Convert excel document to pdf.");
            // This can also throw COMExceptions
            try
            {
                this.document.ExportAsFixedFormat(Excel.XlFixedFormatType.xlTypePDF, this.intermediateFilePath);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Error exporting Excel document to PDF: {ex.Message}");
                this.ConversionFailed(Properties.Resources.ErrorUnableToUseMicrosoftOffice + " (export to PDF failed)");
                return;
            }


            Debug.Log($"Close excel document '{this.InputFilePath}'.");
            try
            {
                this.document.Close(false); // false: do not save changes
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                 Debug.Log($"Error closing Excel document: {ex.Message}");
                 // Continue, as we want to release the app instance anyway
            }
            finally
            {
                this.document = null; // Ensure it's marked as null
            }


            this.ReleaseOfficeApplicationInstanceIfNeeded(); // Releases 'this.application'
            
            if (this.pdf2ImageConversionJob != null) // This job converts the PDF (from Excel) to images
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
            // This entire 'Convert' method's core logic relies on Office Interop.
            // If not on NETFRAMEWORK, this job type should ideally not be attempted or should fail early.
            // The check in base.Initialize() using Helpers.IsMicrosoftOfficeApplicationAvailable should prevent this.
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

            // Initialize excel application.
            Diagnostics.Debug.Log("Instantiate excel application via interop.");
            try
            {
                this.application = new Excel.Application
                {
                    Visible = false
                    // Consider setting DisplayAlerts = false if appropriate
                    // application.DisplayAlerts = false;
                };
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Debug.Log($"Failed to instantiate Excel application: {ex.Message}");
                this.application = null; // Ensure it's null on failure
            }
#else
            // No-op on non-Windows platforms
#endif
        }

        protected override void ReleaseOfficeApplicationInstanceIfNeeded()
        {
#if NETFRAMEWORK
            if (this.application != null)
            {
                try
                {
                    Diagnostics.Debug.Log("Quit excel application via interop.");
                    this.application.Quit();
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    Debug.Log($"Error quitting Excel application: {ex.Message}");
                }
                finally
                {
                    // Ensure COM object is released
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(this.application);
                    this.application = null;
                }
            }
#else
            // No-op on non-Windows platforms
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
                this.InitializeOfficeApplicationInstanceIfNecessary(); // This now handles its own try-catch for instantiation
            }
            catch (Exception exception) // Catch unexpected errors from Initialize or subsequent logic
            {
                Debug.Log($"Unexpected error during InitializeOfficeApplicationInstanceIfNecessary or pre-load: {exception.ToString()}");
                // Ensure application is null if initialization failed critically before COM object assignment
                if (this.application != null && ! (this.application is Excel.Application) ) this.application = null;
            }

            if (this.application == null) // Check if instantiation failed
            {
                Debug.Log("Excel application instance is null, cannot load document.");
                return false;
            }

            if (this.document == null)
            {
                Debug.Log($"Load excel document '{this.InputFilePath}'.");
                try
                {
                    // Parameters for Open: Filename, UpdateLinks, ReadOnly, Format, Password, WriteResPassword,
                    // IgnoreReadOnlyRecommended, Origin, Delimiter, Editable, Notify, Converter, AddToMru
                    this.document = this.application.Workbooks.Open(
                        this.InputFilePath,
                        0, // UpdateLinks: 0 = Don't update, 3 = Update remote and local
                        true, // ReadOnly
                        Type.Missing, // Format
                        Type.Missing, // Password
                        Type.Missing, // WriteResPassword
                        Type.Missing, // IgnoreReadOnlyRecommended
                        Type.Missing, // Origin
                        Type.Missing, // Delimiter
                        Type.Missing, // Editable
                        false,        // Notify: false = Don't notify if file can't be opened
                        Type.Missing, // Converter
                        false         // AddToMru: false = Don't add to Most Recently Used
                    );
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    Debug.Log($"Failed to open Excel document '{this.InputFilePath}': {ex.Message}");
                    this.document = null; // Ensure document is null on failure
                }
            }
            return this.document != null;
#else
            // Office Interop not available on this platform
            return false;
#endif
        }
    }
}
