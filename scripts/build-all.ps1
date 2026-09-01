param(
    [string]$ToolPath = $env:CSII_TOOLPATH,
    [string]$TownRoadLanePath = $env:TOWN_ROAD_LANE_DLL,
    [switch]$SkipNpmInstall
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

if ([string]::IsNullOrWhiteSpace($ToolPath)) {
    $standardToolPath = 'C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Content\Game\.ModdingToolchain'
    if (Test-Path -LiteralPath $standardToolPath) { $ToolPath = $standardToolPath }
}
if ([string]::IsNullOrWhiteSpace($ToolPath) -or -not (Test-Path -LiteralPath (Join-Path $ToolPath 'Mod.props'))) {
    throw 'Set CSII_TOOLPATH or pass -ToolPath with the official CS2 modding toolchain directory.'
}

$env:CSII_TOOLPATH = [IO.Path]::GetFullPath($ToolPath)
$logRoot = Join-Path $repo 'artifacts/build-logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

$projects = @(
    @{ Name='FastTrack'; Project='FastTrack/FastTrack.csproj'; UI='FastTrack/UI' },
    @{ Name='RoadRules'; Project='RoadRules/RoadRules.csproj'; UI='RoadRules/UI' },
    @{ Name='SaveGuard'; Project='SaveGuard/SaveGuard.csproj'; UI='SaveGuard/UI' },
    @{ Name='TrafficStressLab'; Project='TrafficStressLab/TrafficStressTester.csproj'; UI='TrafficStressLab/UI' },
    @{ Name='AccessStudio'; Project='AccessStudio/AccessStudio.csproj'; UI='AccessStudio/UI' },
    @{ Name='CityPulse'; Project='CityPulse/CityPulse.csproj'; UI='CityPulse/UI' },
    @{ Name='ContourPlus'; Project='ContourPlus/ContourPlus.csproj'; UI='ContourPlus/UI' },
    @{ Name='CrashLens'; Project='CrashLens/CrashLens.csproj'; UI='CrashLens/UI' },
    @{ Name='JunctionStudio'; Project='JunctionStudio/JunctionStudio.csproj'; UI='JunctionStudio/UI' },
    @{ Name='EventEngine'; Project='EventEngine/EventEngine.csproj'; UI='EventEngine/UI' },
    @{ Name='MagicMarking'; Project='MagicMarking/src/MarkingStudio/MarkingStudio.csproj'; UI='MagicMarking/src/MarkingStudioUI' },
    @{ Name='NetworkStudio'; Project='NetworkStudio/NetworkStudio.csproj'; UI='NetworkStudio/UI' },
    @{ Name='Parkify'; Project='Parkify/Parkify.csproj'; UI='Parkify/UI' },
    @{ Name='DiscordRPC'; Project='DiscordRPC/DiscordRPC.csproj'; UI='DiscordRPC/UI' }
)

if ([string]::IsNullOrWhiteSpace($TownRoadLanePath)) {
    $cacheRoot = $env:LOCALAPPDATA + 'Low\Colossal Order\Cities Skylines II\.cache\Mods\pdx_mods'
    if (Test-Path -LiteralPath $cacheRoot) {
        $TownRoadLanePath = Get-ChildItem -LiteralPath $cacheRoot -Recurse -File -Filter 'TownRoadLane.dll' -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
}

foreach ($item in $projects) {
    $ui = Join-Path $repo $item.UI
    if (-not $SkipNpmInstall) {
        Push-Location $ui
        try {
            if (Test-Path -LiteralPath 'package-lock.json') { npm ci --no-audit --no-fund }
            else { npm install --no-audit --no-fund }
            if ($LASTEXITCODE -ne 0) { throw "npm install failed for $($item.Name)" }
        } finally { Pop-Location }
    }

    $project = Join-Path $repo $item.Project
    $extra = @()
    if ($item.Name -eq 'JunctionStudio') {
        if ([string]::IsNullOrWhiteSpace($TownRoadLanePath) -or -not (Test-Path -LiteralPath $TownRoadLanePath)) {
            throw 'Junction Studio requires TownRoadLane.dll. Set TOWN_ROAD_LANE_DLL or pass -TownRoadLanePath.'
        }
        $extra += "-p:TownRoadLanePath=$TownRoadLanePath"
    }

    dotnet restore $project --nologo -v:minimal @extra
    if ($LASTEXITCODE -ne 0) { throw "Restore failed for $($item.Name)" }
    dotnet clean $project -c Release --nologo -v:quiet @extra
    if ($LASTEXITCODE -ne 0) { throw "Clean failed for $($item.Name)" }
    $log = Join-Path $logRoot "$($item.Name).log"
    dotnet build $project -c Release --no-restore --nologo -v:minimal @extra "/flp:logfile=$log;verbosity=minimal"
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $($item.Name); see $log" }
}

Write-Output "Built $($projects.Count)/$($projects.Count) projects. Logs: $logRoot"
