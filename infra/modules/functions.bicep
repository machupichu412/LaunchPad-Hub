@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

param sqlServerFqdn string
param sqlDatabaseName string
param appInsightsConnectionString string
param serviceBusNamespace string

@description('Subnet to attach this module\'s own storage account\'s private endpoints to')
param privateEndpointSubnetId string
param blobPrivateDnsZoneId string
param queuePrivateDnsZoneId string
param tablePrivateDnsZoneId string

@description('Delegated subnet (Microsoft.Web/serverfarms) for regional VNet integration — required to reach SQL/the shared Storage account now that they\'re private-endpoint-only')
param vnetIntegrationSubnetId string

var deploymentContainerName = 'deploymentpackage'

// Function App default hostnames are globally unique across all of Azure, not just this
// subscription — same fix as appService.bicep's uniqueSuffix.
var uniqueSuffix = substring(uniqueString(subscription().id, resourceGroup().id), 0, 4)

// Flex Consumption needs a storage account for its deployment package plus runtime
// bookkeeping (locks, trigger state) — deliberately a separate account from the shared
// stlaunchpad<env> business-data account (storage.bicep), not the same one, so this
// account never holds candidate/resume data. Unlike classic Y1 Consumption (which
// requires an unrestricted storage account and can't be used here at all — Linux
// dynamic workers aren't available on this subscription), Flex Consumption fully
// supports a private-endpoint-only storage account, so this gets the same lockdown as
// every other storage/SQL/Key Vault resource in this file.
resource functionsStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'stlaunchpadfunc${env}'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    // Public access is disabled in every environment, not just Prod — Microsoft's
    // internal CloudGov policy set denies public network access tenant-wide.
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

// Same "one groupId per private endpoint" constraint as storage.bicep.
var functionsStorageSubResources = [
  { suffix: 'blob', groupId: 'blob', dnsZoneId: blobPrivateDnsZoneId }
  { suffix: 'queue', groupId: 'queue', dnsZoneId: queuePrivateDnsZoneId }
  { suffix: 'table', groupId: 'table', dnsZoneId: tablePrivateDnsZoneId }
]

resource functionsStoragePrivateEndpoints 'Microsoft.Network/privateEndpoints@2023-11-01' = [for sub in functionsStorageSubResources: {
  name: 'pe-st-launchpadfunc-${env}-${sub.suffix}'
  location: location
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      {
        name: 'pe-st-launchpadfunc-${env}-${sub.suffix}-connection'
        properties: {
          privateLinkServiceId: functionsStorageAccount.id
          groupIds: [ sub.groupId ]
        }
      }
    ]
  }
}]

resource functionsStoragePrivateDnsZoneGroups 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = [for (sub, i) in functionsStorageSubResources: {
  parent: functionsStoragePrivateEndpoints[i]
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      { name: 'privatelink-${sub.suffix}-core-windows-net', properties: { privateDnsZoneId: sub.dnsZoneId } }
    ]
  }
}]

resource functionsStorageBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: functionsStorageAccount
  name: 'default'
}

// Flex Consumption's deployment package lives here — created up front so the site
// resource's functionAppConfig.deployment.storage reference resolves on first deploy
// (the classic az functionapp create CLI path fails with ContainerNotFound if this
// doesn't already exist; an explicit dependsOn on the site resource avoids that here).
resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: functionsStorageBlobService
  name: deploymentContainerName
  properties: {
    publicAccess: 'None'
  }
}

// Flex Consumption (FC1) replaces the old EP1 Elastic Premium plan — EP1 billed for
// reserved capacity 24/7 regardless of load; FC1 is pay-per-execution with an
// always-ready-instance count of zero, so an idle Dev environment costs ~$0. Classic Y1
// Consumption was evaluated first and rejected: it requires an unrestricted storage
// account (incompatible with this repo's private-endpoint-everywhere posture) *and*
// Linux dynamic workers aren't available on this subscription at all.
resource functionsPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-launchpad-fn-${env}'
  location: location
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
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
    // All outbound traffic routes through the VNet, not just RFC1918 ranges — SQL/the
    // shared Storage account are reachable only via their private endpoints now.
    vnetRouteAllEnabled: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${functionsStorageAccount.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            // Identity-based, not a connection string — matches this repo's "no
            // connection strings or API keys anywhere" rule. Requires Storage Blob Data
            // Owner on functionsStorageAccount for functionApp's identity (granted in
            // main.bicep) — Flex Consumption's own deployment/runtime operations need
            // more than Blob Data Contributor gives.
            type: 'SystemAssignedIdentity'
          }
        }
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '9.0'
      }
      scaleAndConcurrency: {
        instanceMemoryMB: 2048
        maximumInstanceCount: 100
      }
    }
    siteConfig: {
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: functionsStorageAccount.name }
        { name: 'ConnectionStrings__Sql', value: 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
        { name: 'ServiceBusConnection__fullyQualifiedNamespace', value: '${serviceBusNamespace}.servicebus.windows.net' }
      ]
    }
  }
  dependsOn: [
    deploymentContainer
  ]
}

output functionAppName string = functionApp.name
output functionAppPrincipalId string = functionApp.identity.principalId
output functionsStorageAccountName string = functionsStorageAccount.name
