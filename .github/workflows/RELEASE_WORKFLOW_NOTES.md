# Release Workflow - Implementation Notes

## Summary

This workflow automates the process of releasing the Unity Type Serializer package to a public repository while keeping the main development repository private.

## Key Features

### 1. Version Management
- **Tag-based releases**: Push a version tag (e.g., `v1.0.0`) to trigger automatic release
- **Manual dispatch**: Manually trigger with custom version number
- **Smart tag validation**: 
  - Prevents accidental re-releases of different code with same version
  - Allows safe re-publishing if tag points to current commit
  - Force republish option for intentional overwrites

### 2. Package Publishing
- Splits only the package directory (`Assets/Packages/Hissal/UnityTypeSerializer`)
- Updates `package.json` version in the public repo only
- Private repo stays clean (no version bump commits)
- Uses `git subtree split` to create isolated package branch

### 3. Authentication
The workflow handles GitHub Actions authentication carefully:

**Critical Fix**: Removes `http.https://github.com/.extraheader` configuration
- This header is set by `actions/checkout` and contains the GitHub Actions token
- It takes precedence over credentials in URLs
- Must be removed to use the `PUBLIC_REPO_TOKEN` for pushing to external repo

### 4. Release Creation
- Creates GitHub release in the **public repository**
- Uses GitHub API with `PUBLIC_REPO_TOKEN`
- Includes installation instructions and changelog link
- Gracefully handles errors (e.g., release already exists)

## Required Secrets

### `PUBLIC_REPO_TOKEN`
- Personal Access Token with repository write access
- **For classic tokens**: Select `repo` scope
- **For fine-grained tokens**: 
  - Select the public repository
  - Grant `Contents: Read and write` permission

### `PUBLIC_REPO_URL`
- Format: `github.com/USERNAME/REPO` or `github.com/USERNAME/REPO.git`
- No `https://` prefix
- Example: `github.com/Hissal/UnityTypeSerializer`

## Workflow Steps

1. **Checkout**: Fetches full git history for subtree split
2. **Determine Version**: Extracts from tag or manual input
3. **Validate Tag** (manual only): Checks tag exists and points to correct commit
4. **Split and Push**:
   - Validates URL and token
   - Clears Git authentication configs
   - Creates subtree split of package directory
   - Updates package.json version
   - Pushes to public repo
   - Pushes version tag to public repo
5. **Create Release**: Creates GitHub release in public repository

## Debugging

The workflow includes extensive debugging output:
- URL validation and normalization
- Token verification via GitHub API
- Git configuration status
- Credential helper status
- Detailed error messages with troubleshooting steps

## Usage Examples

### Automatic Release (Tag-based)
```bash
git tag v1.0.0
git push origin v1.0.0
```

### Manual Release (New Version)
```bash
gh workflow run release.yml -f version=1.0.0 -f create_tag=true
```

### Re-publish Existing Version
```bash
# If tag points to current commit
gh workflow run release.yml -f version=1.0.0 -f create_tag=false

# If tag is on different commit (use with caution!)
gh workflow run release.yml -f version=1.0.0 -f force_republish=true
```

## Troubleshooting

### Push Fails with 403
1. Verify token has write access to public repo
2. Check if fine-grained token has the repository selected
3. Ensure no branch protection rules block force push

### Release Creation Fails
1. Check if release already exists for that version
2. Verify tag exists in public repository
3. Confirm token has permission to create releases

### Tag Already Exists Error
1. Use a different version number, OR
2. Enable `force_republish` if intentionally republishing

## Version History Tracking

- **Private repo**: Version tracked via Git tags only
- **Public repo**: Version in `package.json` + Git tags
- No version bump commits in private repo (keeps history clean)
- Each release tag marks the exact code that was published

## Future Improvements

Possible enhancements:
- Automatic CHANGELOG generation
- Semantic version validation
- Pre-release support (alpha, beta, rc)
- Automated testing before release
- Slack/Discord notifications on release
