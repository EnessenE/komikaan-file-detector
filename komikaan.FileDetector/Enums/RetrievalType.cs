namespace komikaan.FileDetector.Enums;

public enum RetrievalType
{
    /// <summary>
    ///  Requires a form of HTTP or HTTPS call
    /// </summary>
    REST = 1,
    /// <summary>
    /// A zip file locally stored, not to be used in prod
    /// </summary>
    LOCAL = 2
}