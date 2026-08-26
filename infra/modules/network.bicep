@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region')
param location string

// Microsoft's internal CloudGov policy set denies public network access on Key Vault
// (and, per the same pattern, Storage/SQL) regardless of environment — so this VNet +
// private endpoint topology, which launchpad-build-guide.md §9.1 originally scoped to
// Prod only, is required for every environment deployed into this tenant, Dev included.
var vnetAddressPrefix = '10.20.0.0/16'
var appSubnetPrefix = '10.20.1.0/24'
var funcSubnetPrefix = '10.20.2.0/24'
var peSubnetPrefix = '10.20.3.0/24'

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: 'vnet-launchpad-${env}'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [ vnetAddressPrefix ]
    }
    subnets: [
      {
        name: 'snet-app'
        properties: {
          addressPrefix: appSubnetPrefix
          delegations: [
            { name: 'appServiceDelegation', properties: { serviceName: 'Microsoft.Web/serverfarms' } }
          ]
        }
      }
      {
        name: 'snet-func'
        properties: {
          addressPrefix: funcSubnetPrefix
          delegations: [
            { name: 'functionsDelegation', properties: { serviceName: 'Microsoft.Web/serverfarms' } }
          ]
        }
      }
      {
        name: 'snet-pe'
        properties: {
          addressPrefix: peSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource appSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: vnet
  name: 'snet-app'
}
resource funcSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: vnet
  name: 'snet-func'
}
resource peSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: vnet
  name: 'snet-pe'
}

// One private DNS zone per privately-linked service, each linked to this VNet so
// resources on snet-app/snet-func resolve the private-endpoint IP instead of the
// public one. servicebus's zone is created now (cheap, no ongoing cost) even though
// no private endpoint uses it yet — Service Bus private endpoints require the Premium
// SKU, a real cost jump from Standard deferred until it's actually needed.
var privateDnsZoneNames = [
  'privatelink.vaultcore.azure.net'
  'privatelink.database.windows.net'
  'privatelink.blob.core.windows.net'
  'privatelink.queue.core.windows.net'
  'privatelink.table.core.windows.net'
  'privatelink.servicebus.windows.net'
]

resource privateDnsZones 'Microsoft.Network/privateDnsZones@2024-06-01' = [for zoneName in privateDnsZoneNames: {
  name: zoneName
  location: 'global'
}]

resource privateDnsZoneLinks 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = [for (zoneName, i) in privateDnsZoneNames: {
  parent: privateDnsZones[i]
  name: 'link-vnet-launchpad-${env}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: { id: vnet.id }
  }
}]

output vnetId string = vnet.id
output appSubnetId string = appSubnet.id
output funcSubnetId string = funcSubnet.id
output peSubnetId string = peSubnet.id
output keyVaultDnsZoneId string = privateDnsZones[0].id
output sqlDnsZoneId string = privateDnsZones[1].id
output blobDnsZoneId string = privateDnsZones[2].id
output queueDnsZoneId string = privateDnsZones[3].id
output tableDnsZoneId string = privateDnsZones[4].id
output serviceBusDnsZoneId string = privateDnsZones[5].id
