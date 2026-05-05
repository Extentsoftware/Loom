using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Loom.Agents.Foundry;

/// <summary>
/// HttpClient-based implementation of IFoundryChatClient. POSTs to
/// {Endpoint}/openai/deployments/{Deployment}/chat/completions?api-version=...
/// and yields shape-stable FoundryStreamEvent instances. The wire format
/// is Azure OpenAI Chat Completions; if Azure ships a breaking event-type
/// addition we add a case here, never let it leak past this seam.
///
/// Auth: <c>api-key</c> request header. The endpoint determines the model;
/// no <c>model</c> field is sent in the body (Azure derives it).
/// </summary>
public sealed class FoundryChatClient(
    HttpClient httpClient,
    IOptions<FoundryOptions> options) : IFoundryChatClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async IAsyncEnumerable<FoundryStreamEvent> StreamAsync(
        FoundryRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Endpoint))
        {
            throw new InvalidOperationException("Foundry Endpoint is not configured.");
        }
        if (string.IsNullOrWhiteSpace(opts.Deployment))
        {
            throw new InvalidOperationException("Foundry Deployment is not configured.");
        }
        if (string.IsNullOrWhiteSpace(opts.ApiVersion))
        {
            throw new InvalidOperationException("Foundry ApiVersion is not configured.");
        }
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new InvalidOperationException("Foundry ApiKey is not configured.");
        }

        var requestUri = new Uri(
            $"{opts.Endpoint.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(opts.Deployment)}/chat/completions?api-version={Uri.EscapeDataString(opts.ApiVersion)}");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        requestMessage.Headers.TryAddWithoutValidation("api-key", opts.ApiKey);

        var messages = new List<ApiMessage>(capacity: request.Messages.Count + 2);
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(new ApiMessage { Role = "system", Content = request.SystemPrompt });
        }
        // Azure OpenAI's response_format=json_object validation requires
        // the literal word "json" in some message. The seeded fragments
        // do mention JSON, but case + boundary matching has bitten us
        // before — appending an explicit lowercase-"json" instruction
        // is belt-and-braces and also doubles as a final reminder to
        // the model.
        if (request.RequiresJsonOutput)
        {
            messages.Add(new ApiMessage
            {
                Role = "system",
                Content = "Respond with a single valid json object that matches the schema described above. No commentary, no markdown fences, no prose — just the json."
            });
        }
        foreach (var m in request.Messages)
        {
            messages.Add(new ApiMessage { Role = m.Role, Content = m.Content });
        }

        var body = new ApiRequestBody
        {
            Messages = messages,
            MaxTokens = request.MaxOutputTokens ?? 4096,
            Stream = true,
            StreamOptions = new ApiStreamOptions { IncludeUsage = true },
            // Forces a JSON-object reply on Azure OpenAI / Foundry when the
            // owning workflow step declares an OutputSchemaName. Without
            // this, GPT-style models sometimes wrap their JSON in prose or
            // markdown fences and downstream parsers fall over.
            ResponseFormat = request.RequiresJsonOutput
                ? new ApiResponseFormat { Type = "json_object" }
                : null
        };
        var bodyJson = JsonSerializer.Serialize(body, Json);
        requestMessage.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            yield return new FoundryStreamEvent.Failed($"HTTP {(int)response.StatusCode}: {errorBody}");
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // SSE parser: events are separated by blank lines. Azure OpenAI uses
        // only `data:` payloads; the terminator is the literal line
        // `data: [DONE]`. We emit a synthetic Stopped on [DONE] if the model
        // didn't surface a finish_reason in a prior chunk.
        var dataBuffer = new StringBuilder();
        var startedEmitted = false;
        var stoppedEmitted = false;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                if (dataBuffer.Length > 0)
                {
                    var payload = dataBuffer.ToString();
                    dataBuffer.Clear();

                    if (payload == "[DONE]")
                    {
                        if (!stoppedEmitted)
                        {
                            yield return new FoundryStreamEvent.Stopped("end_turn");
                            stoppedEmitted = true;
                        }
                        continue;
                    }

                    foreach (var evt in ParseEvent(payload, ref startedEmitted, ref stoppedEmitted))
                    {
                        yield return evt;
                    }
                }
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var fragment = line.Length > 5 && line[5] == ' ' ? line[6..] : line[5..];
                dataBuffer.Append(fragment);
            }
        }

        if (dataBuffer.Length > 0)
        {
            var payload = dataBuffer.ToString();
            if (payload == "[DONE]")
            {
                if (!stoppedEmitted)
                {
                    yield return new FoundryStreamEvent.Stopped("end_turn");
                }
            }
            else
            {
                foreach (var evt in ParseEvent(payload, ref startedEmitted, ref stoppedEmitted))
                {
                    yield return evt;
                }
            }
        }
    }

    private static List<FoundryStreamEvent> ParseEvent(string payload, ref bool startedEmitted, ref bool stoppedEmitted)
    {
        var parseResult = TryParse(payload);
        if (parseResult.Error is not null)
        {
            return [new FoundryStreamEvent.Failed($"Could not parse event: {parseResult.Error}")];
        }
        var parsed = parseResult.Payload;
        if (parsed is null)
        {
            return [];
        }

        return MaterializeEvents(parsed, ref startedEmitted, ref stoppedEmitted);
    }

    private static List<FoundryStreamEvent> MaterializeEvents(
        ChatStreamPayload parsed,
        ref bool startedEmitted,
        ref bool stoppedEmitted)
    {
        // Accumulate into a local list rather than yielding — ref-locals
        // (the started/stopped flags) can't cross iterator boundaries.
        var events = new List<FoundryStreamEvent>();

        if (parsed.Error is { Message: { Length: > 0 } errMsg })
        {
            events.Add(new FoundryStreamEvent.Failed(errMsg));
            return events;
        }

        if (!startedEmitted && !string.IsNullOrEmpty(parsed.Id))
        {
            events.Add(new FoundryStreamEvent.Started(parsed.Id ?? string.Empty, parsed.Model ?? string.Empty));
            startedEmitted = true;
        }

        if (parsed.Choices is { Count: > 0 })
        {
            foreach (var choice in parsed.Choices)
            {
                if (choice.Delta is { Content: { Length: > 0 } content })
                {
                    events.Add(new FoundryStreamEvent.TextDelta(content));
                }
                if (choice.FinishReason is { Length: > 0 } finish)
                {
                    events.Add(new FoundryStreamEvent.Stopped(finish));
                    stoppedEmitted = true;
                }
            }
        }

        if (parsed.Usage is { } usage)
        {
            events.Add(new FoundryStreamEvent.Usage(usage.PromptTokens, usage.CompletionTokens));
        }

        return events;
    }

    private static (ChatStreamPayload? Payload, string? Error) TryParse(string payload)
    {
        try
        {
            return (JsonSerializer.Deserialize<ChatStreamPayload>(payload, Json), null);
        }
        catch (JsonException ex)
        {
            return (null, ex.Message);
        }
    }

    // Wire shapes — internal, never escape this assembly.

    private sealed class ApiRequestBody
    {
        [JsonPropertyName("messages")] public List<ApiMessage> Messages { get; set; } = [];

        // GPT-5 / o-series Azure OpenAI deployments require
        // `max_completion_tokens`; the legacy `max_tokens` is rejected.
        // `max_completion_tokens` is also accepted by newer GPT-4 SKUs,
        // so it's safe as the single name we send.
        [JsonPropertyName("max_completion_tokens")] public int MaxTokens { get; set; }

        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("stream_options")] public ApiStreamOptions? StreamOptions { get; set; }
        [JsonPropertyName("response_format")] public ApiResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class ApiMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class ApiStreamOptions
    {
        [JsonPropertyName("include_usage")] public bool IncludeUsage { get; set; }
    }

    private sealed class ApiResponseFormat
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "text";
    }

    private sealed class ChatStreamPayload
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("choices")] public List<ChatChoice>? Choices { get; set; }
        [JsonPropertyName("usage")] public ChatUsage? Usage { get; set; }
        [JsonPropertyName("error")] public ChatError? Error { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("delta")] public ChatDelta? Delta { get; set; }
        [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
    }

    private sealed class ChatDelta
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    private sealed class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    }

    private sealed class ChatError
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
