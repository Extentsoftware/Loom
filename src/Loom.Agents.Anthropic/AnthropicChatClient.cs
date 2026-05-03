using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Loom.Agents.Anthropic;

/// <summary>
/// HttpClient-based implementation of IAnthropicChatClient. Streams Server-
/// Sent Events from /v1/messages and yields shape-stable AnthropicStreamEvent
/// instances. The wire format follows the Messages API v1; if Anthropic ships
/// a breaking event-type addition we add a case here, never let it leak past
/// this seam.
/// </summary>
public sealed class AnthropicChatClient(
    HttpClient httpClient,
    IOptions<AnthropicOptions> options) : IAnthropicChatClient
{
    private static readonly Uri DefaultBaseUrl = new("https://api.anthropic.com/");
    private const string AnthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async IAsyncEnumerable<AnthropicStreamEvent> StreamAsync(
        AnthropicRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new InvalidOperationException("Anthropic ApiKey is not configured.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl)
            ? DefaultBaseUrl
            : new Uri(opts.BaseUrl);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUrl, "v1/messages"));
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        requestMessage.Headers.TryAddWithoutValidation("x-api-key", opts.ApiKey);
        requestMessage.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);

        var body = new ApiRequestBody
        {
            Model = request.Model,
            System = request.SystemPrompt,
            Messages = [.. request.Messages.Select(m => new ApiMessage { Role = m.Role, Content = m.Content })],
            MaxTokens = request.MaxOutputTokens ?? 4096,
            Stream = true,
            Thinking = request.ExtendedThinking
                ? new ApiThinking { Type = "enabled", BudgetTokens = 8000 }
                : null
        };
        var bodyJson = JsonSerializer.Serialize(body, Json);
        requestMessage.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            yield return new AnthropicStreamEvent.Failed($"HTTP {(int)response.StatusCode}: {errorBody}");
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // SSE parser: events are separated by blank lines. We only care about
        // the `data:` lines (we ignore explicit `event:` lines because the
        // payload's "type" field is authoritative on the wire).
        var dataBuffer = new StringBuilder();
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
                    foreach (var evt in ParseEvent(payload))
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
            // event: / id: / retry: lines are intentionally ignored.
        }

        if (dataBuffer.Length > 0)
        {
            foreach (var evt in ParseEvent(dataBuffer.ToString()))
            {
                yield return evt;
            }
        }
    }

    private static IEnumerable<AnthropicStreamEvent> ParseEvent(string payload)
    {
        // Anthropic streaming events: message_start, content_block_start,
        // content_block_delta, content_block_stop, message_delta, message_stop,
        // ping, error. Loom only forwards the events that mean something to
        // the engine; ping is dropped silently.
        var parseResult = TryParse(payload);
        if (parseResult.Error is not null)
        {
            yield return new AnthropicStreamEvent.Failed($"Could not parse event: {parseResult.Error}");
            yield break;
        }
        var parsed = parseResult.Payload;
        if (parsed is null)
        {
            yield break;
        }

        switch (parsed.Type)
        {
            case "message_start":
                if (parsed.Message is { } m)
                {
                    yield return new AnthropicStreamEvent.Started(m.Id ?? "", m.Model ?? "");
                    if (m.Usage is { } usage)
                    {
                        yield return new AnthropicStreamEvent.Usage(usage.InputTokens, usage.OutputTokens);
                    }
                }
                break;

            case "content_block_delta":
                if (parsed.Delta is { Type: "text_delta", Text: { Length: > 0 } text })
                {
                    yield return new AnthropicStreamEvent.TextDelta(text);
                }
                break;

            case "message_delta":
                if (parsed.Usage is { } finalUsage)
                {
                    yield return new AnthropicStreamEvent.Usage(finalUsage.InputTokens, finalUsage.OutputTokens);
                }
                if (parsed.Delta is { StopReason: { Length: > 0 } reason })
                {
                    yield return new AnthropicStreamEvent.Stopped(reason);
                }
                break;

            case "message_stop":
                // Sometimes the StopReason isn't on message_delta — emit a
                // generic Stopped on message_stop if we haven't already.
                yield return new AnthropicStreamEvent.Stopped("end_turn");
                break;

            case "error":
                yield return new AnthropicStreamEvent.Failed(parsed.Error?.Message ?? "unknown error");
                break;
        }
    }

    private static (AnthropicStreamPayload? Payload, string? Error) TryParse(string payload)
    {
        try
        {
            return (JsonSerializer.Deserialize<AnthropicStreamPayload>(payload, Json), null);
        }
        catch (JsonException ex)
        {
            return (null, ex.Message);
        }
    }

    // Wire shapes — internal, never escape this assembly.

    private sealed class ApiRequestBody
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("system")] public string? System { get; set; }
        [JsonPropertyName("messages")] public List<ApiMessage> Messages { get; set; } = [];
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("thinking")] public ApiThinking? Thinking { get; set; }
    }

    private sealed class ApiMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class ApiThinking
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("budget_tokens")] public int BudgetTokens { get; set; }
    }

    private sealed class AnthropicStreamPayload
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("message")] public StreamMessage? Message { get; set; }
        [JsonPropertyName("delta")] public StreamDelta? Delta { get; set; }
        [JsonPropertyName("usage")] public StreamUsage? Usage { get; set; }
        [JsonPropertyName("error")] public StreamError? Error { get; set; }
    }

    private sealed class StreamMessage
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("usage")] public StreamUsage? Usage { get; set; }
    }

    private sealed class StreamDelta
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("stop_reason")] public string? StopReason { get; set; }
    }

    private sealed class StreamUsage
    {
        [JsonPropertyName("input_tokens")] public int InputTokens { get; set; }
        [JsonPropertyName("output_tokens")] public int OutputTokens { get; set; }
    }

    private sealed class StreamError
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
