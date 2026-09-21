$ErrorActionPreference = 'Stop'
$publishDirectory = Join-Path $PSScriptRoot 'x64/Publish'
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
$projects = @(
    'CodeWalker',
    'CodeWalker.ErrorReport',
    'CodeWalker.Gen9Converter',
    'CodeWalker.ModManager',
    'CodeWalker.ParticleEditorWpf',
    'CodeWalker.Peds',
    'CodeWalker.RPFExplorer',
    'CodeWalker.Vehicles'
)

foreach ($project in $projects) {
    $projectPath = Join-Path $PSScriptRoot "$project/$project.csproj"
    $appPublishDirectory = Join-Path $PSScriptRoot "$project/bin/Release/bundle"
    dotnet publish $projectPath `
        --configuration Release --runtime win-x64 --self-contained false `
        -p:CodeWalkerPublish=true --output $appPublishDirectory
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
    Copy-Item -Path "$appPublishDirectory/*" -Destination $publishDirectory -Recurse -Force

    # Remove a loose config left by an older package, only after replacing its executable.
    $projectXml = [xml](Get-Content -LiteralPath $projectPath -Raw)
    $assemblyName = $projectXml.SelectSingleNode('/Project/PropertyGroup/AssemblyName')
    $appName = if ($assemblyName) { $assemblyName.InnerText } else { $project }
    $looseConfig = Join-Path $publishDirectory "$appName.runtimeconfig.json"
    if (Test-Path -LiteralPath $looseConfig) { Remove-Item -LiteralPath $looseConfig }
}

# Remove former loose assets only after all their replacements have been published.
foreach ($asset in @(@{ Directory = 'icons'; Filter = '*.png' }, @{ Directory = 'Shaders'; Filter = '*.cso' })) {
    $assetDirectory = Join-Path $publishDirectory $asset.Directory
    if (!(Test-Path -LiteralPath $assetDirectory)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $asset.Directory) -Filter $asset.Filter -File) {
        $looseFile = Join-Path $assetDirectory $file.Name
        if (Test-Path -LiteralPath $looseFile) { Remove-Item -LiteralPath $looseFile }
    }
    if (!(Get-ChildItem -LiteralPath $assetDirectory -Force)) { Remove-Item -LiteralPath $assetDirectory }
}

Write-Host "Packaged applications: $publishDirectory"
