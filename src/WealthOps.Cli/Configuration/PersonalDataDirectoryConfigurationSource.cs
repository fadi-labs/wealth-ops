using Microsoft.Extensions.Configuration;

namespace WealthOps.Cli.Configuration;

/// <summary>
/// Supplies <c>WealthOps:PersonalDataDirectory</c> from the environment, with a default.
/// </summary>
/// <remarks>
/// <para>
/// <c>WEALTHOPS_PERSONAL_DATA_DIR</c> is the documented way to point the system at the operator's
/// data, and it is an environment variable rather than a settings key for a specific reason: the
/// repository must hold a configuration <em>key</em> and never a path to real data (ADR-003,
/// BR-12). A committed settings file cannot contain the answer, so the environment supplies it.
/// </para>
/// <para>
/// Registered <em>below</em> the JSON and environment providers in precedence, so an explicit
/// <c>WealthOps:PersonalDataDirectory</c> in a local settings file still wins. This source only
/// fills a gap; it never overrides a deliberate choice.
/// </para>
/// </remarks>
public sealed class PersonalDataDirectoryConfigurationSource : IConfigurationSource
{
    /// <summary>The environment variable an operator sets to relocate their data root.</summary>
    public const string EnvironmentVariableName = "WEALTHOPS_PERSONAL_DATA_DIR";

    /// <summary>The configuration key this source populates.</summary>
    public const string ConfigurationKey = "WealthOps:PersonalDataDirectory";

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new PersonalDataDirectoryConfigurationProvider();
}

internal sealed class PersonalDataDirectoryConfigurationProvider : ConfigurationProvider
{
    public override void Load()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable(
            PersonalDataDirectoryConfigurationSource.EnvironmentVariableName);

        string value = string.IsNullOrWhiteSpace(fromEnvironment)
            ? DefaultPersonalDataDirectory()
            : Path.GetFullPath(fromEnvironment);

        Data[PersonalDataDirectoryConfigurationSource.ConfigurationKey] = value;
    }

    /// <summary>
    /// <c>~/.wealthops/personal</c>, matching the documented default.
    /// </summary>
    /// <remarks>
    /// Outside the repository by construction, which is what keeps personal files from ever
    /// landing somewhere a commit could pick them up.
    /// </remarks>
    private static string DefaultPersonalDataDirectory()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".wealthops",
            "personal");
}
