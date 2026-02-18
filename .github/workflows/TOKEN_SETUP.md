# GitHub Token Setup Guide

## Creating the Personal Access Token

### Option 1: Classic Token (Recommended for simplicity)

1. Go to https://github.com/settings/tokens
2. Click "Generate new token" → "Generate new token (classic)"
3. Give it a descriptive name (e.g., "UnityTypeSerializer Release Workflow")
4. Select expiration (or "No expiration" if preferred)
5. **Select scopes:**
   - ✅ Check `repo` (this enables full control of private repositories)
     - This will automatically select all sub-scopes
6. Click "Generate token"
7. **Copy the token immediately** (you won't be able to see it again)

### Option 2: Fine-grained Token (More secure, more complex)

1. Go to https://github.com/settings/personal-access-tokens/new
2. Give it a descriptive name
3. Set expiration
4. **Repository access:**
   - Select "Only select repositories"
   - Choose your **public repository** (the one you're pushing to)
5. **Permissions:**
   - Repository permissions → Contents → **Read and write**
   - Repository permissions → Metadata → **Read-only** (auto-selected)
6. Click "Generate token"
7. **Copy the token immediately**

## Configuring the Secrets

### 1. PUBLIC_REPO_TOKEN

In your **private repository** (the one with this workflow):

1. Go to `Settings` → `Secrets and variables` → `Actions`
2. Click "New repository secret"
3. Name: `PUBLIC_REPO_TOKEN`
4. Value: Paste the token you just created
5. Click "Add secret"

### 2. PUBLIC_REPO_URL

1. Click "New repository secret" again
2. Name: `PUBLIC_REPO_URL`
3. Value: Your public repository URL in the format: `github.com/USERNAME/REPO.git`
   - ⚠️ **No `https://` prefix**
   - ⚠️ **Must end with `.git`**
   - Example: `github.com/Hissal/UnityTypeSerializer.git`
4. Click "Add secret"

## Testing the Token

Before running the workflow, test your token manually:

### Using Git Command Line

```bash
# Clone using the token
git clone https://YOUR_TOKEN@github.com/USERNAME/REPO.git

# Or test push with existing repo
git remote add test-public https://YOUR_TOKEN@github.com/USERNAME/REPO.git
git push test-public main
git remote remove test-public
```

### Using GitHub API

```bash
# Test token validity (replace YOUR_TOKEN with your actual token)
curl -H "Authorization: token YOUR_TOKEN" https://api.github.com/user
```

If this returns your user information, the token is valid.

## Common Issues

### 403 Forbidden Error

**Causes:**
- Token doesn't have access to the repository
- Token has expired
- Token has insufficient permissions
- Wrong repository URL

**Solutions:**
1. **For fine-grained tokens:** Make sure the token has access to the specific repository
   - Go to token settings and check "Repository access"
   - The public repo must be in the list
2. **Check token expiration:** Tokens can expire
3. **Verify permissions:** Make sure `repo` scope (classic) or Contents write (fine-grained) is enabled
4. **Test manually:** Try cloning/pushing with the token outside of GitHub Actions

### Token Shows as `***` in Logs

This is expected - GitHub automatically masks secrets in logs for security.

### URL Format Issues

❌ Wrong:
- `https://github.com/USERNAME/REPO.git`
- `github.com/USERNAME/REPO`
- `USERNAME/REPO.git`

✅ Correct:
- `github.com/USERNAME/REPO.git`

## Security Best Practices

1. **Use fine-grained tokens when possible** - they have more granular permissions
2. **Set expiration dates** - rotate tokens regularly
3. **Use minimum required permissions** - only grant what's needed
4. **Never commit tokens to code** - always use GitHub Secrets
5. **Rotate tokens if exposed** - if a token is accidentally exposed, delete it immediately and create a new one

## Token Scope Requirements

### For Classic Tokens:
- `repo` - Full control of private repositories (includes public repositories)

### For Fine-grained Tokens:
- **Repository access**: Must include the public repository
- **Contents**: Read and write
- **Metadata**: Read-only (automatically selected)

## Verifying Setup

After configuring secrets, you can verify they're set correctly:

1. Go to your repository → `Settings` → `Secrets and variables` → `Actions`
2. You should see both:
   - `PUBLIC_REPO_TOKEN` (with masked value)
   - `PUBLIC_REPO_URL` (with masked value)
3. You can update them but not view them
4. If you need to change them, delete and recreate

## Need Help?

If you're still having issues:

1. Check the workflow run logs for specific error messages
2. Verify the token works manually using git commands
3. Ensure the public repository exists and you have write access
4. Check if the repository requires specific branch protection rules
