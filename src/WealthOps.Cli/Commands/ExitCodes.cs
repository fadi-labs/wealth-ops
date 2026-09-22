namespace WealthOps.Cli.Commands;

/// <summary>
/// Process exit codes.
/// </summary>
/// <remarks>
/// Distinguished rather than collapsed into "nonzero" so <c>status</c> can be used as a gate: a
/// script wants to tell "the stack is unhealthy" apart from "you typed the command wrong".
/// </remarks>
internal static class ExitCodes
{
    public const int Success = 0;

    /// <summary>The command ran but the thing it did failed.</summary>
    public const int Failure = 1;

    /// <summary>The command line could not be understood.</summary>
    public const int UsageError = 2;

    /// <summary><c>status</c> ran successfully and found something wrong.</summary>
    public const int Unhealthy = 3;
}
