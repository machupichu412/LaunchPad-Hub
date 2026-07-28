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

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-launchpad-${env}'
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
    publicNetworkAccess: 'Enabled'
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
