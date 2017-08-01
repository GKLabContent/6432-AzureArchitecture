$stName = "<storage account name>" 
$locName = "EastUS" 
$rgName = "module2" 
$vnetName = "complex-VNET"
$vmName = "complex-VM"
New-AzureRmResourceGroup -Name $rgName -Location $locName 
#$storageAcc = New-AzureRmStorageAccount -ResourceGroupName $rgName -Name $stName -Type "Standard_GRS" -Location $locName 
$public = New-AzureRmVirtualNetworkSubnetConfig -Name public -AddressPrefix 10.0.0.0/24 
$vnet1 = New-AzureRmVirtualNetwork -Name $vnetName -ResourceGroupName $rgName -Location $locName -AddressPrefix 10.0.0.0/16 -Subnet $public 
Add-AzureRmVirtualNetworkSubnetConfig -Name private -VirtualNetwork $vnet1 -AddressPrefix 10.0.1.0/24
$vnet1 | Set-AzureRmVirtualNetwork
$vnet1 = Get-AzureRmVirtualNetwork -Name $vnetName -ResourceGroupName $rgName
$pip = New-AzureRmPublicIpAddress -Name TestPIP -ResourceGroupName $rgName -Location $locName -AllocationMethod Dynamic 
$nic1 = New-AzureRmNetworkInterface -Name TestNIC1 -ResourceGroupName $rgName -Location $locName -SubnetId $vnet1.Subnets[0].Id -PublicIpAddressId $pip.Id 
$nic2 = New-AzureRmNetworkInterface -Name TestNIC2 -ResourceGroupName $rgName -Location $locName -SubnetId $vnet1.Subnets[1].Id 
$cred = Get-Credential -Message "Type the name and password of the local administrator account." 
$vm = New-AzureRmVMConfig -VMName WindowsVM -VMSize "Standard_A1" 
$vm = Set-AzureRmVMOperatingSystem -VM $vm -Windows -ComputerName MyWindowsVM -Credential $cred -ProvisionVMAgent -EnableAutoUpdate 
$vm = Set-AzureRmVMSourceImage -VM $vm -PublisherName MicrosoftWindowsServer -Offer WindowsServer -Skus 2012-R2-Datacenter -Version "latest" 
$vm = Add-AzureRmVMNetworkInterface -VM $vm -Id $nic1.Id -Primary 
$vm = Add-AzureRmVMNetworkInterface -VM $vm -Id $nic2.Id 
#$osDiskUri = $storageAcc.PrimaryEndpoints.Blob.ToString() + "vhds/WindowsVMosDisk.vhd" 
#$vm = Set-AzureRmVMOSDisk -VM $vm -Name "windowsvmosdisk" -VhdUri $osDiskUri -CreateOption fromImage 
New-AzureRmVM -ResourceGroupName $rgName -Location $locName -VM $vm  