// <copyright file="ConversionJob_Office.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    // No specific Office Interop usings directly here, but derived classes will have them.
    // System might be needed for Console.WriteLine in #else blocks if we add logging there.
    using System;

    public abstract class ConversionJob_Office : ConversionJob
    {
        protected ConversionJob_Office() : base()
        {
        }

        protected ConversionJob_Office(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
        }

        public enum ApplicationName
        {
            None,

            Word,
            Excel,
            PowerPoint
        }

        protected abstract ApplicationName Application
        {
            get;
        }

        protected override bool IsCancelable() => false;

        protected override void Initialize()
        {
            base.Initialize();

            // Helpers.IsMicrosoftOfficeApplicationAvailable will be responsible for the platform check.
            // If it correctly returns false on non-Windows, this logic remains valid.
            // The Office Interop code itself in derived classes will be conditionally compiled.
            if (!Helpers.IsMicrosoftOfficeApplicationAvailable(this.Application))
            {
                switch (this.Application)
                {
                    case ApplicationName.Word:
                        this.ConversionFailed(Properties.Resources.ErrorMicrosoftWordIsNotAvailable);
                        return;

                    case ApplicationName.PowerPoint:
                        this.ConversionFailed(Properties.Resources.ErrorMicrosoftPowerPointIsNotAvailable);
                        return;

                    case ApplicationName.Excel:
                        this.ConversionFailed(Properties.Resources.ErrorMicrosoftExcelIsNotAvailable);
                        return;

                    default:
                        this.ConversionFailed(Properties.Resources.ErrorMicrosoftOfficeIsNotAvailable);
                        return;
                }
            }
        }

        protected override void OnConversionFailed()
        {
            base.OnConversionFailed();
#if NETFRAMEWORK
            this.ReleaseOfficeApplicationInstanceIfNeeded();
#endif
        }

        // These methods will be implemented in derived classes and will contain Office Interop code,
        // so their implementations in derived classes need to be conditionally compiled.
        protected abstract void InitializeOfficeApplicationInstanceIfNecessary();

        protected abstract void ReleaseOfficeApplicationInstanceIfNeeded();
    }
}