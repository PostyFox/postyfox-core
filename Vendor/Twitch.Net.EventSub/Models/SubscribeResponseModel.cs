using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Models;

public class SubscribeResponseModel
{
    [JsonPropertyName("data")]
    public SubscribeResponseDataModel[] Data { get; init; } = Array.Empty<SubscribeResponseDataModel>();

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("total_cost")]
    public int TotalCost { get; init; }

    [JsonPropertyName("max_total_cost")]
    public int MaxTotalCost { get; init; }

    [JsonPropertyName("pagination")]
    public PaginationDataModel Pagination { get; init; } = new(); // make sure it is never null
}

public class SubscribeResponseDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("cost")]
    public int Cost { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("transport")]
    public SubscribeResponseDataTransportModel Transport { get; init; } = null!;
}

public class SubscribeResponseDataTransportModel
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("callback")]
    public string Callback { get; init; } = string.Empty;
}

public class PaginationDataModel
{
    [JsonPropertyName("cursor")]
    public string Cursor { get; init; } = string.Empty;

    public bool HasCursor => !string.IsNullOrWhiteSpace(Cursor);
}