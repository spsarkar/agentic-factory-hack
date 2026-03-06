using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a part required for a work order.
/// </summary>
public sealed class WorkOrderPartUsage
{
    [JsonPropertyName("partId")]
    [JsonProperty("partId")]
    public string PartId { get; set; } = string.Empty;

    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    [JsonPropertyName("partName")]
    [JsonProperty("partName")]
    public string PartName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    [JsonProperty("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("totalCost")]
    [JsonProperty("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "required"; // "required", "allocated", "used"
}
