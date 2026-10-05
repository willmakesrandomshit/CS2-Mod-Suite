param(
    [string]$WorkPath,
    [string]$LotPath,
    [string]$MarkingPath,
    [string]$CityMcpPath,
    [string[]]$Configurations = @('Debug', 'Release')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stage = Join-Path $repo 'artifacts/offline-portfolio'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$env:CSII_USERDATAPATH = $stage
if (-not $env:CSII_TOOLPATH) {
    $env:CSII_TOOLPATH = 'C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Content\Game\.ModdingToolchain'
}
$mods = Join-Path $stage 'Mods'
$town = $env:TOWN_ROAD_LANE_DLL
if (-not $town) {
    $cache = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) '..\LocalLow\Colossal Order\Cities Skylines II\.cache\Mods\pdx_mods'
    $town = Get-ChildItem -LiteralPath $cache -Recurse -File -Filter TownRoadLane.dll | Select-Object -First 1 -ExpandProperty FullName
}
$items = @()
foreach ($name in @('AccessStudio','CityPulse','ContourPlus','CrashLens','DiscordRPC','EventEngine','FastTrack','JunctionStudio','NetworkStudio','Parkify','RoadRules','SaveGuard','TrafficStressLab')) {
    $project = Get-ChildItem -LiteralPath (Join-Path $repo $name) -Filter '*.csproj' -File | Select-Object -First 1 -ExpandProperty FullName
    $items += @{ Name = $name; Project = $project }
}
$items += @{ Name = 'MagicMarking'; Project = Join-Path $repo 'MagicMarking/src/MarkingStudio/MarkingStudio.csproj' }
if ($WorkPath) {
    foreach ($name in @('DemandLens','LaneDoctor','ParkingPulse','ServiceDoctor','TrafficPulse','TransitPulse')) {
        $items += @{ Name = $name; Project = Join-Path $WorkPath "$name/$name.csproj" }
    }
}
if ($LotPath) { $items += @{ Name = 'LotStudio'; Project = Join-Path $LotPath 'LotStudio.csproj' } }
if ($MarkingPath) { $items += @{ Name = 'MarkingStudio-checkout'; Project = Join-Path $MarkingPath 'src/MarkingStudio/MarkingStudio.csproj' } }
if ($CityMcpPath) { $items += @{ Name = 'CityMCP'; Project = Join-Path $CityMcpPath 'CityMCP.csproj' } }
$results = @()
foreach ($item in $items) {
    foreach ($config in $Configurations) {
        $log = Join-Path $stage "$($item.Name)-$config.log"
        $extra = @("-p:LocalModsPath=$mods")
        if ($item.Name -eq 'JunctionStudio') { $extra += "-p:TownRoadLanePath=$town" }
        dotnet build $item.Project -c $config --nologo -v:minimal @extra *> $log
        $result = [PSCustomObject]@{ Mod = $item.Name; Config = $config; Exit = $LASTEXITCODE; Log = $log }
        $results += $result
        Write-Output "$($item.Name) $config exit=$($result.Exit)"
    }
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'results.json')
if ($results.Where({ $_.Exit -ne 0 }).Count) { throw 'Some builds failed; inspect artifacts/offline-portfolio/results.json and logs.' }
