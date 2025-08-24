# Event Sourcing Azure Function

Este proyecto implementa un patrón Event Sourcing usando Azure Functions y Azure Cosmos DB.

## Requisitos
- .NET 8 o superior
- Azure Functions Core Tools
- Cuenta de Azure y recurso de Azure Cosmos DB (API SQL)
- Cuenta de Azure Functions creada

## Configuración local
1. Clona el repositorio y entra al directorio del proyecto.
2. Crea un archivo `local.settings.json` en la raíz del proyecto con el siguiente contenido (reemplaza los valores por los de tu cuenta):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "CosmosDbConnectionString": "<TU_COSMOSDB_CONNECTION_STRING>"
  }
}
```

3. Instala las dependencias necesarias:
```sh
 dotnet restore
```

## Ejecución local
1. Inicia la función localmente:
```sh
func start
```
2. La función estará disponible en `http://localhost:7071`.

## Despliegue en Azure
1. Publica la función a tu recurso de Azure Functions:
```sh
func azure functionapp publish <NOMBRE_DE_TU_FUNCTION_APP>
```
2. En el portal de Azure, ve a tu Function App > Settings > Configuration y agrega la variable `CosmosDbConnectionString` con el valor de tu connection string de Cosmos DB.
3. Asegúrate de que la base de datos y el contenedor existen en Cosmos DB:
   - Base de datos: `banking-events`
   - Contenedor: `Events`
   - Partition Key: `/aggregateId`

## Pruebas (Testing)

### Crear una cuenta
Haz un POST con curl (reemplaza la URL y el code por los de tu función):

```sh
curl -X POST "https://<TU_FUNCTION_APP>.azurewebsites.net/api/commands/CreateAccount?code=<TU_CODE>&accountId=acc-123" \
  -H "Content-Type: application/json" \
  -d '{"name": "Cuenta de prueba", "initialBalance": 500}'
```

### Hacer un depósito
```sh
curl -X POST "https://<TU_FUNCTION_APP>.azurewebsites.net/api/commands/Deposit?code=<TU_CODE>&accountId=acc-123" \
  -H "Content-Type: application/json" \
  -d '{}'
```

### Hacer un retiro
```sh
curl -X POST "https://<TU_FUNCTION_APP>.azurewebsites.net/api/commands/Withdraw?code=<TU_CODE>&accountId=acc-123" \
  -H "Content-Type: application/json" \
  -d '{}'
```

## Verificación
- Los eventos se almacenan en el contenedor `Events` de tu base de datos `banking-events` en Cosmos DB.
- Puedes ver los documentos usando el Data Explorer del portal de Azure Cosmos DB.

## Notas
- No compartas tu connection string ni tu code de Azure Functions públicamente.
- Si tienes problemas con el Partition Key, asegúrate de que el valor de `aggregateId` en el documento y el Partition Key enviado sean idénticos.
