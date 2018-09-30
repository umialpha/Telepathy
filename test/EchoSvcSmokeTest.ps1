$env:REPO_ROOT=(Split-Path $PSScriptRoot -Parent) 
$svchost = Start-Process "$env:REPO_ROOT\src\soa\CcpServiceHost\bin\Debug\CcpServiceHost.exe" -ArgumentList "-standalone" -PassThru
&"$env:REPO_ROOT\src\soa\EchoClient\bin\Debug\EchoClient.exe" -inproc -isnose -regpa "$env:REPO_ROOT\test\registration" -targetli 127.0.0.1
Stop-Process -Id $svchost.Id