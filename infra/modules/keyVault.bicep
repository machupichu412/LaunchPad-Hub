@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

param tenantId string

@description('Subnet to attach the private endpoint\'s NIC to')
param privateEndpointSubnetId string

@description('Private DNS zone (privatelink.vaultcore.azure.net) to register the private endpoint in')
param privateDnsZoneId string

// Key Vault names are globally unique across all of Azure, not just this subscription —
// 'kv-launchpad-dev' (no suffix) collided with an unrelated vault outside this tenant on
// first deploy. A short deterministic suffix avoids that without making the name random
// across redeploys.
var uniqueSuffix = substring(uniqueString(subscription().id, resourceGroup().id), 0, 4)

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-launchpad-${env}-${uniqueSuffix}'
  location: location
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    // RBAC via Key Vault Secrets User role assignment, not legacy access policies.
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    // Public access is disabled in every environment, not just Prod — Microsoft's
    // internal CloudGov policy set denies Key Vault public network access tenant-wide.
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-kv-launchpad-${env}'
  location: location
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      {
        name: 'pe-kv-launchpad-${env}-connection'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [ 'vault' ]
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
      { name: 'privatelink-vaultcore-azure-net', properties: { privateDnsZoneId: privateDnsZoneId } }
    ]
  }
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
