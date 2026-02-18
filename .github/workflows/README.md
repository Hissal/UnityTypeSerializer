# GitHub Actions Workflows

## Release Workflow

The `release.yml` workflow automates the package release process, including updating version numbers and pushing to the public repository.

### Setup

#### Required Secrets

You need to configure the following secrets in your GitHub repository settings (`Settings > Secrets and variables > Actions`):

1. **PUBLIC_REPO_TOKEN**: A Personal Access Token (PAT) with `repo` permissions for the public repository
   - Create at: https://github.com/settings/tokens
   - Required scopes: `repo` (Full control of private repositories)

2. **PUBLIC_REPO_URL**: The URL of your public repository (without `https://`)
   - Format: `github.com/USERNAME/REPO_NAME.git`
   - Example: `github.com/Hissal/UnityTypeSerializer.git`

#### Setting up your Git remote locally

To test locally, you can configure your public remote:
```bash
git remote add public https://github.com/USERNAME/REPO_NAME.git
```

### Usage

#### Option 1: Tag-based Release (Automatic)

Push a version tag to trigger the release:

```bash
# Create and push a tag
git tag v1.0.0
git push origin v1.0.0
```

The workflow will:
1. Detect the version from the tag
2. Split the package directory using `git subtree`
3. Update `package.json` with the version in the split branch only
4. Push to the public repository
5. Create a GitHub release

#### Option 2: Manual Dispatch

Trigger the release manually from GitHub Actions UI or via CLI:

**Via GitHub UI:**
1. Go to `Actions` tab
2. Select `Release Package` workflow
3. Click `Run workflow`
4. Enter the version number (e.g., `1.0.0`)
5. Choose whether to create a tag

**Via GitHub CLI:**
```bash
# With tag creation
gh workflow run release.yml -f version=1.0.0 -f create_tag=true

# Without tag creation
gh workflow run release.yml -f version=1.0.0 -f create_tag=false
```

The workflow will:
1. Optionally create and push the version tag
2. Split the package directory using `git subtree`
3. Update `package.json` with the specified version in the split branch only
4. Push to the public repository
5. Create a GitHub release (if tag was created)

### Workflow Details

#### What gets pushed to the public repository

Only the contents of `Assets/Packages/Hissal/UnityTypeSerializer/` directory are pushed to the public repository. This includes:
- `package.json` (with updated version - only in public repo)
- All package source files
- Documentation files within the package

**Note:** The `package.json` version is only updated in the public repository. The private repository's package.json remains unchanged, and version tracking is done via Git tags instead.

#### Version numbering

- For tag-based releases: Version is extracted from the tag (e.g., `v1.0.0` → `1.0.0`)
- For manual releases: Version is taken from the workflow input
- Version is applied to the split branch before pushing to public repo
- No version bump commits are created in the private repository

#### Branch strategy

- The package is always pushed to the `main` branch of the public repository
- The split is forced (`--force`) to ensure the public repository stays in sync
- A temporary branch `pkg-split` is created and deleted during the process

### Troubleshooting

**Error: "remote public already exists"**
- The workflow cleans up remotes automatically, but if it fails, the next run will handle it

**Error: "Authentication failed"**
- Check that `PUBLIC_REPO_TOKEN` is valid and has correct permissions
- Ensure `PUBLIC_REPO_URL` is correctly formatted (no `https://` prefix)


**Split fails or wrong files are pushed**
- Ensure the path `Assets/Packages/Hissal/UnityTypeSerializer` exists
- Check that the directory structure hasn't changed

### Manual Release Process (for reference)

If you need to release manually without the workflow:

```bash
# Create and push tag (no need to update package.json in private repo)
git tag vX.Y.Z
git push origin vX.Y.Z

# Split and push to public repo
git subtree split --prefix=Assets/Packages/Hissal/UnityTypeSerializer -b pkg-split

# Checkout split branch and update version there
git checkout pkg-split
jq --arg version "X.Y.Z" '.version = $version' package.json > package.json.tmp
mv package.json.tmp package.json
git add package.json
git commit -m "chore: release version X.Y.Z"

# Push to public repo
git push public pkg-split:main --force

# Clean up
git checkout -
git branch -D pkg-split
```
