using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a maintenance technician with skills and availability.
/// Queried from Cosmos DB Technicians container.
/// </summary>
public sealed class Technician
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("skills")]
    [JsonProperty("skills")]
    public List<string> Skills { get; set; } = [];

    [JsonPropertyName("availability")]
    [JsonProperty("availability")]
    public string Availability { get; set; } = string.Empty; // "available", "busy", "off-duty"

    [JsonPropertyName("currentWorkOrders")]
    [JsonProperty("currentWorkOrders")]
    public List<string> CurrentWorkOrders { get; set; } = [];

    [JsonPropertyName("certifications")]
    [JsonProperty("certifications")]
    public List<string> Certifications { get; set; } = [];

    [JsonPropertyName("yearsOfExperience")]
    [JsonProperty("yearsOfExperience")]
    public int YearsOfExperience { get; set; }

    [JsonPropertyName("contactInfo")]
    [JsonProperty("contactInfo")]
    public string ContactInfo { get; set; } = string.Empty;
}
