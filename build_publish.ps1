param([switch]$Publish)

dotnet restore
dotnet build -c Release

if ($Publish) {
  dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false
}
