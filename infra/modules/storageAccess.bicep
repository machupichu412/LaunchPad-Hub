@description('Name of an existing Storage Account to grant access to')
param storageAccountName string

@description('Role assignments to create: [{ principalId: string, roleDefinitionId: string }]')
param roleAssignments array

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource assignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for ra in roleAssignments: {
  name: guid(storageAccount.id, ra.principalId, ra.roleDefinitionId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', ra.roleDefinitionId)
    principalId: ra.principalId
    principalType: 'ServicePrincipal'
  }
}]
