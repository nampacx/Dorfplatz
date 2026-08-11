@description('Function App name.')
param name string
@description('App Service (Functions) plan name.')
param planName string
param location string
param tags object = {}

param storageAccountName string
param userAssignedIdentityId string
param userAssignedClientId string
param appInsightsConnectionString string
param imagesBlobEndpoint string
param imagesContainerName string
param postgresConnectionSecretUri string
param signalrConnectionSecretUri string
@description('Email of the first user to auto-grant the admin role on initial sign-in.')
param seedAdminEmail string = ''

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

var storageConnString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storage.listKeys().keys[0].value}'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: userAssignedIdentityId
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      cors: {
        allowedOrigins: [ 'https://portal.azure.com' ]
      }
      appSettings: [
        { name: 'AzureWebJobsStorage', value: storageConnString }
        { name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING', value: storageConnString }
        { name: 'WEBSITE_CONTENTSHARE', value: toLower(name) }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        // Managed identity used for Blob / SignalR / Key Vault access
        { name: 'AZURE_CLIENT_ID', value: userAssignedClientId }
        // App configuration consumed by the API
        { name: 'Postgres__ConnectionString', value: '@Microsoft.KeyVault(SecretUri=${postgresConnectionSecretUri})' }
        { name: 'AzureSignalRConnectionString', value: '@Microsoft.KeyVault(SecretUri=${signalrConnectionSecretUri})' }
        { name: 'Blob__ServiceUri', value: imagesBlobEndpoint }
        { name: 'Blob__ContainerName', value: imagesContainerName }
        { name: 'Blob__ManagedIdentityClientId', value: userAssignedClientId }
        { name: 'Seed__AdminEmail', value: seedAdminEmail }
      ]
    }
  }
}

output id string = functionApp.id
output name string = functionApp.name
output defaultHostname string = functionApp.properties.defaultHostName
