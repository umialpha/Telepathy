Set-Location -Path C:\Users\jingjli\github\Telepathy\future\sparrow-poc
Start-Process cmd.exe -ArgumentList "/K dotnet run -p BrokerServer"
#Start-Process cmd.exe -ArgumentList "/K dotnet run -p WorkerServer"

Start-Sleep -s 5

$max_iterations = 1;

for ($i=1; $i -le $max_iterations; $i++)
{
	
	
	$proc = Start-Process cmd.exe -ArgumentList "/K dotnet run -p BrokerClient" -PassThru
	$proc.WaitForExit()
  
}