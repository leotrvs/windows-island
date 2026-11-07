# Releasing

Automatic updates need a **public** repository, because the app downloads release files without signing in. While the repository is private, installed copies simply find no update.

## First time

```powershell
pwsh -File scripts/new-signing-key.ps1
```

This writes the private key to `~\.windows-island-signing\update-signing-key.xml` and the public key to `src\UpdateKey.cs`. Commit `UpdateKey.cs`, and back up the private key somewhere safe, such as a password manager.

**Never commit the private key.** If it is lost, installed copies can no longer update automatically and users must reinstall once. If it leaks, remove every release signed with it, publish a new key in a new version, and tell users to reinstall.

Forks must create their own key and change `BaseUrl` in `src\Updater.cs`.

## Each release

1. Update `<Version>` in `src\WindowsIsland.csproj` and add an entry to `CHANGELOG.md`.
2. Commit and push.
3. Build and sign without publishing, then test `bin\release\WindowsIsland.exe`:

   ```powershell
   pwsh -File scripts/release.ps1
   ```

4. Publish:

   ```powershell
   pwsh -File scripts/release.ps1 -Publish
   ```

The script writes the release notes from the version's section in `CHANGELOG.md` and adds the SHA-256 of the app.

The release must include exactly these files: `WindowsIsland.exe`, `update.json` and `update.json.sig`.

Release from a local machine only. Do not put the private key in GitHub Actions or any other online service.

## Testing an update

Build two copies with `-p:DefineConstants=UPDATE_TEST` (one of them with a higher `-p:Version`), serve the higher one with `update.json` and `update.json.sig` at `http://127.0.0.1:8766/`, and run the lower one with `--portable`. It checks after 3 seconds. Never publish an `UPDATE_TEST` build.
