using './deployment.bicep'

// The media serivces account being migrated.
param mediaAccountName = 'provenanceuswc'
param mediaAccountRG = 'provenance'

// The storage account where migrated data is written.
param storageAccountName = 'amsencodermsitest'
param storageAccountRG = 'amsmediacore'

// setting to turn encryption on or off.

param encrypt = false
// The key vault to store encryption keys if encryption is turned on.
param keyvaultname = 'mpprovenance'
param keyvaultRG = 'provenance'

//additional arguments.
param arguments = [
  '-t'
  '$web/deployment/\${AssetName}'
]
