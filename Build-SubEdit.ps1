$ErrorActionPreference = 'Stop'


$buildArguments = @($args)
$releaseBuild = $false
for ($index = 0; $index -lt $buildArguments.Count; $index++) {
    $argument = [string]$buildArguments[$index]
    if (($argument -in @('-c', '--configuration')) -and $index + 1 -lt $buildArguments.Count) {
        $releaseBuild = [string]$buildArguments[$index + 1] -eq 'Release'
    }
    elseif ($argument -match '^(?:--configuration=|[-/](?:p|property):Configuration=)([^;]+)$') {
        $releaseBuild = $Matches[1] -eq 'Release'
    }
}
if ($releaseBuild) {
    # Release artifacts must not disclose CodeView/PDB paths from the build machine.
    $buildArguments += @('-p:DebugType=None', '-p:DebugSymbols=false')
}
dotnet build (Join-Path $PSScriptRoot 'SubEdit.Player.slnx') @buildArguments
exit $LASTEXITCODE
