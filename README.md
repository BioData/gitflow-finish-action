# Gitflow Finish Release Action
A GitHub action to complete a release based off of the gitflow workflow.

Performs the following actions:
- Validates that the PR is merged
- Validates that the head branch matches the gitflow and semver specifications (release/major.minor.patch etc.)
- Creates a new tag with the release version
- (Optional) Merges the release branch back to develop
- (Optional) Deletes the release branch


``` yaml
on:
  # Triggers the workflow on push or pull request events but only for the main branch
  pull_request:
    branches: [ main ]
    types: [closed]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: mdwhitten/gitflow-finish-release-action@v1.0.0
        with:
            pr-num: ${{ github.event.number }}
            token: ${{ secrets.GITHUB_TOKEN }}
            repo-name: ${{ github.repository }}
            # Optional - sets whether to merge the release branch back into the development branch automatically.
            merge-into-dev: true
            # Optional - the name of the development branch to merge the release branch into.
            dev-branch-name: develop
            # Optional - indicates whether to delete the release branch after completing all gitflow actions.
            del-rel-branch: true
```
