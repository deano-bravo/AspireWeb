# Trust the local dev CA (k8s/.certs/ca.crt) in the current user's Root store so browsers
# accept https://localhost without warnings. No admin needed; re-running is idempotent.
# Restart the browser afterwards (Chromium caches root certs at startup).
#
#   powershell -File k8s/trust-ca.ps1
$ErrorActionPreference = "Stop"
$ca = Join-Path $PSScriptRoot ".certs\ca.crt"
if (-not (Test-Path $ca)) { throw "CA not found at $ca - run ./k8s/gen-cert.sh first." }

$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($ca)
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
$store.Open("ReadWrite")
$store.Add($cert)
$store.Close()

Write-Host "Trusted CA '$($cert.Subject)' [$($cert.Thumbprint)]."
Write-Host "Restart your browser, then open https://localhost"
Write-Host "To remove later: Get-ChildItem Cert:\CurrentUser\Root | Where-Object { `$_.Subject -like '*AspireWeb Local Dev CA*' } | Remove-Item"
