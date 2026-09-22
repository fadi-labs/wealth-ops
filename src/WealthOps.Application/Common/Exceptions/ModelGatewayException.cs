namespace WealthOps.Application.Common.Exceptions;

/// <summary>
/// The configured model endpoint failed, or answered with something unusable.
/// </summary>
/// <remarks>
/// A named exception rather than a bare <see cref="InvalidOperationException"/> so the CLI can tell
/// "your model endpoint is not running" apart from a defect, and say so in terms the operator can
/// act on (NFR-7).
/// </remarks>
public sealed class ModelGatewayException : Exception
{
    public ModelGatewayException(string message)
        : base(message)
    {
    }

    public ModelGatewayException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
