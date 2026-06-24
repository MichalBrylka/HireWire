# Publish the production-optimized artifact binaries locally
dotnet publish -c Release -o ./publish

# Compress the publish outputs into a zip deployment archive
Compress-Archive -Path ./publish/* -DestinationPath ./deploy.zip -Force

# Push the deployment package straight to Azure
az webapp deployment source config-zip `
    --resource-group $ResourceGroupName `
    --name $WebAppName `
    --src ./deploy.zip