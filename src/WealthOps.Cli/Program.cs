using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Exceptions;
using WealthOps.Application.Extensions;
using WealthOps.Cli.Commands;
using WealthOps.Cli.Configuration;
using WealthOps.Infrastructure.Extensions;
using WealthOps.Infrastructure.Persistence.Extensions;

namespace WealthOps.Cli;

/// <summary>
/// The v1 operator surface (ADR-001).
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            // Cancel cooperatively so an interrupted ingest stops between files rather than
            // mid-write, and the chat loop exits its prompt cleanly.
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        if (args.Length == 0 || IsHelpRequest(args[0]))
        {
            WriteUsage(Console.Out);
            return args.Length == 0 ? ExitCodes.UsageError : ExitCodes.Success;
        }

        string command = args[0].ToLowerInvariant();

        try
        {
            using IHost host = BuildHost(args);

            // Both composition roots migrate on startup; see MigrationExtensions for why that is
            // appropriate in a single-operator system. status deliberately does not, so it can
            // report a pending schema rather than quietly changing it.
            if (command is "chat" or "ingest")
            {
                await host.Services.MigrateWealthOpsAsync(cancellation.Token);
            }

            var mediator = host.Services.GetRequiredService<IMediator>();

            return command switch
            {
                "status" => await StatusCommand.RunAsync(mediator, Console.Out, cancellation.Token),
                "chat" => await ChatCommand.RunAsync(mediator, Console.In, Console.Out, cancellation.Token),
                "ingest" => await RunIngestAsync(mediator, args, cancellation.Token),
                _ => UnknownCommand(command)
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return ExitCodes.Failure;
        }
        catch (OptionsValidationException ex)
        {
            // The first-run failure. Listing every problem beats one-at-a-time discovery.
            Console.Error.WriteLine("Configuration is not valid:");
            foreach (string failure in ex.Failures)
            {
                Console.Error.WriteLine($"  - {failure}");
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine("Copy appsettings.Example.json to appsettings.Local.json and fill it in.");
            Console.Error.WriteLine("appsettings.Local.json is gitignored and is where real values belong.");
            return ExitCodes.Failure;
        }
        catch (RequestValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.UsageError;
        }
        catch (Exception ex) when (ex is ModelGatewayException
                                       or EmbeddingDimensionMismatchException
                                       or DirectoryNotFoundException
                                       or FileNotFoundException)
        {
            // Failures with an actionable message of their own — print it, not a stack trace.
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.Failure;
        }
    }

    private static async Task<int> RunIngestAsync(IMediator mediator, string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2 || args[1].StartsWith('-'))
        {
            Console.Error.WriteLine("ingest requires a path: wealthops ingest <path>");
            return ExitCodes.UsageError;
        }

        return await IngestCommand.RunAsync(mediator, args[1], Console.Out, cancellationToken);
    }

    private static IHost BuildHost(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddWealthOpsConfiguration(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        // The CLI is an interactive tool: routine Information-level operation logs would bury the
        // command's own output. Raise the level per namespace in configuration when diagnosing.
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddApplication(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);

        return builder.Build();
    }

    private static bool IsHelpRequest(string argument)
        => argument is "-h" or "--help" or "help" or "-?" or "/?";

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        WriteUsage(Console.Error);
        return ExitCodes.UsageError;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("wealthops <command>");
        writer.WriteLine();
        writer.WriteLine("  status          Report configuration against the model endpoint and database.");
        writer.WriteLine("  chat            Hold a conversation with the configured chat model.");
        writer.WriteLine("  ingest <path>   Record the files under a path, idempotently.");
        writer.WriteLine();
        writer.WriteLine("Configuration lives in appsettings.Local.json (gitignored) or WEALTHOPS_* variables.");
        writer.WriteLine("appsettings.Example.json shows the shape.");
    }
}
