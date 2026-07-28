@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

param tenantId string

@description('True in Prod once private endpoints are wired up (see §9.2). Dev/Test start public and Entra-authenticated.')
param publicNetworkAccess bool = true

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-launchpad-${env}'
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
    publicNetworkAccess: publicNetworkAccess ? 'Enabled' : 'Disabled'
  }
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
