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
            _container = _cosmosClient.GetContainer("banking-events", "Events");
        }

        // Clase para deserializar el body de CreateAccount
        private class CreateAccountBody
        {
            public string name { get; set; }
            public int initialBalance { get; set; }
        }

        [Function("CommandHandler")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "commands/{command}")] HttpRequest req,
            string command)
        {
            try
            {
                // 1. Obtener accountId del query string

                var accountId = req.Query["accountId"].ToString();
                if (string.IsNullOrEmpty(accountId)) return new BadRequestObjectResult("Falta accountId en los parámetros");
                object eventData = null;
                string requestBody = null;
                using (var reader = new System.IO.StreamReader(req.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                _logger.LogInformation($"[DEBUG] requestBody: {requestBody}");

                if (command == "CreateAccount")
                {
                    if (string.IsNullOrWhiteSpace(requestBody))
                        return new BadRequestObjectResult("El body no puede estar vacío para CreateAccount");

                    var bodyData = JsonConvert.DeserializeObject<CreateAccountBody>(requestBody);
                    if (bodyData == null || string.IsNullOrWhiteSpace(bodyData.name) || bodyData.initialBalance <= 0)
                        return new BadRequestObjectResult("El body debe tener 'name' y 'initialBalance' (mayor a 0)");

                    eventData = new { name = bodyData.name, initialBalance = bodyData.initialBalance };
                }
                else if (command == "Deposit" || command == "Withdraw")
                {
                    int amount = command == "Deposit" ? 100 : 50;
                    if (!string.IsNullOrWhiteSpace(requestBody))
                    {
                        try
                        {
                            dynamic bodyData = JsonConvert.DeserializeObject(requestBody);
                            if (bodyData != null && bodyData.amount != null)
                            {
                                amount = (int)bodyData.amount;
                            }
                        }
                        catch { /* Si falla, se usa el valor por defecto */ }
                    }
                    eventData = new { amount = amount };
                }
                else
                {
                    throw new ArgumentException("Comando no válido");
                }

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
                    data = eventData,
                    timestamp = DateTime.UtcNow
                };

                // Log de depuración antes de guardar
                _logger.LogInformation($"[DEBUG] accountId: {accountId}");
                _logger.LogInformation($"[DEBUG] newEvent: {JsonConvert.SerializeObject(newEvent)}");
                _logger.LogInformation($"[DEBUG] PartitionKey enviado: {accountId}");

                // 3. Guardar evento en Cosmos DB
                await _container.CreateItemAsync(newEvent, new PartitionKey(accountId));

                // 4. Registrar evento en logs
                _logger.LogInformation($"Evento generado: {JsonConvert.SerializeObject(newEvent)}");

                // 5. Respuesta de éxito
                return new OkObjectResult(new { status = "Evento guardado", @event = newEvent });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el comando: {Message}", ex.Message);
                return new ObjectResult(new { status = "error", message = ex.Message, exception = ex.ToString() }) { StatusCode = 500 };
            }
        }
    }
}