using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RepairPlanner.Services;

/// <summary>
/// Service for interacting with Azure Cosmos DB.
/// </summary>
public sealed class CosmosDbService : IDisposable
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _techniciansContainer;
    private readonly Container _partsInventoryContainer;
    private readonly Container _workOrdersContainer;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(CosmosDbOptions options, ILogger<CosmosDbService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (string.IsNullOrWhiteSpace(options?.Endpoint))
            throw new ArgumentException("Cosmos DB endpoint is required", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Key))
            throw new ArgumentException("Cosmos DB key is required", nameof(options));
        if (string.IsNullOrWhiteSpace(options.DatabaseName))
            throw new ArgumentException("Cosmos DB database name is required", nameof(options));

        _logger.LogInformation("Initializing Cosmos DB client for database: {DatabaseName}", options.DatabaseName);

        _cosmosClient = new CosmosClient(options.Endpoint, options.Key);
        _database = _cosmosClient.GetDatabase(options.DatabaseName);
        _techniciansContainer = _database.GetContainer(options.TechniciansContainer);
        _partsInventoryContainer = _database.GetContainer(options.PartsInventoryContainer);
        _workOrdersContainer = _database.GetContainer(options.WorkOrdersContainer);
    }

    /// <summary>
    /// Queries technicians who have any of the required skills and are available.
    /// </summary>
    /// <param name="requiredSkills">List of skills to match against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available technicians with matching skills.</returns>
    public async Task<List<Technician>> QueryTechniciansAsync(
        IReadOnlyList<string> requiredSkills,
        CancellationToken cancellationToken = default)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
        {
            _logger.LogWarning("QueryTechniciansAsync called with no required skills");
            return new List<Technician>();
        }

        try
        {
            _logger.LogInformation("Querying technicians with skills: {Skills}", string.Join(", ", requiredSkills));

            // Query for available technicians with any of the required skills
            // Using WHERE EXISTS to check if skills array contains any required skill
            var queryText = @"
                SELECT * FROM Technicians t 
                WHERE t.availability = 'available'
                AND EXISTS(
                    SELECT VALUE skill FROM skill IN t.skills 
                    WHERE skill IN (@skills)
                )";

            var queryDefinition = new QueryDefinition(queryText)
                .WithParameter("@skills", string.Join(",", requiredSkills));

            var technicians = new List<Technician>();
            using var iterator = _techniciansContainer.GetItemQueryIterator<Technician>(queryDefinition);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                technicians.AddRange(response);
                
                _logger.LogDebug("Retrieved {Count} technicians from page (RU: {RU})", 
                    response.Count, response.RequestCharge);
            }

            _logger.LogInformation("Found {Count} available technicians matching required skills", technicians.Count);
            return technicians;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Technicians container not found: {Message}", ex.Message);
            return new List<Technician>();
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error querying technicians. Status: {Status}, Message: {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error querying technicians");
            throw;
        }
    }

    /// <summary>
    /// Fetches parts from inventory by part numbers.
    /// </summary>
    /// <param name="partNumbers">List of part numbers to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of parts found in inventory.</returns>
    public async Task<List<Part>> GetPartsByNumbersAsync(
        IReadOnlyList<string> partNumbers,
        CancellationToken cancellationToken = default)
    {
        if (partNumbers == null || partNumbers.Count == 0)
        {
            _logger.LogDebug("GetPartsByNumbersAsync called with no part numbers");
            return new List<Part>();
        }

        try
        {
            _logger.LogInformation("Fetching parts: {PartNumbers}", string.Join(", ", partNumbers));

            // Query for parts by part numbers
            var queryText = "SELECT * FROM PartsInventory p WHERE p.partNumber IN (@partNumbers)";
            var queryDefinition = new QueryDefinition(queryText)
                .WithParameter("@partNumbers", string.Join(",", partNumbers));

            var parts = new List<Part>();
            using var iterator = _partsInventoryContainer.GetItemQueryIterator<Part>(queryDefinition);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                parts.AddRange(response);
                
                _logger.LogDebug("Retrieved {Count} parts from page (RU: {RU})", 
                    response.Count, response.RequestCharge);
            }

            _logger.LogInformation("Found {Count} parts in inventory", parts.Count);

            // Log any missing parts
            var foundPartNumbers = parts.Select(p => p.PartNumber).ToHashSet();
            var missingParts = partNumbers.Where(pn => !foundPartNumbers.Contains(pn)).ToList();
            if (missingParts.Count > 0)
            {
                _logger.LogWarning("Parts not found in inventory: {MissingParts}", string.Join(", ", missingParts));
            }

            return parts;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("PartsInventory container not found: {Message}", ex.Message);
            return new List<Part>();
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error fetching parts. Status: {Status}, Message: {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching parts");
            throw;
        }
    }

    /// <summary>
    /// Creates a new work order in Cosmos DB.
    /// </summary>
    /// <param name="workOrder">The work order to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created work order with ID populated.</returns>
    public async Task<WorkOrder> CreateWorkOrderAsync(
        WorkOrder workOrder,
        CancellationToken cancellationToken = default)
    {
        if (workOrder == null)
            throw new ArgumentNullException(nameof(workOrder));

        try
        {
            // Generate ID if not set
            if (string.IsNullOrWhiteSpace(workOrder.Id))
            {
                workOrder.Id = Guid.NewGuid().ToString();
            }

            // Set timestamps
            var now = DateTime.UtcNow;
            workOrder.CreatedAt = now;
            workOrder.UpdatedAt = now;

            _logger.LogInformation("Creating work order {WorkOrderNumber} for machine {MachineId}", 
                workOrder.WorkOrderNumber, workOrder.MachineId);

            // Partition key is "status" for WorkOrders container
            var response = await _workOrdersContainer.CreateItemAsync(
                workOrder,
                new PartitionKey(workOrder.Status),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Successfully created work order {Id} (RU: {RU})", 
                response.Resource.Id, 
                response.RequestCharge);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogError("Work order with ID {Id} already exists", workOrder.Id);
            throw new InvalidOperationException($"Work order with ID {workOrder.Id} already exists", ex);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error creating work order. Status: {Status}, Message: {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating work order");
            throw;
        }
    }

    public void Dispose()
    {
        _cosmosClient?.Dispose();
        _logger.LogInformation("Cosmos DB client disposed");
    }
}
