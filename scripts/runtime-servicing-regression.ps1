#Requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
[xml]$source = Get-Content (Join-Path $repo 'src/SlashText/SlashText.csproj') -Raw
$target = @($source.Project.Target | Where-Object Name -eq 'ValidateRuntimeServicing')
$minimum = @($source.Project.PropertyGroup.SlashDeskMinimumRuntimeVersion | Where-Object { $_ })
if ($target.Count -ne 1 -or $minimum.Count -ne 1) { throw 'Runtime servicing policy is missing or ambiguous.' }
$dotnet = (Get-Command dotnet -CommandType Application).Source
$version = [version]$minimum[0]
$later = '{0}.{1}.{2}' -f $version.Major, $version.Minor, ($version.Build + 1)
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('SlashDesk-runtime-policy-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$cases = @(
    @{ name='reviewed'; core=$minimum[0]; desktop=$minimum[0]; pass=$true },
    @{ name='later-servicing'; core=$later; desktop=$later; pass=$true },
    @{ name='old-core'; core='10.0.12'; desktop=$minimum[0]; pass=$false; error='below the reviewed minimum' },
    @{ name='old-desktop'; core=$minimum[0]; desktop='10.0.12'; pass=$false; error='below the reviewed minimum' },
    @{ name='missing-packs'; core=$null; desktop=$null; pass=$false; error='requires resolved Windows x64 runtime packs' }
)
try {
    foreach ($case in $cases) {
        # Execute the production MSBuild target against inert resolved-pack metadata.
        # No SDK/runtime/package is fabricated, downloaded or loaded by this fixture.
        $project = [xml]'<Project><PropertyGroup/><ItemGroup/></Project>'
        $properties = $project.SelectSingleNode('/Project/PropertyGroup')
        $items = $project.SelectSingleNode('/Project/ItemGroup')
        foreach ($entry in @{SelfContained='true';SlashDeskMinimumRuntimeVersion=$minimum[0]}.GetEnumerator()) {
            $property = $project.CreateElement($entry.Key)
            $property.InnerText = $entry.Value
            $null = $properties.AppendChild($property)
        }
        foreach ($kind in @('core','desktop')) {
            if (-not $case[$kind]) { continue }
            $id = if ($kind -eq 'core') { 'Microsoft.NETCore.App.Runtime.win-x64' } else { 'Microsoft.WindowsDesktop.App.Runtime.win-x64' }
            $pack = $project.CreateElement('ResolvedRuntimePack')
            $pack.SetAttribute('Include', $id)
            foreach ($entry in @{NuGetPackageId=$id;NuGetPackageVersion=$case[$kind]}.GetEnumerator()) {
                $metadata = $project.CreateElement($entry.Key)
                $metadata.InnerText = $entry.Value
                $null = $pack.AppendChild($metadata)
            }
            $null = $items.AppendChild($pack)
        }
        $null = $project.Project.AppendChild($project.ImportNode($target[0], $true))
        $path = Join-Path $fixture ($case.name + '.proj')
        $project.Save($path)
        $output = & $dotnet msbuild $path -target:ValidateRuntimeServicing -verbosity:minimal 2>&1
        $code = $LASTEXITCODE
        if (($code -eq 0) -ne $case.pass -or (-not $case.pass -and ($output -join "`n") -notlike ('*' + $case.error + '*'))) {
            throw "Runtime servicing case failed: $($case.name), exit $code`n$($output -join "`n")"
        }
        Write-Output "Runtime servicing $($case.name): PASS"
    }
} finally {
    # Only the unique directory created by this invocation is removed.
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
# Negative native calls must not leak their exit code to the Actions wrapper.
exit 0
