Remove-Item .\artifacts -Force -Recurse
dotnet pack .\jint\project.json -c Release -o .\artifacts
.\.nuget\nuget.exe push .\artifacts\*.nupkg -Source nuget.org -ApiKey $nugetApiKey
