@description('SignalR Service name.')
param name string
param location string
param tags object = {}
@description('Principal ID of the API managed identity (granted SignalR App Server).')
param apiPrincipalId string
@description('Key Vault to store the connection string in.')
param keyVaultName string

// SignalR Service App Server built-in role
var signalrAppServerRoleId = '420fcaa2-552c-430f-98ca-3264be4806c7'

resource signalr 'Microsoft.SignalRService/signalR@2023-08-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    features: [
      {
        flag: 'ServiceMode'
        value: 'Serverless'
      }
    ]
    cors: {
      allowedOrigins: [ '*' ]
    }
  }
}

resource apiSignalrRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(signalr.id, apiPrincipalId, signalrAppServerRoleId)
  scope: signalr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', signalrAppServerRoleId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource connSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'signalr-connectionstring'
  properties: {
    value: signalr.listKeys().primaryConnectionString
  }
}

output name string = signalr.name
output hostName string = signalr.properties.hostName
output connectionStringSecretUri string = connSecret.properties.secretUri
