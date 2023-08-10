using './deployment.bicep'

// The media services account being migrated.
param mediaAccountName = 'provenanceuswc'
param mediaAccountRG = 'provenance'
// If media account is in a different subscrtipion than where the migration is running.
// param mediaAccountSubscription = ''

// The storage account where migrated data is written.
param storageAccountName = 'amsencodermsitest'
param storageAccountRG = 'amsmediacore'
// If the storage account is in a different subscription than where the migration is running.
// param storageAccountSubscription = ''

// setting to turn encryption on or off.
param encrypt = false

// The key vault to store encryption keys if encryption is turned on.
param keyvaultname = 'mpprovenance'
param keyvaultRG = 'provenance'
// param keyvaultSubscription = ''

//additional arguments.
param arguments = [
  '-t'
  '\${AssetName}'
]
