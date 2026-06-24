# 1. Login to Azure (if not already logged in)
az login

# 2. Variable Configuration
$ResourceGroupName = "rg-codeshare-prod"
$Location          = "westeurope"
$AppServicePlan    = "asp-codeshare"
$WebAppName        = "hire-wire-" + (Get-Random) # Generates a unique global URL suffix
$Sku               = "B1" # Basic tier supporting persistent custom WebSockets settings

Write-Host "Creating Resource Group: $ResourceGroupName..." -ForegroundColor Cyan
az group create --name $ResourceGroupName --location $Location

Write-Host "Creating Linux App Service Plan ($Sku)..." -ForegroundColor Cyan
az appservice plan create `
    --name $AppServicePlan `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku $Sku `
    --is-linux

Write-Host "Creating Web App: $WebAppName..." -ForegroundColor Cyan
az webapp create --name $WebAppName --resource-group $ResourceGroupName --plan $AppServicePlan --% --runtime "DOTNET|10.0"

Write-Host "Enabling WebSockets for SignalR context..." -ForegroundColor Cyan
az webapp config set `
    --name $WebAppName `
    --resource-group $ResourceGroupName `
    --web-sockets-enabled true

Write-Host "Deployment target successfully setup!" -ForegroundColor Green
Write-Host "Your app configuration endpoint: https://$WebAppName.azurewebsites.net" -ForegroundColor Yellow