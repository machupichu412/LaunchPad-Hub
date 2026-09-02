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

// --- Networking ---
// Required in every environment, not just Prod (see network.bicep) — Microsoft's
// internal CloudGov policy set denies public network access on Key Vault/Storage/SQL
// tenant-wide, so private endpoints + VNet integration aren't a Prod-only hardening
// step here, they're the only way any of this deploys at all.
module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    env: env
    location: location
  }
}

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
    privateEndpointSubnetId: network.outputs.peSubnetId
    privateDnsZoneId: network.outputs.keyVaultDnsZoneId
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
    privateEndpointSubnetId: network.outputs.peSubnetId
    privateDnsZoneId: network.outputs.sqlDnsZoneId
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    env: env
    location: location
    privateEndpointSubnetId: network.outputs.peSubnetId
    blobPrivateDnsZoneId: network.outputs.blobDnsZoneId
    queuePrivateDnsZoneId: network.outputs.queueDnsZoneId
    tablePrivateDnsZoneId: network.outputs.tableDnsZoneId
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
    vnetIntegrationSubnetId: network.outputs.appSubnetId
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
    privateEndpointSubnetId: network.outputs.peSubnetId
    blobPrivateDnsZoneId: network.outputs.blobDnsZoneId
    queuePrivateDnsZoneId: network.outputs.queueDnsZoneId
    tablePrivateDnsZoneId: network.outputs.tableDnsZoneId
    vnetIntegrationSubnetId: network.outputs.funcSubnetId
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
// Each grant lives in its own tiny module (keyVaultAccess/storageAccess/serviceBusAccess)
// rather than as a top-level `existing` resource + roleAssignment here. A roleAssignment's
// `name` (a guid()) must be resolvable "at the start of the deployment" (BCP120), and a
// SystemAssigned identity's principalId — read via another module's .outputs — isn't
// resolvable at that point when referenced directly in this scope. Passing it as a plain
// string *parameter* into a dedicated module sidesteps the restriction; that module then
// declares its own `existing` reference to the target resource and does the assignment
// entirely within its own deployment scope, where the incoming principalId is just an
// opaque parameter rather than a cross-module output chain.
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var serviceBusDataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var serviceBusDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

module keyVaultAccess 'modules/keyVaultAccess.bicep' = {
  name: 'keyVaultAccess'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    roleAssignments: [
      { principalId: appService.outputs.appServicePrincipalId, roleDefinitionId: keyVaultSecretsUserRoleId }
      { principalId: functionApp.outputs.functionAppPrincipalId, roleDefinitionId: keyVaultSecretsUserRoleId }
    ]
  }
}

module storageAccess 'modules/storageAccess.bicep' = {
  name: 'storageAccess'
  params: {
    storageAccountName: storage.outputs.storageAccountName
    roleAssignments: [
      { principalId: appService.outputs.appServicePrincipalId, roleDefinitionId: storageBlobDataContributorRoleId }
      { principalId: functionApp.outputs.functionAppPrincipalId, roleDefinitionId: storageBlobDataContributorRoleId }
    ]
  }
}

// The Functions module's own dedicated storage account (deployment package + runtime
// bookkeeping — see functions.bicep) — separate grant from the shared business-data
// account above, scoped to just this one account.
module functionsStorageAccess 'modules/storageAccess.bicep' = {
  name: 'functionsStorageAccess'
  params: {
    storageAccountName: functionApp.outputs.functionsStorageAccountName
    roleAssignments: [
      { principalId: functionApp.outputs.functionAppPrincipalId, roleDefinitionId: storageBlobDataOwnerRoleId }
      { principalId: functionApp.outputs.functionAppPrincipalId, roleDefinitionId: storageQueueDataContributorRoleId }
      { principalId: functionApp.outputs.functionAppPrincipalId, roleDefinitionId: storageTableDataContributorRoleId }
    ]
  }
}

module serviceBusAccess 'modules/serviceBusAccess.bicep' = {
  name: 'serviceBusAccess'
  params: {
    serviceBusNamespaceName: serviceBus.outputs.namespaceName
    roleAssignments: [
      { principalId: appService.outputs.appServicePrincipalId, roleDefinitionId: serviceBusDataSenderRoleId }
      { principalId: functionApp.outputs.functionAppPrincipalId, roleDefinitionId: serviceBusDataReceiverRoleId }
    ]
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
