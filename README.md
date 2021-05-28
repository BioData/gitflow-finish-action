# Gitflow Finish Action
A GitHub action to complete a release or hotfix branch based off of the gitflow workflow after a pull request has been merged into the main branch.

Performs the following actions:
- Validates that the PR is merged
- Validates that the head branch matches the gitflow and semver specifications ([release|hotfix]/major.minor.patch etc.). Valid branch names:
  - release/1.0.0
  - hotfix/v1.2.0
  - release/1.5.0+sha1
- Creates a new tag with the release/hotfix version
- (Optional) Merges the release/hotfix branch back to develop
- (Optional) Deletes the release/hotfix branch


``` yaml
name: Gitflow Finish Feature

on:
  # Triggers the workflow on push or pull request events but only for the main branch
  pull_request:
    branches: [ main ]
    types: [closed]

jobs:
  gitflow-finish:
    runs-on: ubuntu-latest
    steps:
      - uses: mdwhitten/gitflow-finish-action@v1.2.0
        with:
            pr-num: ${{ github.event.number }}
            token: ${{ secrets.GITHUB_TOKEN }}
            repo-name: ${{ github.repository }}
            # Sets whether to delete the release/hotfix branch after completing all giftlow actions
            del-rel-branch: true
            # Optional - sets whether to merge the release/hotfix branch back into the development branch automatically.
            merge-into-dev: true
            # Optional - the name of the development branch to merge the release/hotfix branch into.
            dev-branch-name: develop
            # Optional - indicates whether to delete the release/hotfix branch after completing all gitflow actions.
            del-source-branch: true
            # Optional - text to prepend to the version when creating a tag.
            tag-prefix: v
```
