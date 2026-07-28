@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

param sqlServerFqdn string
param sqlDatabaseName string
param appInsightsConnectionString string
param serviceBusNamespace string
param storageAccountName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource functionsPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-launchpad-fn-${env}'
  location: location
  kind: 'functionapp,linux'
  properties: {
    reserved: true
  }
  sku: {
    name: 'EP1'
    tier: 'ElasticPremium'
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-launchpad-${env}'
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionsPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      appSettings: [
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'AzureWebJobsStorage__accountName', value: storageAccount.name }
        { name: 'ConnectionStrings__Sql', value: 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
        { name: 'ServiceBusConnection__fullyQualifiedNamespace', value: '${serviceBusNamespace}.servicebus.windows.net' }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output functionAppPrincipalId string = functionApp.identity.principalId
