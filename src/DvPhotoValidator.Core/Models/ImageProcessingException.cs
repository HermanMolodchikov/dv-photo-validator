namespace DvPhotoValidator.Core.Models;

/// <summary>Expected processing failure with a message that can be shown to the user as is.</summary>
public class ImageProcessingException : Exception
{
    public ImageProcessingException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>The exported file was written but did not pass re-verification, so the export was rolled back.</summary>
public sealed class ExportVerificationException : ImageProcessingException
{
    public ExportVerificationException(string message, ValidationReport report)
        : base(message)
    {
        Report = report;
    }

    public ValidationReport Report { get; }
}
