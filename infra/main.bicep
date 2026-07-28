@description('Environment name: dev, test, or prod')
@allowed([ 'dev', 'test', 'prod' ])
param env string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Entra object ID of the SG-LaunchPad-ProgramOps-equivalent DBA group for SQL Entra-only administration')
param sqlAadAdminObjectId string

@description('Display name of that Entra admin principal')
param sqlAadAdminLogin string

var isProd = env == 'prod'

// --- Observability ---
module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    env: env
    location: location
  }
}

module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    env: env
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

// --- Secrets ---
module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    env: env
    location: location
    tenantId: subscription().tenantId
    publicNetworkAccess: !isProd
  }
}

// --- Data ---
module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    env: env
    location: location
    aadAdminObjectId: sqlAadAdminObjectId
    aadAdminLogin: sqlAadAdminLogin
    autoPauseDelayMinutes: isProd ? -1 : 60
    maxVCore: isProd ? 8 : 2
    zoneRedundant: isProd
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    env: env
    location: location
    publicNetworkAccess: !isProd
  }
}

module serviceBus 'modules/serviceBus.bicep' = {
  name: 'serviceBus'
  params: {
    env: env
    location: location
    publicNetworkAccess: !isProd
  }
}

// --- Compute ---
module appService 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    env: env
    location: location
    skuName: isProd ? 'P0v3' : 'B1'
    sqlServerFqdn: sql.outputs.sqlServerFqdn
    sqlDatabaseName: sql.outputs.sqlDatabaseName
    keyVaultUri: keyVault.outputs.keyVaultUri
    appInsightsConnectionString: appInsights.outputs.connectionString
    serviceBusNamespace: serviceBus.outputs.namespaceName
    deployStagingSlot: isProd
  }
}

module functionApp 'modules/functions.bicep' = {
  name: 'functionApp'
  params: {
    env: env
    location: location
    sqlServerFqdn: sql.outputs.sqlServerFqdn
    sqlDatabaseName: sql.outputs.sqlDatabaseName
    appInsightsConnectionString: appInsights.outputs.connectionString
    serviceBusNamespace: serviceBus.outputs.namespaceName
    storageAccountName: storage.outputs.storageAccountName
  }
}

module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    env: env
    location: location
    skuName: isProd ? 'Standard' : 'Free'
  }
}

// --- Managed identity → data-plane RBAC (no connection-string secrets anywhere) ---
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var serviceBusDataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var serviceBusDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

resource kvVaultRef 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVault.outputs.keyVaultName
}
resource storageAccountRef 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storage.outputs.storageAccountName
}
resource serviceBusNamespaceRef 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: serviceBus.outputs.namespaceName
}

resource apiKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvVaultRef.id, appService.outputs.appServicePrincipalId, keyVaultSecretsUserRoleId)
  scope: kvVaultRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: appService.outputs.appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource apiStorageAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountRef.id, appService.outputs.appServicePrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccountRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: appService.outputs.appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource apiServiceBusSendAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceRef.id, appService.outputs.appServicePrincipalId, serviceBusDataSenderRoleId)
  scope: serviceBusNamespaceRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderRoleId)
    principalId: appService.outputs.appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource functionsStorageAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountRef.id, functionApp.outputs.functionAppPrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccountRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionApp.outputs.functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource functionsServiceBusReceiveAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceRef.id, functionApp.outputs.functionAppPrincipalId, serviceBusDataReceiverRoleId)
  scope: serviceBusNamespaceRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiverRoleId)
    principalId: functionApp.outputs.functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource functionsKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvVaultRef.id, functionApp.outputs.functionAppPrincipalId, keyVaultSecretsUserRoleId)
  scope: kvVaultRef
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: functionApp.outputs.functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// NOTE: Azure SQL Database has no Azure RBAC data plane. Granting db_datareader /
// db_datawriter / EXECUTE to the App Service and Functions identities requires
// `CREATE USER [app-launchpad-<env>] FROM EXTERNAL PROVIDER` run by the deployment
// identity — do this as a post-deploy script step in CI, not here. See §9.2/§9.4.

output appServiceHostName string = appService.outputs.defaultHostName
output staticWebAppHostName string = staticWebApp.outputs.defaultHostname
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output functionAppName string = functionApp.outputs.functionAppName
