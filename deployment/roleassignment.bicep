@description('The name of the resource to assign role')
param resourceName string

@description('The role to assign to the resource')
param roleName string

@description('The service principal to add the role assignment')
param principalId string

@description('true if the resource is storage else false')
param storage bool = false

@description('The role to assign  to the resource group of the resource.')
param resourceGroupRoleName string = 'Reader'

@description('Assigna a role to the resoucr group.')
param assignGroupRole bool = false

var roles = {
  Contributor: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c'
  Reader: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7'
  'Storage Blob Data Contributor': '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  'Media Services Media Operator': '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/e4395492-1534-4db2-bedf-88c14621589c'
  'Key Vault Secrets Officer':'/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/b86a8fe4-44ce-4948-aee5-eccb2c155cd7'  
}

resource mediaAccount 'Microsoft.Media/mediaservices@2023-01-01' existing = if(!storage) {
  name: resourceName
}

resource resourceGroupRoleNameAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (assignGroupRole) {
  name: guid(resourceGroup().id, principalId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: roles[resourceGroupRoleName]
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource mediaRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!storage) {
  name: guid(mediaAccount.id, principalId)
  scope: mediaAccount
  properties: {
    roleDefinitionId: roles[roleName]
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = if(storage) {
  name: resourceName
}

resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if(storage) {
  name: guid(storageAccount.id, principalId)
  scope: storageAccount
  properties: {
    roleDefinitionId: roles[roleName]
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

