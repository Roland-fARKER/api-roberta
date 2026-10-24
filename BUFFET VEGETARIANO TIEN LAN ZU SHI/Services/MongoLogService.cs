using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TIEN_LAN_ZU_SHI.Configurations;
using TIEN_LAN_ZU_SHI.Models;

namespace TIEN_LAN_ZU_SHI.Services;

public interface ILogService
{
    Task InsertLogAsync(SystemLog log);
    Task<List<SystemLog>> GetRecentLogsAsync(int count = 20);
}

public class MongoLogService : ILogService
{
    private readonly IMongoCollection<SystemLog> _collection;

    public MongoLogService(IOptions<MongoLogSettings> options)
    {
        var settings = options.Value;

        // Timeout a 15s para dar tiempo a la negociación SSL inicial en Azure
        var mongoSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);

        var client = new MongoClient(mongoSettings);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<SystemLog>(settings.CollectionName);

        try
        {
            InicializarIndices(settings.RetentionDays);
        }
        catch (Exception ex)
        {
            // Si Mongo no está disponible al arrancar, la API NO debe caerse
            Console.WriteLine($"[WARNING] No se pudieron crear los índices de MongoDB: {ex.Message}");
        }
    }

    private void InicializarIndices(int retentionDays)
    {
        // 1. Índice TTL (Time-To-Live) para purga automática
        var ttlKeys = Builders<SystemLog>.IndexKeys.Ascending(x => x.Timestamp);
        var ttlOptions = new CreateIndexOptions
        {
            ExpireAfter = TimeSpan.FromDays(retentionDays)
        };

        // 2. Índices de consulta frecuente
        var traceKeys = Builders<SystemLog>.IndexKeys.Ascending(x => x.TraceId);
        var levelKeys = Builders<SystemLog>.IndexKeys.Ascending(x => x.Level);

        _collection.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<SystemLog>(ttlKeys, ttlOptions),
            new CreateIndexModel<SystemLog>(traceKeys),
            new CreateIndexModel<SystemLog>(levelKeys)
        });
    }

    public async Task InsertLogAsync(SystemLog log)
    {
        try
        {
            await _collection.InsertOneAsync(log);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITICAL] Fallo de persistencia en MongoDB: {ex.Message}");
        }
    }

    public async Task<List<SystemLog>> GetRecentLogsAsync(int count = 20)
    {
        try
        {
            return await _collection.Find(_ => true)
                .SortByDescending(x => x.Timestamp)
                .Limit(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error leyendo logs de MongoDB: {ex.Message}");
            return new List<SystemLog>
            {
                new SystemLog
                {
                    Level = "Error",
                    Environment = $"Detalle del error en Cosmos DB: {ex.Message}",
                    TraceId = "MONGO_QUERY_ERROR",
                    Timestamp = DateTime.UtcNow
                }
            };
        }
    }
}