@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

@description('Entra object ID of the group/user administering the server (Entra-only auth — SQL auth is disabled)')
param aadAdminObjectId string

@description('Display name of the Entra admin principal')
param aadAdminLogin string

@description('Auto-pause after N minutes of inactivity; -1 disables auto-pause (use in Prod)')
param autoPauseDelayMinutes int = 60

@description('Zone-redundant Prod gets more vCores; Dev/Test stay small')
param minCapacity string = '0.5'

param maxVCore int = 2

param zoneRedundant bool = false

@description('Subnet to attach the private endpoint\'s NIC to')
param privateEndpointSubnetId string

@description('Private DNS zone (privatelink.database.windows.net) to register the private endpoint in')
param privateDnsZoneId string

// SQL server names are globally unique across all of Azure, not just this subscription —
// 'sql-launchpad-dev' (no suffix) collided with an unrelated server outside this tenant on
// first deploy. Same fix as keyVault.bicep/serviceBus.bicep's uniqueSuffix.
var uniqueSuffix = substring(uniqueString(subscription().id, resourceGroup().id), 0, 4)

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-launchpad-${env}-${uniqueSuffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // Entra-only authentication — SQL authentication is disabled entirely, see CLAUDE.md.
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Group'
      login: aadAdminLogin
      sid: aadAdminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    // Public access is disabled in every environment, not just Prod — Microsoft's
    // internal CloudGov policy set denies public network access tenant-wide.
    publicNetworkAccess: 'Disabled'
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-sql-launchpad-${env}'
  location: location
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      {
        name: 'pe-sql-launchpad-${env}-connection'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: [ 'sqlServer' ]
        }
      }
    ]
  }
}

resource privateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      { name: 'privatelink-database-windows-net', properties: { privateDnsZoneId: privateDnsZoneId } }
    ]
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'launchpad'
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: maxVCore
  }
  properties: {
    minCapacity: json(minCapacity)
    autoPauseDelay: autoPauseDelayMinutes
    zoneRedundant: zoneRedundant
    requestedBackupStorageRedundancy: zoneRedundant ? 'Zone' : 'Local'
  }
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
