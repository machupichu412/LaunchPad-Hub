@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

param publicNetworkAccess bool = true

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: 'sb-launchpad-${env}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    publicNetworkAccess: publicNetworkAccess ? 'Enabled' : 'Disabled'
  }
}

// Cohort-wide matching runs. Dead-lettering catches jobs the matching Function fails to process.
resource matchingJobsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'matching-jobs'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 5
  }
}

// Sponsor auto-flag evaluation, fired on review submit.
resource reviewSubmittedQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'review-submitted'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 5
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
    maxDeliveryCount: 5
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
    maxDeliveryCount: 5
  }
}

output namespaceName string = serviceBusNamespace.name
output namespaceId string = serviceBusNamespace.id
