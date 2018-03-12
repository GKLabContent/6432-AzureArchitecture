Param(
    [string] [Parameter(Mandatory=$true)] $ResourceGroupLocation,
    [string] $ResourceGroupName = 'DataAnalytics',
)

$key = Get-AzureRmStorageAccountKey 
