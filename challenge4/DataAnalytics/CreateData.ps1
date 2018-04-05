$rg = "6342C4"
$webApp = (Get-AzureRmWebApp -ResourceGroupName $rg)[0]
$servername = $webApp.SiteName

#SQL Settings
$adminName = "student"
$adminPassword = "Pa55w.rd1234"
$databaseName = "demo"

#Login if necessary
Login-AzureRmAccount


#Add Files
$data = "{'id':'us01','name':'butter'}","{'id':'us02','name':'eggs'}","{'id':'us03','name':'flour'}"
$container = "unstructured"

$sa = (Get-AzureRmStorageAccount -ResourceGroupName $rg)[0]
$key = (Get-AzureRmStorageAccountKey -ResourceGroupName $rg -Name $sa.StorageAccountName)[0].Value
$saContext = New-AzureStorageContext -StorageAccountName $sa.StorageAccountName -StorageAccountKey $key 
New-AzureStorageContainer -Name $container -Permission Off -Context $saContext
$fileNo = 0
foreach ($content in $data) {
    $blobName = "demo$($fileNo).txt"
    $content > $blobName
    Set-AzureStorageBlobContent -File $blobName -Container $container -Blob $blobName -BlobType Block -Context $saContext
    Remove-Item -Path $blobName
    $fileNo++

}

#Add SQL Data


$MyIP = (Invoke-WebRequest ifconfig.me/ip).Content.Trim()
New-AzureRmSqlServerFirewallRule -ServerName $servername -ResourceGroupName $rg -FirewallRuleName "local" -StartIpAddress $myIP -EndIpAddress $myIP

$connectionString = "Data Source=$($servername).database.windows.net;Initial Catalog=$($databaseName);User ID=$($adminName);Password=$($adminPassword);Connection Timeout=90"
$connection = New-Object -TypeName System.Data.SqlClient.SqlConnection($connectionString)
$query = "
CREATE TABLE dbo.products(id INT NOT NULL, name varchar(30));
INSERT INTO dbo.products(id,name) VALUES (1,'Bullet train');
INSERT INTO dbo.products(id,name) VALUES (2,'Falcon Heavy');
INSERT INTO dbo.products(id,name) VALUES (3,'eggs');

 "
$command = New-Object -TypeName System.Data.SqlClient.SqlCommand($query, $connection)
$connection.Open()
$command.ExecuteNonQuery()
$connection.Close()

#Add CosmosDB data
$key = (Invoke-AzureRmResourceAction -Action listKeys `
    -ResourceType "Microsoft.DocumentDb/databaseAccounts" `
    -ApiVersion "2015-04-08" `
    -ResourceGroupName $rg `
    -Name $servername `
    -Force)[0].PrimaryMasterKey
"Server Name: $($servername)"
"CosmosDB Key: $($key)"

