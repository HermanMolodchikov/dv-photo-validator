namespace DvPhotoValidator.Core.Models;

public sealed record JpegEncodeResult(byte[] Data, int Quality);

public sealed record ExportResult(string FilePath, int Quality, long FileBytes, ImageFileFacts Facts, ValidationReport Report);
