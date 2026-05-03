# Migrations — note

The initial migration in this folder (`20260502_Initial.cs`) was authored by hand
during the first commit, before `Loom.Web` existed as a startup project. Now
that it does, regenerate the migration via the standard EF tooling and remove
this note:

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

After regenerating, diff the new migration against the manual one and
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
