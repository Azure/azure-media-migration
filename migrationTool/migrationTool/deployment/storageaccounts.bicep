@description('The service principal to add the role assignment')
param principalId string

@description('Storage role name')
param storageRoleName string = 'Storage Blob Data Contributor'

@description('The storage accounts associated with the media account')
param storageAccounts array

module storageRoleAssignments './roleassignment.bicep' = [for storage in storageAccounts: {
  name: 'storageRoleAssignment-${split(storage, '/')[8]}'
  scope: resourceGroup(split(storage, '/')[2], split(storage, '/')[4])
  params: {
    resourceName: split(storage, '/')[8]
    roleName: storageRoleName
    principalId: principalId
    storage: true
  }
}]

