if(![System.IO.File]::Exists("./.nuget/nuget.exe")){
    # file with path $path doesn't exist
}

Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "./.nuget/nuget.exe"

Remove-Item .\artifacts -Force -Recurse
dotnet pack .\src\Esprima\project.json -c Release -o .\artifacts
.\.nuget\nuget.exe push .\artifacts\*.nupkg -Source nuget.org -ApiKey $nugetApiKey
