$projectjson = ".\jint\project.json"

if(![System.IO.Directory]::Exists("./.nuget")){
  New-Item ".nuget" -type directory
  Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "./.nuget/nuget.exe"
}

if([System.IO.Directory]::Exists("./artifacts")){
  Remove-Item .\artifacts -Force -Recurse
}

dotnet pack $projectjson -c Release -o .\artifacts
.\.nuget\nuget.exe push .\artifacts\*.nupkg -Source nuget.org -ApiKey $nugetApiKey
