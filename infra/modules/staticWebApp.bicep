@description('Environment name, e.g. dev, test, prod')
param env string

@description('Azure region — Static Web Apps is only available in a subset of regions')
param location string

@description('Free tier for Dev; Standard is required for Prod (private endpoints, custom auth)')
param skuName string = 'Free'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-launchpad-${env}'
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    // Deployed via GitHub Actions (azure/static-web-apps-deploy), not a linked repo,
    // so PR preview environments are driven by the workflow rather than this resource.
    provider: 'Custom'
  }
}

output staticWebAppName string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname
