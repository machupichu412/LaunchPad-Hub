@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

param privateNetworking bool = false
param privateEndpointSubnetId string
param privateDnsZoneId string

// Service Bus namespace names are globally unique across all of Azure, not just this
// subscription — 'sb-launchpad-dev' (no suffix) collided with an unrelated namespace
// outside this tenant on first deploy. Same fix as keyVault.bicep's uniqueSuffix.
var uniqueSuffix = substring(uniqueString(subscription().id, resourceGroup().id), 0, 4)

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: 'sb-launchpad-${env}-${uniqueSuffix}'
  location: location
  sku: {
    name: privateNetworking ? 'Premium' : 'Standard'
    tier: privateNetworking ? 'Premium' : 'Standard'
    capacity: privateNetworking ? 1 : 0
  }
  properties: {
    publicNetworkAccess: privateNetworking ? 'Disabled' : 'Enabled'
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
  }
}

// Cohort-wide matching runs. Dead-lettering catches jobs the matching Function fails to process.
resource matchingJobsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'matching-jobs'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 3
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P1D'
  }
}

// Sponsor auto-flag evaluation, fired on review submit.
resource reviewSubmittedQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'review-submitted'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 3
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P1D'
  }
}

// Outlook (Graph) email notifications — project submission and Ops approval decisions.
// Consumed by LaunchPad.Functions' NotificationFunction. The API and Functions identities
// already have namespace-wide send/receive RBAC (see main.bicep), so no per-queue role
// assignment is needed for this one.
resource notificationsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'notifications'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 3
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P1D'
  }
}

// Cohort/candidate/project folder provisioning, fired on Cohort/Candidate create and Project
// approve. Consumed by LaunchPad.Functions' SharePointProvisioningFunction. Same "no per-queue
// role assignment needed" reasoning as notificationsQueue above.
resource sharePointProvisioningQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'sharepoint-provisioning'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 3
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P1D'
  }
}

// Disabling the public endpoint without a private route stranded all production jobs.
// Private Link requires Premium; one fixed messaging unit bounds its baseline capacity.
resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = if (privateNetworking) {
  name: 'pe-sb-launchpad-${env}'
  location: location
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [{
      name: 'sb-launchpad-${env}'
      properties: {
        privateLinkServiceId: serviceBusNamespace.id
        groupIds: [ 'namespace' ]
      }
    }]
  }
}
resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = if (privateNetworking) {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [{ name: 'servicebus', properties: { privateDnsZoneId: privateDnsZoneId } }]
  }
}

output namespaceName string = serviceBusNamespace.name
output namespaceId string = serviceBusNamespace.id
