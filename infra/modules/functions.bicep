@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

param sqlServerFqdn string
param sqlDatabaseName string
param appInsightsConnectionString string
param serviceBusNamespace string
param storageAccountName string

@description('Delegated subnet (Microsoft.Web/serverfarms) for regional VNet integration — required to reach SQL/Storage now that they\'re private-endpoint-only')
param vnetIntegrationSubnetId string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

// Function App default hostnames are globally unique across all of Azure, not just this
// subscription — same fix as appService.bicep's uniqueSuffix.
var uniqueSuffix = substring(uniqueString(subscription().id, resourceGroup().id), 0, 4)

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
  name: 'func-launchpad-${env}-${uniqueSuffix}'
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionsPlan.id
    httpsOnly: true
    virtualNetworkSubnetId: vnetIntegrationSubnetId
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      // All outbound traffic routes through the VNet, not just RFC1918 ranges — SQL/Storage
      // are reachable only via their private endpoints now.
      vnetRouteAllEnabled: true
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
