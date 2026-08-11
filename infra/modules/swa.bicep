@description('Static Web App name.')
param name string
@description('SWA is only available in a handful of regions; overrides the RG location.')
@allowed([ 'westeurope', 'centralus', 'eastus2', 'westus2', 'eastasia' ])
param location string = 'westeurope'
param tags object = {}

@description('Resource ID of the bring-your-own Function App to link as the backend.')
param functionAppResourceId string
@description('Region the Function App lives in.')
param functionAppLocation string

@description('Entra client ID surfaced as an app setting for auth config reference.')
param entraClientId string = ''
@description('Google client ID surfaced as an app setting for auth config reference.')
param googleClientId string = ''
@description('Entra client secret consumed by staticwebapp.config.json (ENTRA_CLIENT_SECRET).')
@secure()
param entraClientSecret string = ''
@description('Google client secret consumed by staticwebapp.config.json (GOOGLE_CLIENT_SECRET).')
@secure()
param googleClientSecret string = ''

resource swa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    // Frontend deploys are handled by GitHub Actions, not repo-linked build here.
    allowConfigFileUpdates: true
  }
}

// Link the "bring your own" Function App as the SWA backend.
resource linkedBackend 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = {
  parent: swa
  name: 'api'
  properties: {
    backendResourceId: functionAppResourceId
    region: functionAppLocation
  }
}

// App settings hold the OAuth client IDs + secrets that staticwebapp.config.json
// references via clientIdSettingName / clientSecretSettingName. Empty values are
// tolerated on the first provision and filled in by scripts/setup-entra-auth.
resource swaSettings 'Microsoft.Web/staticSites/config@2023-12-01' = {
  parent: swa
  name: 'appsettings'
  properties: {
    ENTRA_CLIENT_ID: entraClientId
    ENTRA_CLIENT_SECRET: entraClientSecret
    GOOGLE_CLIENT_ID: googleClientId
    GOOGLE_CLIENT_SECRET: googleClientSecret
  }
}

output name string = swa.name
output defaultHostname string = swa.properties.defaultHostname
output id string = swa.id
