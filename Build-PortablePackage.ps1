#requires -Version 5.1
<# Creates a reviewed portable package from a verified Release output. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReleaseOutput,
    [Parameter(Mandatory=$true)][string]$VerifiedOutputManifest,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$ZipTimestamp,
    [switch]$ValidateOnly,
    [switch]$Candidate,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,31}$')][string]$CandidateLabel,
    [switch]$ApproveReleaseWithUnverifiedFFmpegSource
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
function Get-PackageZipName([string]$ProductVersion, [bool]$IsCandidate, [string]$Label) {
    $match = [regex]::Match($ProductVersion, '^(?<version>(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))(?:\+[0-9A-Za-z.-]+)?$')
    if (-not $match.Success) { throw "Unexpected product version: $ProductVersion" }
    $version = $match.Groups['version'].Value
    if ($IsCandidate) {
        $suffix = if ($Label) { '-candidate-' + $Label } else { '-candidate' }
        return 'SubEdit-Player-Next-' + $version + '-portable-x64' + $suffix + '.zip'
    }
    return 'SubEdit-Player-Next-' + $version + '.zip'
}
$sourceRoot=$PSScriptRoot
$runtime=[IO.Path]::GetFullPath($ReleaseOutput).TrimEnd('\')
$output=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
$date=[DateTime]::MinValue
if(-not [DateTime]::TryParseExact($ZipTimestamp,"yyyy-MM-dd'T'HH:mm:ss",[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::None,[ref]$date) -or
   $date.Year -lt 1981 -or $date.Year -gt 2107 -or $date.Second % 2 -ne 0){
    throw 'ZipTimestamp must be a fixed yyyy-MM-ddTHH:mm:ss value, year 1981-2107, with an even second (ZIP precision).'
}
# ZIP has no timezone field. Use these explicit UTC wall-clock components unchanged.
$entryTimestamp=[DateTimeOffset]::new($date.Year,$date.Month,$date.Day,$date.Hour,$date.Minute,$date.Second,[TimeSpan]::Zero)
if($CandidateLabel -and -not $Candidate){throw 'CandidateLabel requires Candidate.'}
if($Candidate -and $ApproveReleaseWithUnverifiedFFmpegSource){throw 'Owner release approval applies only to final packages; do not combine it with Candidate.'}
if($output -eq $runtime -or $output.StartsWith($runtime+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Package output must be outside the runtime input tree.'}
$noticePath=Join-Path $sourceRoot 'THIRD-PARTY-NOTICES.txt'
$notices=[IO.File]::ReadAllText($noticePath)
$sourceVerified=$notices -match '(?m)^FFMPEG SOURCE GATE: PASS\r?$'
$distributionReady=$notices -match '(?m)^PUBLIC DISTRIBUTION READY: true\r?$'
$review=Get-Content -LiteralPath $VerifiedOutputManifest -Raw | ConvertFrom-Json
if($review.Status -ne 'PASS'){throw 'Release output has not passed its audit.'}
foreach($item in $review.Files){
    $file=[IO.Path]::GetFullPath((Join-Path $runtime $item.Path))
    if(-not $file.StartsWith($runtime+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe runtime manifest path.'}
    if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $item.SHA256){throw "Runtime output changed after review: $($item.Path)"}
}
$productVersion=(Get-Item -LiteralPath (Join-Path $runtime 'SubEdit.Player.Next.dll')).VersionInfo.ProductVersion
$zipName=Get-PackageZipName $productVersion $Candidate.IsPresent $CandidateLabel
$ffmpegComplianceReady=$sourceVerified -and $distributionReady
$ownerReleaseApproved=$ApproveReleaseWithUnverifiedFFmpegSource.IsPresent
$publicRevisionReady=$productVersion -notmatch 'unknown|dirty'
$finalPackageAllowed=$publicRevisionReady -and ($ffmpegComplianceReady -or $ownerReleaseApproved)
if($ownerReleaseApproved){
    Write-Warning ('EXPLICIT OWNER RELEASE APPROVAL: packaging is authorized while FFmpeg source/distribution verification may remain incomplete. This does not confirm GPL compliance. FFmpegComplianceReady={0}.' -f $ffmpegComplianceReady)
}
if(-not $Candidate -and -not $ValidateOnly -and -not $finalPackageAllowed){throw 'Final package blocked: a clean public revision and verified FFmpeg source/distribution or explicit owner release approval are required.'}
if(Test-Path -LiteralPath $output){throw 'Use a new empty package directory; existing artifacts will not be overwritten.'}
$roots=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($name in @('SubEdit.Player.Next.exe','SubEdit.Player.Next.dll','SubEdit.Player.Next.deps.json','SubEdit.Player.Next.runtimeconfig.json')){[void]$roots.Add($name)}
$deps=Get-Content -LiteralPath (Join-Path $runtime 'SubEdit.Player.Next.deps.json') -Raw | ConvertFrom-Json
foreach($target in $deps.targets.PSObject.Properties){foreach($library in $target.Value.PSObject.Properties){
    $entry=$library.Value.PSObject.Properties['runtime']
    if($entry){foreach($asset in $entry.Value.PSObject.Properties){if($asset.Name.EndsWith('.dll')){[void]$roots.Add([IO.Path]::GetFileName($asset.Name))}}}
}}
$help=@('SubEditHelp.html','SkrotyKlawiszowe.html','faq_edycja.html')
$native=@('avcodec-63.dll','avfilter-12.dll','avformat-63.dll','avutil-61.dll','swresample-7.dll','swscale-10.dll')
$selected=[Collections.Generic.List[string]]::new()
foreach($file in Get-ChildItem -LiteralPath $runtime -Recurse -File -Force){
    $relative=$file.FullName.Substring($runtime.Length+1).Replace('\','/')
    $allow=($relative -notmatch '/' -and $roots.Contains($relative)) -or
      ($relative -match '^FFmpeg/([^/]+)$' -and $native -contains $Matches[1]) -or
      ($relative -in @('runtimes/win-x64/native/Scintilla.dll','runtimes/win-x64/native/Lexilla.dll')) -or
      ($relative -match '^doc/([^/]+)$' -and $help -contains $Matches[1])
    if(-not $allow){continue}
    if($relative -match '(?i)(^|/)(src|tests|bin|obj|\.cache|\.git|images)(/|$)|\.pdb$|SubEditHelp-Editor\.html$'){throw "Forbidden package entry: $relative"}
    $selected.Add($relative)
}
foreach($required in @($roots)+@($native|ForEach-Object {'FFmpeg/'+$_})+@($help|ForEach-Object {'doc/'+$_})+@('runtimes/win-x64/native/Scintilla.dll','runtimes/win-x64/native/Lexilla.dll')){
    if(-not $selected.Contains($required)){throw "Missing required runtime entry: $required"}
}
# Refuse private developer paths before creating a redistributable payload.
foreach($relative in $selected){
    $bytes=[IO.File]::ReadAllBytes((Join-Path $runtime $relative))
    $ascii=[Text.Encoding]::ASCII.GetString($bytes)
    $unicode=[Text.Encoding]::Unicode.GetString($bytes)
    $privatePattern='(?i)[A-Z]:[\\/](?:Users|AI)[\\/]|/(?:home|Users)/|[\\/]\.cache[\\/]'
    if($ascii -match $privatePattern -or $unicode -match $privatePattern){throw "Private build metadata in runtime input: $relative. Rebuild Release without debug metadata."}
}
$zip=Join-Path $output $zipName
if($ValidateOnly){
    $previewEntries=@(foreach($relative in $selected){
        $file=Join-Path $runtime $relative
        [pscustomobject]@{Path=$relative;Bytes=(Get-Item -LiteralPath $file).Length;SHA256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
    })
    foreach($name in @('LICENSE','THIRD-PARTY-NOTICES.txt')){
        $file=Join-Path $sourceRoot $name
        $previewEntries+= [pscustomobject]@{Path=$name;Bytes=(Get-Item -LiteralPath $file).Length;SHA256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
    }
    if($Candidate){
        $marker="Internal audit candidate. Not a final release. ProductVersion=$productVersion. FFmpeg Corresponding Source and all release blockers must be resolved before distribution.`n"
        $markerBytes=[Text.UTF8Encoding]::new($false).GetBytes($marker)
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$markerHash=[BitConverter]::ToString($sha.ComputeHash($markerBytes)).Replace('-','')}finally{$sha.Dispose()}
        $previewEntries+= [pscustomobject]@{Path='CANDIDATE-NOT-FOR-DISTRIBUTION.txt';Bytes=$markerBytes.Length;SHA256=$markerHash}
    }
    $byPreviewPath=@{}
    foreach($entry in $previewEntries){$byPreviewPath[$entry.Path]=$entry}
    $previewPaths=[string[]]@($previewEntries | ForEach-Object {$_.Path})
    [Array]::Sort($previewPaths,[StringComparer]::Ordinal)
    [pscustomobject]@{Status='VALIDATED / NO FILES WRITTEN';Zip=$zip;ZipName=$zipName;Files=$previewEntries.Count;ProductVersion=$productVersion;FinalPackageAllowed=$finalPackageAllowed;FFmpegComplianceReady=$ffmpegComplianceReady;FFmpegSourceVerification=if($sourceVerified){'VERIFIED'}else{'UNVERIFIED'};OwnerReleaseApproval=if($ownerReleaseApproved){'EXPLICIT'}else{'NOT PROVIDED'};Manifest=@($previewPaths | ForEach-Object {$byPreviewPath[$_]})}
    return
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$package=Join-Path $output 'package-staging'
New-Item -ItemType Directory -Path $package | Out-Null
foreach($relative in $selected){
    $destination=Join-Path $package $relative
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $runtime $relative) -Destination $destination
}
foreach($name in @('LICENSE','THIRD-PARTY-NOTICES.txt')){Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination $package}
if($Candidate){[IO.File]::WriteAllText((Join-Path $package 'CANDIDATE-NOT-FOR-DISTRIBUTION.txt'),"Internal audit candidate. Not a final release. ProductVersion=$productVersion. FFmpeg Corresponding Source and all release blockers must be resolved before distribution.`n",[Text.UTF8Encoding]::new($false))}
$entries=@(Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object{[pscustomobject]@{Path=$_.FullName.Substring($package.Length+1).Replace('\','/');Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$byPath=@{}
foreach($entry in $entries){$byPath[$entry.Path]=$entry}
$sortedPaths=[string[]]@($entries | ForEach-Object {$_.Path})
[Array]::Sort($sortedPaths,[StringComparer]::Ordinal)
$manifest=@($sortedPaths | ForEach-Object {$byPath[$_]})
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'package-manifest.json') -Encoding utf8
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($entry in $manifest){
    $created=$archive.CreateEntry($entry.Path,[IO.Compression.CompressionLevel]::Optimal)
    $created.LastWriteTime=$entryTimestamp
    $inputStream=[IO.File]::OpenRead((Join-Path $package $entry.Path))
    $outputStream=$created.Open()
    try{$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
}}finally{$archive.Dispose()}
$result=[ordered]@{Status=if($Candidate){'CANDIDATE / NOT FINAL / NOT FOR DISTRIBUTION'}else{'PACKAGED / PUBLICATION REQUIRES SEPARATE APPROVAL'};Zip=$zip;SHA256=(Get-FileHash -LiteralPath $zip).Hash;Bytes=(Get-Item -LiteralPath $zip).Length;Files=$manifest.Count;ProductVersion=$productVersion;ZipTimestamp=$ZipTimestamp;ZipTimestampConvention='Fixed UTC wall-clock components; ZIP has no timezone field';FFmpegComplianceReady=$ffmpegComplianceReady;FFmpegSourceVerification=if($sourceVerified){'VERIFIED'}else{'UNVERIFIED'};OwnerReleaseApproval=if($ownerReleaseApproved){'EXPLICIT'}else{'NOT PROVIDED'}}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'package-result.json') -Encoding utf8
$result | ConvertTo-Json -Depth 4
