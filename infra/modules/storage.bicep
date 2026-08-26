@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

@description('Subnet to attach the private endpoint\'s NIC to')
param privateEndpointSubnetId string

@description('Private DNS zone ids, one each for blob/queue/table (privatelink.{blob,queue,table}.core.windows.net) — the Functions host needs all three even with no storage-triggered functions, since AzureWebJobsStorage covers blob leases plus internal queue/table bookkeeping.')
param blobPrivateDnsZoneId string
param queuePrivateDnsZoneId string
param tablePrivateDnsZoneId string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'stlaunchpad${env}'
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

// Storage accounts only permit ONE groupId per private endpoint ("OnlyOneGroupIdPermitted"
// connecting to a first-party resource) — unlike Key Vault/SQL, blob/queue/table each need
// their own private endpoint rather than one endpoint listing all three groupIds.
var storageSubResources = [
  { suffix: 'blob', groupId: 'blob', dnsZoneId: blobPrivateDnsZoneId }
  { suffix: 'queue', groupId: 'queue', dnsZoneId: queuePrivateDnsZoneId }
  { suffix: 'table', groupId: 'table', dnsZoneId: tablePrivateDnsZoneId }
]

resource privateEndpoints 'Microsoft.Network/privateEndpoints@2023-11-01' = [for sub in storageSubResources: {
  name: 'pe-st-launchpad-${env}-${sub.suffix}'
  location: location
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      {
        name: 'pe-st-launchpad-${env}-${sub.suffix}-connection'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [ sub.groupId ]
        }
      }
    ]
  }
}]

resource privateDnsZoneGroups 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = [for (sub, i) in storageSubResources: {
  parent: privateEndpoints[i]
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      { name: 'privatelink-${sub.suffix}-core-windows-net', properties: { privateDnsZoneId: sub.dnsZoneId } }
    ]
  }
}]

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

// Resumes, deliverables, recordings — private container, served via short-lived
// user-delegation SAS. Hot for active cohorts, Cool lifecycle handles the rest.
resource artifactsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'artifacts'
  properties: {
    publicAccess: 'None'
  }
}

resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'move-to-cool-after-90-days'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [ 'blockBlob' ]
              prefixMatch: [ 'artifacts/' ]
            }
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 90
                }
              }
            }
          }
        }
      ]
    }
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
