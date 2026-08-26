@description('Name of an existing Service Bus namespace to grant access to')
param serviceBusNamespaceName string

@description('Role assignments to create: [{ principalId: string, roleDefinitionId: string }]')
param roleAssignments array

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: serviceBusNamespaceName
}

resource assignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for ra in roleAssignments: {
  name: guid(serviceBusNamespace.id, ra.principalId, ra.roleDefinitionId)
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', ra.roleDefinitionId)
    principalId: ra.principalId
    principalType: 'ServicePrincipal'
  }
}]
