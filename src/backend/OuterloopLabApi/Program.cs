using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.CosmosDB.Models;
using Azure;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using OuterloopLabApi.Data;
using OuterloopLabApi.Models;
using OuterloopLabApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Health endpoint for container readiness.
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpClient();

var settings = AppSettingsExtensions.FromEnvironment();

// Token-based auth for Cosmos DB data-plane.
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ManagedIdentityClientId = settings.ManagedIdentityClientId,
});

var cosmosClient = new CosmosClient(settings.CosmosDbUri, credential);

// Control plane provisioning is best-effort.
try
{
    await ArmProvisionCosmosAsync(settings, credential);
}
catch
{
    // Best-effort: do not block startup.
}

// Mandatory data-plane create-if-not-exists (startup must fail if this fails).
var dbResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(settings.CosmosDbDatabase);
var containerResponse = await dbResponse.Database.CreateContainerIfNotExistsAsync(
    settings.CosmosDbContainer,
    "/conversionId",
    throughput: 400);
var container = containerResponse.Resource;

builder.Services.AddSingleton(container);
builder.Services.AddSingleton<ICosmosConversionAuditRepository, CosmosConversionAuditRepository>();

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<ICurrencyRateProvider, FrankfurterCurrencyRateProvider>();
builder.Services.AddScoped<CurrencyConversionService>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapControllers();

app.Run();

static async Task ArmProvisionCosmosAsync(AppSettings settings, TokenCredential credential)
{
    // Best-effort control-plane provisioning.
    // Data-plane create-if-not-exists is the authoritative provisioning path and will fail startup if it cannot create resources.
    // ARM is attempted only so the solution complies with control-plane provisioning requirements.
    try
    {
        var subscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
        if (string.IsNullOrWhiteSpace(subscriptionId)) return;

        var armClient = new ArmClient(credential, subscriptionId);

        var cosmosAccountResourceId = new ResourceIdentifier(
            $"/subscriptions/{subscriptionId}/resourceGroups/{settings.CosmosDbResourceGroup}/providers/Microsoft.DocumentDB/databaseAccounts/{settings.CosmosDbAccountName}");

        // CosmosDBAccountResource can be constructed from ArmClient and a ResourceIdentifier.
        // We use reflection so this remains best-effort even if SDK method names change.
        object? cosmosAccountObj = null;
        var getResourceMethods = armClient.GetType().GetMethods().Where(m => m.Name == "GetResource").ToArray();
        foreach (var method in getResourceMethods)
        {
            if (!method.IsGenericMethodDefinition) continue;
            var genericArgs = method.GetGenericArguments();
            if (genericArgs.Length != 1) continue;

            var closed = method.MakeGenericMethod(typeof(CosmosDBAccountResource));
            var parameters = method.GetParameters();
            if (parameters.Length == 1)
            {
                cosmosAccountObj = closed.Invoke(armClient, new object[] { cosmosAccountResourceId });
                break;
            }
            if (parameters.Length == 2)
            {
                cosmosAccountObj = closed.Invoke(armClient, new object[] { cosmosAccountResourceId, CancellationToken.None });
                break;
            }
        }

        if (cosmosAccountObj is null) return;

        dynamic cosmosAccount = cosmosAccountObj;

        var region = (AzureLocation)settings.CosmosDbRegion;
        var dbInfo = new CosmosDBSqlDatabaseResourceInfo(settings.CosmosDbDatabase);
        var dbContent = new CosmosDBSqlDatabaseCreateOrUpdateContent(region, dbInfo);

        // Create/update the SQL database.
        dynamic dbOps = cosmosAccount.GetCosmosDBSqlDatabases().CreateOrUpdate(WaitUntil.Completed, settings.CosmosDbDatabase, dbContent, CancellationToken.None);
        try
        {
            await dbOps.WaitForCompletionAsync();
        }
        catch
        {
            // Best-effort.
        }

        var partitionKey = new CosmosDBContainerPartitionKey
        {
            Paths = new List<string> { "/conversionId" },
            Kind = CosmosDBPartitionKind.Hash,
            Version = 2,
        };

        var containerInfo = new CosmosDBSqlContainerResourceInfo(settings.CosmosDbContainer)
        {
            PartitionKey = partitionKey,
        };
        var containerContent = new CosmosDBSqlContainerCreateOrUpdateContent(region, containerInfo);

        // Create/update the SQL container.
        dynamic containerOps = cosmosAccount
            .GetCosmosDBSqlDatabases()
            .GetCosmosDBSqlDatabaseResource(settings.CosmosDbDatabase)
            .GetCosmosDBSqlContainers()
            .CreateOrUpdate(WaitUntil.Completed, settings.CosmosDbContainer, containerContent, CancellationToken.None);

        try
        {
            await containerOps.WaitForCompletionAsync();
        }
        catch
        {
            // Best-effort.
        }
    }
    catch
    {
        // Best-effort: never block startup.
    }
}

public sealed record AppSettings(
    string CosmosDbUri,
    string CosmosDbDatabase,
    string CosmosDbContainer,
    string CosmosDbAccountName,
    string CosmosDbResourceGroup,
    string CosmosDbRegion,
    string ManagedIdentityClientId,
    string CurrencyApiBaseUrl);

public static class AppSettingsExtensions
{
    public static AppSettings FromEnvironment()
    {
        static string Required(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required environment variable: {key}");
            }
            return value;
        }

        var currencyApiBaseUrl = Environment.GetEnvironmentVariable("CURRENCY_API_BASE_URL")
            ?? "https://frankfurter.dev";

        return new AppSettings(
            CosmosDbUri: Required("COSMOS_DB_URI"),
            CosmosDbDatabase: Required("COSMOS_DB_DATABASE"),
            CosmosDbContainer: Required("COSMOS_DB_CONTAINER"),
            CosmosDbAccountName: Required("COSMOS_DB_ACCOUNT_NAME"),
            CosmosDbResourceGroup: Required("COSMOS_DB_RESOURCE_GROUP"),
            CosmosDbRegion: Required("COSMOS_DB_REGION"),
            ManagedIdentityClientId: Required("AZURE_MANAGED_IDENTITY_CLIENT_ID"),
            CurrencyApiBaseUrl: currencyApiBaseUrl);
    }
}
