# Migrations — note

The initial migration in this folder (`20260502_Initial.cs`) was authored by hand
during the first commit so the repo would be buildable end-to-end without yet
having a `Loom.Web` startup project. Once `Loom.Web` exists, regenerate the
migration via the standard EF tooling and remove this note:

```bash
# Remove the hand-authored migration
dotnet ef migrations remove \
  --project src/Loom.Infrastructure \
  --startup-project src/Loom.Web

# Regenerate from the model
dotnet ef migrations add Initial \
  --project src/Loom.Infrastructure \
  --startup-project src/Loom.Web \
  --output-dir Persistence/Migrations
```

Until then, the design-time factory at `Persistence/LoomDbContextFactory.cs`
provides what the EF tooling needs. The connection string defaults to
`(localdb)\\MSSQLLocalDB` and can be overridden via the
`LOOM_DESIGN_TIME_CONNECTION` environment variable.

After the regeneration, diff the new migration against the manual one and
ensure the column types, indexes, and FK behaviours match what the integration
tests (`tests/Loom.Infrastructure.Tests/Persistence/RoundTripTests.cs`) expect.

## Migration discipline

Once a migration is merged to `main`, treat it as immutable. Schema changes
are additive, via new migrations. Editing a shipped migration is a footgun
because environments that have already applied it will diverge from those
that haven't.

If you absolutely must squash early migrations (e.g. before the first
production deployment), do it in a single PR that explains why, and update
this note.
