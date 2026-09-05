namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>Thrown when an XNC <c>program</c> sub-document cannot be read or its expressions evaluated.</summary>
    public class XncProgramFormatException : Exception
    {
        public XncProgramFormatException(string message) : base(message)
        {
        }

        public XncProgramFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
