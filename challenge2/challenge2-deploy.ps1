#Set these variables
$batchAccountName = "tswbatch"
$batchStorageAccountName = "tswbatch"
$serviceBusNamespace = "tswbatch"
$location = "eastus"
$resourceGroup = "challenge2"
$functionName = "tswbatchfunction"



#Leave these variables alone
$batchDataAccountName = "$($batchStorageAccountName)data"
$videoContainerName = "video"
$resultsContainerName = "results"
$serviceBusAuthName = "default"
$queueName = "videoprocess"
$poolName = "demo"
$poolOffer = "WindowsServer"
$poolPublisher = "MicrosoftWindowsServer"
$poolSku = "2016-Datacenter"
$poolVersion = "latest"
$batchAgentSku="batch.node.windows amd64"
$batchAppName = "ProcessVideo"
$batchAppFile = "$($PSScriptRoot)\videoProcessor.zip"
$environmentSettingsFile = "$($PSScriptRoot)\environment.json"
$batchAppVersion = "1.0"
$batchJobName = "Demo"
$ErrorActionPreference = "Stop"


#This really doesn't need to be a function, but at some point I am going to modularize this.
function getSubscription() {
    $subs = get-azurermsubscription
    $done = $false
    while ($done -eq $false) {
        $i = 1
        $compare = "["
        Write-Output "Select a subscription"
        $subs | foreach {
            Write-Host "$i) $($_.Name)" -ForegroundColor Cyan
            $compare = "$compare$i"
            $i++
        }
        $compare = "$compare]"
        $sn = Read-Host
        $done = $sn -match $compare
    }
    Write-Host "You have selected $($subs[$sn-1].Name). Do you want to proceed? (y/N)"
    $proceed = Read-Host 
    if (!($proceed -match "[Yy]")) {
        return $false
    }
    Select-AzureRmSubscription -SubscriptionId $subs[$sn-1].Id
    return $true
}

#Process Begins here

#Login and get or create the resource group.  Note, right now nothing else is protected - if it is already there you will get errors.
Login-AzureRmAccount
getSubscription
try {
    $rg = get-azurermresourcegroup -Name $resourceGroup
} Catch {
    $rg = New-AzureRmResourceGroup -Name $resourceGroup -Location $location
}

#set up storage
$batchStorageAccount = New-AzureRmStorageAccount -ResourceGroupName $resourceGroup -Location $location -Kind Storage -SkuName Standard_LRS -Name $batchStorageAccountName
$batchDataAccount = New-AzureRmStorageAccount -ResourceGroupName $resourceGroup -Location $location -Kind Storage -SkuName Standard_LRS -Name $batchDataAccountName

$batchStorageKey = (Get-AzureRmStorageAccountKey -ResourceGroupName $resourceGroup -Name $batchStorageAccountName)[0].Value
$batchDataKey = (Get-AzureRmStorageAccountKey -ResourceGroupName $resourceGroup -Name $batchDataAccountName)[0].Value

$batchDataContext = New-AzureStorageContext -StorageAccountName $batchDataAccountName -StorageAccountKey $batchDataKey
$startTime = Get-Date
$EndTime = $startTime.AddDays(2)
$batchDataSAS = New-AzureStorageAccountSASToken -Service Blob,Queue -ResourceType Container,Object -Permission rwdlacup -Context $batchDataContext -StartTime $startTime -ExpiryTime $EndTime


$videoContainer = New-AzureStorageContainer -Name $videoContainerName -Permission Off -Context $batchDataContext
$videoContainer = New-AzureStorageContainer -Name $resultsContainerName -Permission Off -Context $batchDataContext

#Set up Service Bus
$serviceBus = New-AzureRmServiceBusNamespace -ResourceGroupName $resourceGroup -Location $location -NamespaceName $serviceBusNamespace -SkuName Basic
$serviceBusAuth = New-AzureRmServiceBusNamespaceAuthorizationRule -ResourceGroup $resourceGroup -NamespaceName $serviceBusNamespace -AuthorizationRuleName $serviceBusAuthName -Rights @("Listen","Send","Manage")
$ServiceBusKey = Get-AzureRmServiceBusNamespaceKey -ResourceGroup $resourceGroup -NamespaceName $serviceBusNamespace -AuthorizationRuleName $serviceBusAuthName 
$serviceBusQueue = New-AzureRmServiceBusQueue -ResourceGroup $resourceGroup -NamespaceName $serviceBusNamespace -QueueName $queueName -EnablePartitioning $false 

#set up batch account
$batchAccount = New-AzureRmBatchAccount -AccountName $batchAccountName -Location $location -ResourceGroupName $resourceGroup -AutoStorageAccountId $batchStorageAccount.Id
$batchcontext = Get-AzureRmBatchAccountKeys -AccountName $batchAccountName -ResourceGroupName $resourceGroup

#Set up batch pool
$imageref = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSImageReference" -ArgumentList @($poolOffer, $poolPublisher, $poolSku, $poolVersion)
$configuration = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSVirtualMachineConfiguration" -ArgumentList @($imageref,$batchAgentSku,  $null)
New-AzureBatchPool -id $poolName -VirtualMachineSize "Standard_A1" -TargetDedicated 1 -VirtualMachineConfiguration $configuration -BatchContext $batchcontext 

#Set up Batch application
New-AzureRmBatchApplication -AccountName $batchAccountName -ResourceGroupName $resourceGroup -ApplicationId $batchAppName -AllowUpdates $true -DisplayName $batchAppName
New-AzureRmBatchApplicationPackage -AccountName $batchAccountName -ResourceGroupName $resourceGroup -ApplicationId $batchAppName -ApplicationVersion $batchAppVersion -Format zip -FilePath $batchAppFile 
Set-AzureRmBatchApplication -AccountName $batchAccountName -ResourceGroupName $resourceGroup -ApplicationId $batchAppName -DefaultVersion $batchAppVersion

#Set up Environment variables
$env = @{
    "BATCH_ACCOUNT_NAME" = $batchAccountName; 
    "BATCH_KEY"=$batchcontext.PrimaryAccountKey; 
    "BATCH_STORAGE_ACCOUNT" = $batchStorageAccountName;
    "BATCH_STORAGE_KEY" = $batchStorageKey;
    "BATCH_URI" = $batchAccount.TaskTenantUrl;
    "DEVELOPMENT" = "false";
    "SB_CONNECT" = $ServiceBusKey.PrimaryConnectionString;
    "VIDEO_STORAGE_ACCOUNT" = $batchDataAccountName;
    "VIDEO_STORAGE_SAS" = $batchDataSAS

  }

$env | ConvertTo-Json | Out-File $environmentSettingsFile

#set up batch job
$PoolInformation = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSPoolInformation" 
$PoolInformation.PoolId = $poolName
New-AzureBatchJob -Id $batchJobName -BatchContext $batchcontext -CommonEnvironmentSettings $env -PoolInformation $PoolInformation

