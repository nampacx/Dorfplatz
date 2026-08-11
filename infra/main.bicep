// =============================================================================
// BulletinBoard - main infrastructure
// Scope: resource group. Deploy with:
//   az deployment group create -g <rg> -f infra/main.bicep -p @infra/main.parameters.json
// =============================================================================
targetScope = 'resourceGroup'

@description('Short environment name, e.g. dev or prod. Used in resource names.')
@allowed([ 'dev', 'prod' ])
param environmentName string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Region for PostgreSQL Flexible Server. Separated because it is not available in every region on every subscription.')
param postgresLocation string = 'swedencentral'

@description('A short, globally-unique-ish token to suffix resource names.')
param nameToken string = uniqueString(resourceGroup().id, environmentName)

@description('PostgreSQL administrator login.')
param postgresAdminLogin string = 'bbadmin'

@description('PostgreSQL administrator password.')
@secure()
param postgresAdminPassword string

@description('Object ID of the user/service principal running the deployment (granted Key Vault admin for setup).')
param deployerObjectId string

@description('Microsoft Entra client ID for SWA auth.')
param entraClientId string = ''
@description('Microsoft Entra client secret for SWA auth.')
@secure()
param entraClientSecret string = ''
@description('Google OAuth client ID for SWA auth.')
param googleClientId string = ''
@description('Google OAuth client secret for SWA auth.')
@secure()
param googleClientSecret string = ''

@description('Email of the first user to auto-grant the admin role on initial sign-in.')
param seedAdminEmail string = ''

var prefix = 'bb-${environmentName}'
var tags = {
  application: 'BulletinBoard'
  environment: environmentName
}

// --- Observability ---------------------------------------------------------
module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    name: '${prefix}-ai-${nameToken}'
    logAnalyticsName: '${prefix}-law-${nameToken}'
    location: location
    tags: tags
  }
}

// --- Identity --------------------------------------------------------------
module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    name: '${prefix}-api-mi-${nameToken}'
    location: location
    tags: tags
  }
}

// --- Key Vault -------------------------------------------------------------
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    // KV names: 3-24 chars, alphanumeric + dashes
    name: take('bbkv${environmentName}${nameToken}', 24)
    location: location
    tags: tags
    apiPrincipalId: identity.outputs.principalId
    deployerObjectId: deployerObjectId
    entraClientSecret: entraClientSecret
    googleClientSecret: googleClientSecret
  }
}

// --- Storage (listing images + Functions runtime storage) ------------------
module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: take('bbst${environmentName}${nameToken}', 24)
    location: location
    tags: tags
    apiPrincipalId: identity.outputs.principalId
  }
}

// --- PostgreSQL ------------------------------------------------------------
module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    name: '${prefix}-pg-${nameToken}'
    location: postgresLocation
    tags: tags
    administratorLogin: postgresAdminLogin
    administratorPassword: postgresAdminPassword
    databaseName: 'bulletinboard'
    keyVaultName: keyVault.outputs.name
  }
}

// --- SignalR (serverless) --------------------------------------------------
module signalr 'modules/signalr.bicep' = {
  name: 'signalr'
  params: {
    name: '${prefix}-signalr-${nameToken}'
    location: location
    tags: tags
    apiPrincipalId: identity.outputs.principalId
    keyVaultName: keyVault.outputs.name
  }
}

// --- Function App (bring your own) -----------------------------------------
module functionApp 'modules/functionApp.bicep' = {
  name: 'functionApp'
  params: {
    name: '${prefix}-func-${nameToken}'
    planName: '${prefix}-plan-${nameToken}'
    location: location
    tags: tags
    storageAccountName: storage.outputs.name
    userAssignedIdentityId: identity.outputs.id
    userAssignedClientId: identity.outputs.clientId
    appInsightsConnectionString: appInsights.outputs.connectionString
    imagesBlobEndpoint: storage.outputs.blobEndpoint
    imagesContainerName: storage.outputs.imagesContainerName
    postgresConnectionSecretUri: postgres.outputs.connectionStringSecretUri
    signalrConnectionSecretUri: signalr.outputs.connectionStringSecretUri
    seedAdminEmail: seedAdminEmail
  }
}

// --- Static Web App + linked backend ---------------------------------------
module swa 'modules/swa.bicep' = {
  name: 'swa'
  params: {
    name: '${prefix}-swa-${nameToken}'
    location: location
    tags: tags
    functionAppResourceId: functionApp.outputs.id
    functionAppLocation: location
    entraClientId: entraClientId
    googleClientId: googleClientId
    entraClientSecret: entraClientSecret
    googleClientSecret: googleClientSecret
  }
}

output staticWebAppName string = swa.outputs.name
output staticWebAppHostname string = swa.outputs.defaultHostname
output functionAppName string = functionApp.outputs.name
output keyVaultName string = keyVault.outputs.name
output postgresFqdn string = postgres.outputs.fqdn
