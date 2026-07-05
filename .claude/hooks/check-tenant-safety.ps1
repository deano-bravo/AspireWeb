# PostToolUse scan (Edit|Write): tenancy data-safety tripwires in edited C#/Razor files.
# Findings go to stderr with exit 2, which feeds them back to the model for self-correction.
# Windows-only (solo Windows repo); cross-platform contributors should move the hook config
# from .claude/settings.json to their .claude/settings.local.json.
# Fail-open by design: any error -> exit 0 so a broken payload never blocks work.
try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $path = $payload.tool_input.file_path
    if (-not $path -or $path -notmatch '\.(cs|razor)$' -or -not (Test-Path -LiteralPath $path)) { exit 0 }

    $text = Get-Content -Raw -LiteralPath $path
    $problems = @()
    if ($text -match 'IgnoreQueryFilters\s*\(\s*\)') {
        $problems += 'BANNED: bare IgnoreQueryFilters() drops tenant isolation. Use IgnoreQueryFilters([TenantDbContext.TenantFilterName]) with an explicit justification (see CLAUDE.md Multi-tenancy).'
    }
    if ($text -match '(FromSqlRaw|FromSqlInterpolated|FromSql|ExecuteSqlRaw|ExecuteSqlRawAsync|ExecuteSql|ExecuteSqlAsync|SqlQueryRaw|SqlQuery)\s*[(<]') {
        $problems += 'Raw SQL detected: repo rule requires a tenant_id predicate in every raw query (see CLAUDE.md Multi-tenancy). Verify the predicate or refactor to LINQ over the filtered context.'
    }
    if ($problems.Count -gt 0) {
        [Console]::Error.WriteLine(($problems -join "`n"))
        exit 2
    }
    exit 0
} catch {
    exit 0
}
