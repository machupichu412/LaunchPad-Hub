@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

@description('Dev/Test run Basic; Prod runs P0v3 (see build guide §8.1)')
param skuName string = 'B1'

param sqlServerFqdn string
param sqlDatabaseName string
param keyVaultUri string
param appInsightsConnectionString string
param serviceBusNamespace string

@description('Deploy the Prod staging slot used for the swap-on-deploy pattern (§9.3)')
param deployStagingSlot bool = false

@description('Delegated subnet (Microsoft.Web/serverfarms) for regional VNet integration — required to reach SQL/Key Vault/Storage now that they\'re private-endpoint-only')
param vnetIntegrationSubnetId string

// App Service default hostnames are globally unique across all of Azure, not just this
// subscription — 'app-launchpad-dev' (no suffix) collided with an unrelated site outside
// this tenant on first deploy. Same fix as keyVault.bicep/serviceBus.bicep/sql.bicep's
// uniqueSuffix. CI/CD (deploy.yml) reads the concrete name back from this module's
// appServiceName output rather than hardcoding it, so this doesn't need to stay in sync
// with anything outside this file.
var uniqueSuffix = substring(uniqueString(subscription().id, resourceGroup().id), 0, 4)
var appName = 'app-launchpad-${env}-${uniqueSuffix}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-launchpad-${env}'
  location: location
  kind: 'linux'
  properties: {
    reserved: true
  }
  sku: {
    name: skuName
  }
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    virtualNetworkSubnetId: vnetIntegrationSubnetId
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      healthCheckPath: '/healthz'
      // All outbound traffic routes through the VNet, not just RFC1918 ranges — SQL/Key
      // Vault/Storage are reachable only via their private endpoints now.
      vnetRouteAllEnabled: true
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: env == 'prod' ? 'Production' : (env == 'test' ? 'Staging' : 'Development') }
        { name: 'ConnectionStrings__Sql', value: 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;' }
        { name: 'KeyVault__Uri', value: keyVaultUri }
        { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
        { name: 'ServiceBus__Namespace', value: '${serviceBusNamespace}.servicebus.windows.net' }
      ]
    }
  }
}

// Deploy -> warm up -> smoke test -> swap. Slot-sticky settings keep env vars from
// following the swap — mark connection/identity settings sticky at the slot level
// once secrets differ per slot; none do yet since everything is managed-identity based.
resource stagingSlot 'Microsoft.Web/sites/slots@2023-12-01' = if (deployStagingSlot) {
  parent: appService
  name: 'staging'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: appService.properties.siteConfig
  }
}

output appServiceName string = appService.name
output appServicePrincipalId string = appService.identity.principalId
output stagingSlotPrincipalId string = deployStagingSlot ? stagingSlot.identity.principalId : ''
output defaultHostName string = appService.properties.defaultHostName
