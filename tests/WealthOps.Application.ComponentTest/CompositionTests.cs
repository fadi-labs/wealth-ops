using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Persistence;
using WealthOps.Application.Extensions;
using WealthOps.Application.Features.Chat;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Application.ComponentTest;

/// <summary>
/// Guards the DI lifetimes that <c>AddApplication</c> sets up.
/// </summary>
/// <remarks>
/// The Mediator source generator defaults to <see cref="ServiceLifetime.Singleton"/>. Handlers in
/// this application consume scoped collaborators — <c>DbContext</c> above all — so that default
/// produces a captive dependency. Worth its own test because the failure appears at composition
/// time in whichever root happens to boot first, which makes it look like a Host or CLI bug rather
/// than a registration one.
/// </remarks>
public sealed class CompositionTests
{
    [Fact]
    public void MediatorAndItsHandlersAreScoped()
    {
        ServiceProvider provider = BuildProvider();

        // ValidateOnBuild + ValidateScopes is what catches a singleton capturing a scoped
        // dependency. If the generator reverted to Singleton, this throws here.
        provider.ShouldNotBeNull();
        provider.Dispose();
    }

    [Fact]
    public async Task AHandlerResolvesAndRunsInsideAScope()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        SendChatMessage.Response response = await mediator.Send(
            new SendChatMessage.Request([], "a question"),
            TestContext.Current.CancellationToken);

        response.Reply.ShouldBe("a synthetic reply");
    }

    [Fact]
    public async Task TheValidationPipelineRunsBeforeTheHandler()
    {
        // Confirms the behavior is actually wired into the generated pipeline, not merely
        // registered — fail-fast validation is a non-negotiable for every request.
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Should.ThrowAsync<Application.Common.Exceptions.RequestValidationException>(
            async () => await mediator.Send(
                new SendChatMessage.Request([], ""),
                TestContext.Current.CancellationToken));
    }

    private static ServiceProvider BuildProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(SyntheticConfiguration())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(configuration);

        // Stand-ins for every Infrastructure registration, scoped exactly as the real ones are.
        // The full set is required: ValidateOnBuild constructs each handler, so a missing
        // contract fails the build rather than being skipped.
        services.AddScoped<IChatModel, StubChatModel>();
        services.AddScoped<IEmbeddingModel, StubEmbeddingModel>();
        services.AddScoped<IModelEndpointProbe, StubModelEndpointProbe>();
        services.AddScoped<IDocumentStore, StubDocumentStore>();
        services.AddScoped<IVectorStore, StubVectorStore>();
        services.AddScoped<IPersistenceDiagnostics, StubPersistenceDiagnostics>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static Dictionary<string, string?> SyntheticConfiguration()
    {
        string personal = Path.Combine(Path.GetTempPath(), "wealthops-synthetic-composition");

        return new Dictionary<string, string?>
        {
            ["WealthOps:PersonalDataDirectory"] = personal,
            ["WealthOps:RulesCacheDirectory"] = Path.Combine(Path.GetTempPath(), "wealthops-synthetic-rules"),
            ["WealthOps:Instruments"] = Path.Combine(personal, "reference", "instruments.csv"),
            ["WealthOps:Taxation:TaxYears:0"] = "2026",
            ["WealthOps:Taxation:Household:Taxpayers:0:Id"] = "taxpayer-a",
            ["WealthOps:Taxation:Household:Taxpayers:0:Municipality"] = "SYNTHETIC_MUNICIPALITY",
            ["WealthOps:Taxation:Household:Taxpayers:0:MaritalStatus"] = "Single",
            ["WealthOps:Accounts:0:Id"] = "account-1",
            ["WealthOps:Accounts:0:Wrapper"] = "FrieMidler",
            ["WealthOps:Accounts:0:ExportFormat"] = "FormatA",
            ["WealthOps:Accounts:0:OwnerTaxpayerId"] = "taxpayer-a"
        };
    }

    private sealed class StubChatModel : IChatModel
    {
        public string ModelId => "synthetic-chat-model";

        public Task<ChatCompletion> CompleteAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatCompletion("a synthetic reply", [], ModelId));
    }

    private sealed class StubEmbeddingModel : IEmbeddingModel
    {
        public string ModelId => "synthetic-embedding-model";

        public int Dimensions => 4;

        public Task<EmbeddingVector> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(EmbeddingVector.Create([0.1f, 0.2f, 0.3f, 0.4f]));

        public Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EmbeddingVector>>(
                [.. texts.Select(_ => EmbeddingVector.Create([0.1f, 0.2f, 0.3f, 0.4f]))]);
    }

    private sealed class StubModelEndpointProbe : IModelEndpointProbe
    {
        public Task<ModelEndpointStatus> ProbeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelEndpointStatus(
                new Uri("http://127.0.0.1:1/v1"),
                IsReachable: false,
                AvailableModelIds: [],
                FailureReason: "No model endpoint in tests (NFR-3)."));
    }

    private sealed class StubDocumentStore : IDocumentStore
    {
        public Task<DocumentRecordResult> RecordAsync(
            Domain.Entities.Document document,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentRecordResult(document, DocumentRecordOutcome.Recorded));

        public Task<Domain.Entities.Document?> FindByContentHashAsync(
            ContentHash contentHash,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Domain.Entities.Document?>(null);

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class StubVectorStore : IVectorStore
    {
        public Task UpsertChunksAsync(
            IReadOnlyCollection<Domain.Entities.DocumentChunk> chunks,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<CorpusSearchResult>> SearchAsync(
            CorpusQuery query,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CorpusSearchResult>>([]);

        public Task<IReadOnlyList<EmbeddingProfile>> GetEmbeddingProfilesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EmbeddingProfile>>([]);
    }

    private sealed class StubPersistenceDiagnostics : IPersistenceDiagnostics
    {
        public Task<PersistenceStatus> ProbeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PersistenceStatus(
                CanConnect: true,
                IsVectorExtensionInstalled: true,
                PendingMigrations: [],
                FailureReason: null));
    }
}
