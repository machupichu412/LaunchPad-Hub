@description('Name of an existing Key Vault to grant access to')
param keyVaultName string

@description('Role assignments to create: [{ principalId: string, roleDefinitionId: string }]')
param roleAssignments array

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource assignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for ra in roleAssignments: {
  name: guid(keyVault.id, ra.principalId, ra.roleDefinitionId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', ra.roleDefinitionId)
    principalId: ra.principalId
    principalType: 'ServicePrincipal'
  }
}]
