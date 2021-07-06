$subId = $args[0]
$rg = $args[1]
$csesName = $args[2]

Write-Host "VIP Swap Params Sub:[$subId] ResourceGroup:[$rg] CSESName:[$csesName]"

# We sleep for ten seconds because there's a known race condition in the MSI sidecar.
# https://ev2docs.azure.net/features/extensibility/shell/troubleshooting.html#running-into-connect-azaccount--connection-refused-error-or-fail-to-connect-to-azure-error
Write-Host "Sleeping for 10 seconds..."
Start-Sleep -Seconds 10

Write-Host "Installing CloudService PS Module..."
Set-PSRepository -Name "PSGallery" -InstallationPolicy Trusted
Install-Module -Name Az.CloudService -Confirm:$false -Force -AcceptLicense

Write-Host "Authenticating and selecting subscription..."
$null = Connect-AzAccount -Identity
$null = Set-AzContext -Subscription $subId

Write-Host "Performing VIP Swap"
Switch-AzCloudService -SubscriptionId $subId -ResourceGroupName $rg -CloudServiceName $csesName -Confirm:$false
