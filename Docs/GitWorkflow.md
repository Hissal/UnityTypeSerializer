# Git Workflow

**Last Updated:** February 2, 2026

This page documents the Git workflow and branching strategy for the project.

---

## Overview

We use a **trunk-based development** workflow where `main` is the single source of truth and must always be release-ready.

### Key Principles

- **Main is sacred** — Always stable, always deployable
- **Short-lived branches** — Feature branches live for days, not weeks
- **Squash merging** — Keep main history clean and linear
- **Test before merge** — All changes must pass tests with latest main
- **Cherry-pick from releases** — Release branches never merge back, only cherry-pick
- **Clear branch types** — See [Refactoring Guide](#refactoring-and-optimization) for nuanced "refactoring" decisions

---

## Branch Types

### `main` — The Trunk

The primary development branch. Always release-ready.

**Rules:**
- ✅ Protected — Cannot push directly
- ✅ Requires PR with passing tests
- ✅ Must be deployable at all times
- ✅ Linear history via squash merges

**When to branch from:** All development work starts here

---

### `feat/` — New Features

For implementing new functionality or mechanics.

**Naming:** `feat/descriptive-name`

**Examples:**
```
feat/ranged-enemy
feat/dash-ability
feat/plasma-rifle
feat/level-selection-ui
```

**Workflow:**
1. Branch from `main`
2. Develop and commit regularly
3. Keep branch up-to-date with `main` (rebase)
4. Create PR when ready
5. Squash merge to `main` after approval
6. Delete branch after merge

**Best for:**
- New weapons, enemies, abilities
- New systems and mechanics
- New UI screens
- New levels or environments
- User-facing performance improvements (see [Refactoring Guide](#refactoring-and-optimization))

#### Spike and Prototype Conventions

Use prefixes within `feat/` branches to signal experimental or exploratory work:

**`feat/spike-*` — Technical Spikes**

Time-boxed technical research to answer specific questions or reduce uncertainty.

**Examples:**
```
feat/spike-multiplayer-netcode
feat/spike-procedural-generation
feat/spike-voice-chat-sdk
```

**Characteristics:**
- **Goal:** "Can we do this?" — Prove technical feasibility
- **Quality:** Rough, hacky, minimal — just enough to answer the question
- **Duration:** 1-3 days max
- **Outcome:** Learn → document findings → delete branch
- **Output:** Knowledge, not production code

**After the spike:**
- If successful → Create new proper `feat/` branch with clean implementation
- If unsuccessful → Delete branch, document why it didn't work, move on

**`feat/proto-*` — Prototypes**

Validating gameplay feel, visual style, or user experience concepts.

**Examples:**
```
feat/proto-dash-ability
feat/proto-arena-layout
feat/proto-weapon-feedback
```

**Characteristics:**
- **Goal:** "Should we build it?" — Validate design/feel
- **Quality:** Good enough to evaluate the experience
- **Duration:** Days to weeks of iteration
- **Outcome:** Iterate → refine → possibly rebuild properly or promote to production
- **Output:** Playable/demonstrable prototype

**Assets:** Use `--PROTO` tag in `_Dev/` folder (see [[1.2-Folder-Dev]])

**After prototyping:**
- If validated → Either clean up and merge, or rebuild properly in new `feat/` branch
- If invalidated → Delete branch, document learnings

---

### `fix/` — Bug Fixes

For fixing broken functionality in the development branch.

**Naming:** `fix/descriptive-name`

**Examples:**
```
fix/player-jump-height
fix/weapon-reload-animation
fix/memory-leak-particles
fix/ui-button-overlap
```

**Workflow:**
1. Branch from `main`
2. Fix the issue
3. Test thoroughly
4. Create PR
5. Squash merge to `main`
6. Delete branch after merge

**Best for:**
- Gameplay bugs
- Visual glitches
- Performance issues (see [Refactoring Guide](#refactoring-and-optimization))
- Logic errors

**Note:** Use `hotfix/` only for production release branches, not `main`.

---

### `chore/` — Maintenance Work

For maintenance tasks that don't affect the end product.

**Naming:** `chore/descriptive-name`

**Examples:**
```
chore/cleanup-prefabs
chore/update-dependencies
chore/reorganize-materials
chore/remove-unused-assets
```

**Workflow:**
Same as `feat/` branches

**Best for:**
- Asset organization
- Code cleanup and refactoring **that do not change behavior** (see [Refactoring Guide](#refactoring-and-optimization))
- CI/CD configuration changes
- Updating packages
- Documentation improvements
- Removing deprecated code
- Adding tests to existing systems without test coverage
- Fixing flaky or unreliable tests
- Improving test infrastructure

**`chore/test-*` — Creating Tests**

Tests should ideally come with features (test-driven development). Use `chore/test` for retrofitting tests to legacy code or improving existing test suites.

**`chore/docs-*` — Documentation Work**

For pure documentation work with no code or asset changes.

**Examples:**
```
chore/docs-update-weapon-guide
chore/docs-add-setup-instructions
```

**Best for:**
- Wiki updates
- README improvements
- Code comment additions/improvements
- API documentation
- Tutorial creation

**When to use:**
- ✅ Updating existing documentation
- ✅ Adding documentation for existing features
- ✅ Fixing documentation errors
- ✅ Pure documentation improvements

**When NOT to use:**
- ❌ Documentation should ideally come with the feature in `feat/` branches
- ❌ Don't separate docs if they're part of new feature work

---

### `tool/` — Internal Tooling

For developing editor tools, build scripts, and development utilities.

**Naming:** `tool/descriptive-name`

**Examples:**
```
tool/asset-validator
tool/build-automation
tool/prefab-batch-editor
tool/scene-setup-wizard
```

**Workflow:**
Same as `feat/` branches

**Best for:**
- Custom Unity editor tools
- Build pipeline improvements
- Asset processing scripts
- Development automation

---

### `import/` — Asset Imports

For bulk asset imports when doing nothing but importing.

**Naming:** `import/asset-name` or `import/asset-pack-name`

**Examples:**
```
import/weapon-models
import/environment-textures
import/audio-sfx-pack
import/ui-icons
```

**Workflow:**
1. Branch from `main`
2. Import assets **only**
3. Configure import settings (textures, models, audio)
4. **No code changes, no prefab setup beyond import settings**
5. Commit with descriptive message
6. Quick PR and merge

**Rules:**
- ⚠️ **Only** import and configure asset import settings
- ❌ No gameplay implementation
- ❌ No prefab creation or modification
- ❌ No code changes
- ❌ No scene modifications

**When to use:**
- ✅ Importing asset packs (textures, models, audio)
- ✅ Batch importing multiple unrelated assets
- ✅ Adding assets that will be used later
- ✅ When the only work is importing and configuring import settings

**When NOT to use:**
- ❌ Importing assets for a specific feature you're building
- ❌ Importing assets that need immediate implementation

**Alternative:** If you're working on `feat/crawler-enemy`, you can import the crawler's model, textures, and animations directly in that feature branch. `import/` branches are only for when you're doing nothing but imports with no implementation work.

**Rationale:** Keeps pure bulk imports separate from implementation work, making it easy to track what was imported vs. what was built. However, feature-specific imports belong with the feature.

**`import/package-`** — Third Party Package Imports

Use for importing third patry packages/assets/libraries

**Examples:**
```
import/package-feel
```

---

### `base/` — Large Collaborative Systems

**Rare.** For very large systems that multiple people work on simultaneously.

These are protected "Trunk" branches meaning that you cannot directly push to one and need to pr changes similarly to main.

**Naming:** `base/feat/system-name`

**Examples:**
```
base/feat/weapons-system
base/feat/inventory-system
base/feat/multiplayer-framework
```

**Workflow:**
1. Branch from `main`
2. Multiple developers create feature branches from the base branch:
   ```
   base/feat/weapons-system
   ├─ feat/weapon-shooting
   ├─ feat/weapon-reloading
   └─ feat/weapon-animations
   ```
3. Feature branches PR to the base branch
4. Once complete, base branch PRs to `main`
5. Squash merge to `main`

**Use sparingly:**
- ✅ Large architectural changes requiring coordination
- ✅ Systems with 3+ developers working simultaneously
- ✅ Work expected to take 2+ weeks with multiple sub-features

**Avoid for:**
- ❌ Normal feature development
- ❌ Solo work
- ❌ Work that can be broken into independent features

---

### `release/` — Production Releases

For stabilizing and shipping specific versions.

**Naming:** `release/version-number`

**Examples:**
```
release/0.1
release/1.0
release/1.3
release/2.0-alpha
```

**Workflow:**
1. Branch from `main` when ready to stabilize for release
2. Only `hotfix/` branches merge to release branches
3. **Never merge main to release**
4. **Never merge release to main**
5. Cherry-pick hotfixes to `main` when applicable
6. Tag final release: `git tag v1.0.0`

**Rules:**
- ⚠️ **No new features** — Bug fixes only
- ⚠️ Release branches are **frozen** — Main continues forward
- ✅ Hotfixes only
- ✅ Cherry-pick to main

**Lifecycle:**
```
main → release/1.0 → [hotfixes] → v1.0.0 (tag)
  ↓                       ↓
  ↓                  cherry-pick
  ↓                       ↓
  ↓← ← ← ← ← ← ← ← ← ← ← ←
  ↓
feat/new-stuff (continues development)
```

---

### `hotfix/` — Production Bug Fixes

**Only from release branches.** For critical fixes to shipped versions.

**Naming:** `hotfix/descriptive-name`

**Examples:**
```
release/1.3 → hotfix/enemy-falling-through-map
release/1.3 → hotfix/crash-on-level-load
release/2.0 → hotfix/weapon-damage-calculation
```

**Workflow:**
1. Branch from `release/X.X`
2. Fix the critical issue
3. Test thoroughly
4. PR and merge to `release/X.X`
5. **Cherry-pick to `main`** (if still relevant)
6. Tag new patch release if needed: `v1.3.1`

**Critical distinction:**
- `fix/` → Branches from `main`, fixes bugs in development
- `hotfix/` → Branches from `release/`, fixes bugs in production

---

### `backup/` — Backups

For taking snapshots before risky operations.

**Naming:** `backup/descriptive-name` or `backup/YYYY-MM-DD-description`

**Examples:**
```
backup/2026-01-12-before-refactor
backup/pre-physics-rewrite
backup/working-state-before-merge
```

**Rules:**
- ⚠️ Create locally before risky changes
- ⚠️ Optionally push to remote for safety
- ❌ **Never merge anywhere** (unless emergency recovery)
- ✅ Delete after work is stable

**Usage:**
```bash
# Create backup before risky work
git checkout -b backup/before-ai-rewrite

# Push if needed
git push origin backup/before-ai-rewrite

# Delete after work is confirmed stable
git branch -d backup/before-ai-rewrite
git push origin --delete backup/before-ai-rewrite
```

---

## Refactoring and Optimization

<a id="refactoring-and-optimization"></a>

Refactoring and optimization work can be challenging to categorize. Use this guide to determine whether your work belongs in `chore/`, `feat/`, or `fix/` branches.

### Use `chore/` When

Refactoring or optimization **does not change behavior or noticeable performance**.

**Criteria:**
- ✅ Code structure changes only
- ✅ Performance optimization is not noticeable to users
- ✅ Changes are purely internal/architectural
- ✅ Same inputs produce same outputs
- ✅ Users cannot tell the difference

**Examples:**
```
chore/refactor-weapon-class-structure      # Same behavior, cleaner code
chore/extract-player-input-methods         # Reorganizing, no logic change
chore/rename-variables-for-clarity         # Naming only
chore/consolidate-utility-functions        # Code organization
chore/optimize-internal-data-structures    # 2% performance gain, imperceptible
```

**Good for:**
- Code readability improvements
- Reducing technical debt
- Making future changes easier
- Minor internal optimizations

---

### Use `feat/` When

Performance improvement or refactor **is user-facing and positive**.

**Criteria:**
- ✅ Performance improvement is noticeable (better FPS, faster loading, smoother gameplay)
- ✅ Architectural refactor enables new capabilities
- ✅ Optimization improves gameplay experience
- ✅ Users benefit directly
- ✅ Worth mentioning in release notes as an improvement

**Examples:**
```
feat/optimize-particle-system              # 30 FPS → 60 FPS improvement
feat/reduce-memory-usage-for-mobile        # Enables mobile port
feat/refactor-ai-for-smarter-pathfinding   # AI behaves differently/better
feat/add-object-pooling                    # Improves performance noticeably
feat/optimize-loading-times                # Reduces load time by 50%
feat/improve-network-latency               # Multiplayer feels more responsive
```

**Good for:**
- Significant performance improvements
- Architectural changes that enable new features
- Optimizations that improve user experience
- Changes users will notice and appreciate

---

### Use `fix/` When

Refactor or optimization **fixes a problem or restores expected behavior**.

**Criteria:**
- ✅ Refactor fixes a bug
- ✅ Optimization fixes a performance issue
- ✅ Changes restore expected behavior
- ✅ Addresses a problem users are experiencing
- ✅ Would be in release notes as a bug fix

**Examples:**
```
fix/refactor-physics-to-prevent-tunneling  # Fixes collision bug via refactor
fix/optimize-shader-causing-fps-drops      # Fixes performance problem
fix/restructure-memory-to-prevent-leaks    # Fixes memory leak
fix/refactor-ai-to-stop-getting-stuck      # Fixes pathfinding bug
fix/optimize-particles-causing-crashes     # Fixes crash due to performance
```

**Good for:**
- Fixing bugs through refactoring
- Resolving performance problems
- Addressing crashes or stability issues
- Correcting unintended behavior

---

### Rule of Thumb

**Simple test:**
If the change would go in **release notes** as a feature or fix, don't use `chore/`. <br/>
If **users won't notice** the change, use `chore/`.

**Examples of edge cases:**

| Change | Branch Type | Rationale |
|--------|-------------|----------|
| Refactor reduces load time from 5s to 4.5s | `chore/` | 10% time reduction, barely noticeable |
| Refactor reduces load time from 5s to 2s | `feat/` | 60% reduction in load time, very noticeable |
| Optimize AI pathing (no behavior change) | `chore/` | Users can't tell the difference |
| Optimize AI pathing (smarter paths) | `feat/` | AI behaves better |
| Fix memory leak (refactor architecture) | `fix/` | Fixes a problem |
| Refactor for future multiplayer support | `chore/` | Enables future work, but no current benefit |
| Rename classes for clarity | `chore/` | Internal organization only |

---

### When in Doubt

Ask yourself:
1. **"Would a player notice this change?"**
   - Yes → `feat/` or `fix/`
   - No → `chore/`

2. **"Is this fixing a problem?"**
   - Yes → `fix/`
   - No → Check question 1

3. **"What would I write in release notes?"**
   - "Fixed..." → `fix/`
   - "Improved..." / "Added..." → `feat/`
   - Nothing / "Internal changes" → `chore/`

If still unclear, discuss with the team. Better to overcommunicate impact than hide significant changes.

---

## Pull Request Workflow

### Before Creating a PR

1. **Rebase with latest main:**
   ```bash
   git checkout main
   git pull origin main
   git checkout feat/your-branch
   git rebase main
   ```

2. **Resolve any conflicts**

3. **Test locally:**
   - Play through affected systems
   - Run automated tests (if any)
   - Check for errors in console
   - Verify performance

4. **Clean up commits** (optional):
   ```bash
   git rebase -i main
   ```

### Creating the PR

1. **Push your branch:**
   ```bash
   git push origin feat/your-branch
   ```

2. **Create PR on GitHub**

3. **Fill out PR template:**
   - **Title:** Clear, descriptive (e.g., "Add plasma rifle weapon")
   - **Description:** 
     - What changed
     - Why it changed
     - How to test it
     - Screenshots/videos if applicable
   - **Link related issues** if any

4. **Request reviewers**

### PR Requirements

- ✅ Passes all tests
- ✅ Up-to-date with latest `main`
- ✅ Code review approval (at least 1)
- ✅ No merge conflicts
- ✅ No broken references or missing assets

### After Approval

1. **Final rebase** (if main has moved):
   ```bash
   git checkout main
   git pull
   git checkout feat/your-branch
   git rebase main
   git push --force-with-lease
   ```

2. **Verify tests still pass**

3. **Squash merge to main:**
   - Use GitHub's "Squash and merge" button
   - Edit commit message to be clear and concise
   - All commits become one commit on main

4. **Delete the branch:**
   - Remote: Delete via UI or `git push origin --delete feat/your-branch`
   - Local: `git branch -d feat/your-branch`

---

## Commit Message Conventions

### Format

```
<type>: <short summary>

<optional detailed description>

<optional footer with issue references>
```

### Types

- `feat:` — New feature
- `fix:` — Bug fix
- `chore:` — Maintenance (no user-facing changes)
- `tool:` — Internal tooling
- `docs:` — Documentation only
- `import:` — Asset imports
- `style:` — Formatting (no code logic changes)
- `refactor:` — Code restructuring (no behavior change)
- `perf:` — Performance improvement
- `test:` — Adding or updating tests
- `build:` — Build system or dependencies
- `ci:` — CI/CD changes

### Examples

```
feat: add plasma rifle weapon

- Implemented shooting mechanics
- Added reload animation
- Integrated VFX and SFX
- Balanced damage and fire rate

Closes #234
```

```
fix: enemy AI not detecting player behind cover

Modified raycast to check from enemy head position
instead of center of mass.

Fixes #567
```

```
chore: reorganize weapon prefabs into subfolders

Moved all weapons into Weapon@Ranged_PF and Weapon@Melee_PF
following naming conventions.
```

### Commit Best Practices

- ✅ Present tense ("add feature" not "added feature")
- ✅ Imperative mood ("move cursor" not "moves cursor")
- ✅ Lowercase (except proper nouns)
- ✅ No period at the end of summary
- ✅ Summary under 72 characters
- ✅ Detailed description when needed
- ✅ Reference issues/tickets when relevant

---

## Git LFS (Large File Storage)

Git LFS is used for binary assets to keep repository size manageable.

### Tracked by LFS

- ✅ Textures (`.png`, `.tga`, `.jpg`, `.psd`)
- ✅ 3D models (`.fbx`, `.obj`, `.blend`)
- ✅ Audio files (`.wav`, `.mp3`, `.ogg`)
- ✅ Video files (`.mp4`, `.mov`)
- ✅ Unity asset bundles
- ✅ Large data files

### Not in LFS

- ❌ Code files (`.cs`)
- ❌ Text files (`.txt`, `.md`, `.json`, `.xml`)
- ❌ Unity scenes (`.unity`)
- ❌ Unity meta files (`.meta`)
- ❌ Prefabs (`.prefab`)
- ❌ Materials (`.mat`)
- ❌ ScriptableObjects (`.asset`)

### LFS Commands

```bash
# Check LFS status
git lfs status

# List LFS files
git lfs ls-files

# Pull LFS files
git lfs pull

# Push LFS files
git lfs push origin main
```

---

## Common Workflows

### Starting New Feature

```bash
# Update main
git checkout main
git pull origin main

# Create feature branch
git checkout -b feat/new-weapon

# Work and commit
git add .
git commit -m "feat: add shotgun base implementation"

# Push when ready
git push origin feat/new-weapon

# Create PR on GitHub
```

### Keeping Branch Up-to-Date

```bash
# Option 1: Rebase (preferred for clean history)
git checkout main
git pull origin main
git checkout feat/your-branch
git rebase main

# Resolve conflicts if any, then
git rebase --continue

# Force push (safe with --force-with-lease)
git push --force-with-lease origin feat/your-branch
```

### Creating a Release

```bash
# Ensure main is stable
git checkout main
git pull origin main

# Create release branch
git checkout -b release/1.0
git push origin release/1.0

# Continue development on main
git checkout main
git checkout -b feat/next-feature
```

### Hotfixing a Release

```bash
# Branch from release
git checkout release/1.3
git pull origin release/1.3
git checkout -b hotfix/critical-crash

# Fix the issue
git add .
git commit -m "fix: resolve crash on level load"

# Push and PR to release branch
git push origin hotfix/critical-crash
# Create PR: hotfix/critical-crash → release/1.3

# After merge, cherry-pick to main
git checkout main
git pull origin main
git cherry-pick <hotfix-commit-hash>
git push origin main

# Tag new patch version
git checkout release/1.3
git tag v1.3.1
git push origin v1.3.1
```

---

## Protected Branch Rules

### `main` Branch

- ✅ Require pull request reviews (minimum 1)
- ✅ Require status checks to pass
- ✅ Require branches to be up to date before merging
- ✅ Require linear history (squash or rebase)
- ✅ Restrict who can push (no one pushes directly)
- ✅ Restrict force pushes

### `release/*` Branches

- ✅ Require pull request reviews
- ✅ Only allow hotfix branches to merge
- ✅ Restrict force pushes
- ✅ Restrict deletions

---

## Branch Cleanup

### When to Delete Branches

**Immediately after merge:**
- ✅ `feat/` branches (including `feat/spike-*` and `feat/proto-*`)
- ✅ `fix/` branches
- ✅ `chore/` branches
- ✅ `tool/` branches
- ✅ `docs/` branches
- ✅ `import/` branches
- ✅ `hotfix/` branches

**Keep until work is done:**
- ⚠️ `release/` branches (keep until EOL)
- ⚠️ `base/` branches (keep until merged to main)

**Manual cleanup:**
- ⚠️ `backup/` branches (delete when no longer needed)

### Cleanup Commands

```bash
# Delete local branch
git branch -d feat/old-branch

# Delete remote branch
git push origin --delete feat/old-branch

# Prune deleted remote branches
git fetch --prune

# List merged branches
git branch --merged main

# Bulk delete merged branches (careful!)
git branch --merged main | grep -v "\*" | grep -v main | xargs -n 1 git branch -d
```

---

## Conflict Resolution

### During Rebase

```bash
# If conflicts occur during rebase
# 1. Open conflicted files and resolve
# 2. Stage resolved files
git add <resolved-files>

# 3. Continue rebase
git rebase --continue

# If you need to abort
git rebase --abort
```

### Tips for Avoiding Conflicts

- ✅ Rebase with main frequently
- ✅ Keep branches short-lived
- ✅ Communicate with team about overlapping work

---

## Best Practices

### Do's ✅

- **Commit often** — Small, focused commits are easier to review
- **Rebase frequently** — Stay up-to-date with main
- **Test before PR** — Verify everything works
- **Write clear commit messages** — Future you will thank you
- **Delete merged branches** — Keep repository clean
- **Use appropriate branch types** — Makes history clear
- **Keep PRs small** — Easier to review, faster to merge
- **Communicate** — Let team know about large changes

### Don'ts ❌

- **Don't push directly to main** — Always use PRs
- **Don't merge main to release** — Only cherry-pick
- **Don't commit work-in-progress** — Commit complete thoughts
- **Don't commit binary files without LFS** — Repository bloat
- **Don't force push to shared branches** — Breaks others' work
- **Don't leave branches hanging** — Clean up after merge
- **Don't commit broken code** — Always test locally first
- **Don't ignore merge conflicts** — Resolve them properly

---

## Emergency Procedures

### Revert Bad Merge to Main

```bash
# Find the merge commit
git log --oneline

# Revert it
git revert -m 1 <merge-commit-hash>
git push origin main
```

### Need to Recover Deleted Branch

```bash
# Find the commit
git reflog

# Recreate branch
git checkout -b recovered-branch <commit-hash>
```

### Accidentally Committed to Main

```bash
# DON'T PUSH!

# Move commits to new branch
git branch feat/accidental-work
git reset --hard origin/main
git checkout feat/accidental-work
```

---
