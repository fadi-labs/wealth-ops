using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Clients;

namespace WealthOps.Infrastructure.Clients.OpenAiCompatible;

/// <summary>
/// Asks the configured endpoint for its model list, to establish whether it is answering.
/// </summary>
/// <remarks>
/// Reports failure as data and never throws. <c>status</c> is the instrument an operator reaches
/// for when the stack is broken; an instrument that throws when the thing it measures is broken is
/// not an instrument.
/// </remarks>
internal sealed class ModelEndpointProbe(
    HttpClient httpClient,
    IOptions<ModelGatewayOptions> options,
    ILogger<ModelEndpointProbe> logger)
    : IModelEndpointProbe
{
    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(5);

    private readonly ModelGatewayOptions _options = options.Value;

    public async Task<ModelEndpointStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = new Uri(_options.Endpoint, UriKind.Absolute);

        // A short, independent budget: the configured completion timeout is minutes long by
        // design, and status must not hang for minutes to tell the operator nothing is listening.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_probeTimeout);

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync("models", timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return new ModelEndpointStatus(
                    endpoint,
                    IsReachable: false,
                    AvailableModelIds: [],
                    FailureReason: $"Endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            ModelListResponseBody? payload =
                await response.Content.ReadFromJsonAsync<ModelListResponseBody>(timeout.Token);

            IReadOnlyList<string> modelIds =
            [
                .. (payload?.Data ?? [])
                    .Select(m => m.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!)
                    .Order(StringComparer.Ordinal)
            ];

            logger.LogDebug("Model endpoint probe succeeded. Models advertised: {ModelCount}", modelIds.Count);

            return new ModelEndpointStatus(endpoint, IsReachable: true, modelIds, FailureReason: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug("Model endpoint probe failed: {Reason}", ex.Message);

            return new ModelEndpointStatus(
                endpoint,
                IsReachable: false,
                AvailableModelIds: [],
                FailureReason: ex is OperationCanceledException
                    ? $"No response within {_probeTimeout.TotalSeconds:0} seconds."
                    : ex.Message);
        }
    }
}
