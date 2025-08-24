using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace icesi.ingesoft
{
    public class CommandHandler
    {
        private readonly ILogger<CommandHandler> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        public CommandHandler(ILogger<CommandHandler> logger)
        {
            _logger = logger;
            string connectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString");
            _cosmosClient = new CosmosClient(connectionString);
            // Cambia estos nombres por los de tu base de datos y contenedor reales
            _container = _cosmosClient.GetContainer("banking-events", "events");
        }

        [Function("CommandHandler")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "commands/{command}")] HttpRequest req,
            string command)
        {
            // 1. Obtener accountId del query string
            var accountId = req.Query["accountId"];
            if (string.IsNullOrEmpty(accountId)) return new BadRequestObjectResult("Falta accountId en los parámetros");

            // 2. Simular generación de evento
            var newEvent = new
            {
                id = Guid.NewGuid().ToString(),
                aggregateId = accountId,
                eventType = command switch
                {
                    "CreateAccount" => "AccountCreated",
                    "Deposit" => "MoneyDeposited",
                    "Withdraw" => "MoneyWithdrawn",
                    _ => throw new ArgumentException("Comando no válido")
                },
                version = 1,
                data = command switch
                {
                    "CreateAccount" => (object)new { name = "Cuenta de prueba", initialBalance = 500 },
                    "Deposit" => (object)new { amount = 100 },
                    "Withdraw" => (object)new { amount = 50 },
                    _ => null
                },
                timestamp = DateTime.UtcNow
            };

            // 3. Guardar evento en Cosmos DB
            await _container.CreateItemAsync(newEvent, new PartitionKey(accountId));

            // 4. Registrar evento en logs
            _logger.LogInformation($"Evento generado: {JsonConvert.SerializeObject(newEvent)}");

            // 5. Respuesta de éxito
            return new OkObjectResult(new { status = "Evento guardado", @event = newEvent });
        }
    }
}