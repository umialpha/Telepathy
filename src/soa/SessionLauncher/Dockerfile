#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

#FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
#ARG source
#WORKDIR /app
#COPY ${source:-obj/Docker/publish} .
#ENTRYPOINT ["C:\\app\\HpcSession.exe"]

#docker container run --rm -it sessionlauncher:0.0.0.1 --AzureBatchServiceUrl "https://telepathy.japaneast.batch.azure.com" --AzureBatchAccountName telepathy --AzureBatchAccountKey "RaLd7C2fwOJin9v2VsCBmUeLZB9+fOmANLjXmWb5/z6/xk1K2h8uxeXy4/acoVLzlP+npHGZqOoY6oQj/OTahg==" --AzureBatchPoolName telepathy --AzureBatchBrokerStorageConnectionString "DefaultEndpointsProtocol=https;AccountName=telepathy;AccountKey=CIPIGmbH3eWA80UXsC/Fr18FOWINrSuyvAd6cQM0r3uzjU+FU5pEUEGpqGLFad4MgVuixlUGhFJggCvZv1Li3g==;EndpointSuffix=core.windows.net"
#ENTRYPOINT "HpcSession.exe" "-d" "--AzureBatchServiceUrl"="${BatchAccountServiceUrl}" "--AzureBatchAccountName"="${BatchAccountName}" "--AzureBatchAccountKey"="${BatchAccountKey}", "--AzureBatchPoolName"="${BatchPoolName}" "--AzureBatchBrokerStorageConnectionString"="${DesStorageConnectionString}"
FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
RUN powershell -Command Add-WindowsFeature NET-WCF-TCP-Activation45
RUN powershell -Command Add-WindowsFeature NET-WCF-HTTP-Activation45
EXPOSE 9090
EXPOSE 9092
WORKDIR /app
COPY bin/Release ./
ENV BatchAccountServiceUrl=https://telepathy.japaneast.batch.azure.com
ENV BatchAccountName=telepathy
ENV BatchAccountKey=RaLd7C2fwOJin9v2VsCBmUeLZB9+fOmANLjXmWb5/z6/xk1K2h8uxeXy4/acoVLzlP+npHGZqOoY6oQj/OTahg==
ENV BatchPoolName=telepathy
Env DesStorageConnectionString=DefaultEndpointsProtocol=https;AccountName=telepathy;AccountKey=CIPIGmbH3eWA80UXsC/Fr18FOWINrSuyvAd6cQM0r3uzjU+FU5pEUEGpqGLFad4MgVuixlUGhFJggCvZv1Li3g==;EndpointSuffix=core.windows.net
ENTRYPOINT HpcSession.exe -d --AzureBatchServiceUrl ${BatchAccountServiceUrl} --AzureBatchAccountName ${BatchAccountName} --AzureBatchAccountKey ${BatchAccountKey} --AzureBatchPoolName ${BatchPoolName} --AzureBatchBrokerStorageConnectionString ${DesStorageConnectionString}