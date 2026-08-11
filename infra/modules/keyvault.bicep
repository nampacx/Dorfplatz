@description('Key Vault name (3-24 chars, alphanumeric + dashes).')
param name string
param location string
param tags object = {}

@description('Principal ID of the API managed identity (granted Secrets User).')
param apiPrincipalId string
@description('Object ID of the deployer (granted Secrets Officer for setup).')
param deployerObjectId string

@secure()
param entraClientSecret string = ''
@secure()
param googleClientSecret string = ''

// Built-in role definition IDs
var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var secretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource apiSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, apiPrincipalId, secretsUserRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource deployerSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, deployerObjectId, secretsOfficerRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficerRoleId)
    principalId: deployerObjectId
  }
}

resource entraSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(entraClientSecret)) {
  parent: kv
  name: 'entra-client-secret'
  properties: { value: entraClientSecret }
}

resource googleSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(googleClientSecret)) {
  parent: kv
  name: 'google-client-secret'
  properties: { value: googleClientSecret }
}

output name string = kv.name
output uri string = kv.properties.vaultUri
output id string = kv.id
