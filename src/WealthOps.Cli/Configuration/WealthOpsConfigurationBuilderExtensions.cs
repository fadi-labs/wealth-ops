using Microsoft.Extensions.Configuration;

namespace WealthOps.Cli.Configuration;

public static class WealthOpsConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds the operator's configuration sources, in precedence order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lowest to highest: the defaulted personal data directory, then committed
    /// <c>appsettings.json</c>, then the gitignored <c>appsettings.Local.json</c>, then
    /// environment variables and command-line arguments.
    /// </para>
    /// <para>
    /// <c>appsettings.Local.json</c> is where real values live and is never committed (BR-12).
    /// <c>appsettings.Example.json</c> is the committed template beside it, carrying placeholders
    /// only — it is deliberately not loaded, so a machine that has not been configured fails
    /// validation loudly rather than running against fictional settings.
    /// </para>
    /// </remarks>
    public static IConfigurationBuilder AddWealthOpsConfiguration(
        this IConfigurationBuilder builder,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(new PersonalDataDirectoryConfigurationSource());
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        builder.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
        builder.AddEnvironmentVariables();

        // Only switch-shaped arguments reach the configuration provider. The command verb and its
        // operand ("ingest <path>") are positional, and the provider throws on those rather than
        // ignoring them.
        string[] switches = [.. args.SkipWhile(a => !IsSwitch(a))];

        if (switches.Length > 0)
        {
            builder.AddCommandLine(switches);
        }

        return builder;

        static bool IsSwitch(string argument)
            => argument.StartsWith("--", StringComparison.Ordinal)
               || argument.StartsWith('/');
    }
}
