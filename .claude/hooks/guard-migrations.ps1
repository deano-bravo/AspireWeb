# PreToolUse guard (Edit|Write): AspireWeb.Data/Migrations/** is EF-generated — never hand-edit;
# regenerate via /ef-migration. Emits an "ask" decision so a deliberate, justified override stays possible.
# Windows-only (solo Windows repo); cross-platform contributors should move the hook config
# from .claude/settings.json to their .claude/settings.local.json.
# Fail-open by design: any error -> exit 0 so a broken payload never blocks work.
try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $path = $payload.tool_input.file_path
    if ($path -and $path -match 'AspireWeb\.Data[\\/]Migrations[\\/]') {
        @{
            hookSpecificOutput = @{
                hookEventName            = 'PreToolUse'
                permissionDecision       = 'ask'
                permissionDecisionReason = 'AspireWeb.Data/Migrations/** is EF-generated code (never hand-edit). Regenerate via /ef-migration instead. Approve only for a deliberate, justified override.'
            }
        } | ConvertTo-Json -Depth 4 -Compress | Write-Output
    }
    exit 0
} catch {
    exit 0
}
