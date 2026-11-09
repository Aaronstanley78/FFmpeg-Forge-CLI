# PowerShell: convert every .mov of the current folder to MP4 and stop at the first error.
$forge = Join-Path $PSScriptRoot "..\forge.exe"
Get-ChildItem *.mov | ForEach-Object {
    & $forge $_.FullName -f mp4 --vcodec h264 --quality high -q
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed: $($_.Name)"; exit $LASTEXITCODE }
}
