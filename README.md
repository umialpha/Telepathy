# Telepathy [![Build Status](https://dev.azure.com/bc-telepathy/telepathy/_apis/build/status/Azure.Telepathy?branchName=dev)](https://dev.azure.com/bc-telepathy/telepathy/_build/latest?definitionId=3&branchName=dev)

Home repo of Project Telepathy. Currently in **prototyping** stage.

## Developing Environment

- Visual Studio 2017 of latter
- Excel 2016 or latter if developing Excel service

## Engineering Practice

This project uses git flow. All new features will go into dev branch.

## Deployment in Azure

### Deploy in Azure Portal

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FBabysbreathJJ%2FTelepathyDeploy%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

### Deploy using Azure CLI

```shell
[ "$(az group exists -n ResourceGroupName)" = "true" ] && az group delete -n  ResourceGroupName -y
az group create --name ResourceGroupName --location "japaneast" --subscription subscriptionName
az group deployment create `
  --name DeployementName `
  --resource-group ResourceGroupName `
  --subscription subscriptionName `
  --template-file  "location of template file" `
  --parameters "location of parameters.json file"
```

### Deploy using Powershell

```shell
$ResourceGroupName = ""
$Location = ""
$TemplateFile = ""
$TemplateParameterFile = ""

Connect-AzAccount
if(Get-AzResourceGroup -Name $ResourceGroupName) {
    Remove-AzResourceGroup -Name $ResourceGroupName -Force
}

New-AzResourceGroup -Name $ResourceGroupName -Location $Location
New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile $TemplateFile -TemplateParameterFile $TemplateParameterFile
```

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
