FROM mcr.microsoft.com/dotnet/sdk:5.0 as build-env

COPY . ./
RUN dotnet publish ./GitflowFinishFeature/GitflowFinishFeature.csproj -c Release -o out --no-self-contained

LABEL maintainer="Michael Whitten"
LABEL repository="https://github.com/mdwhitten/gitflow-finish-release-action"
LABEL homepage="https://github.com/mdwhitten/gitflow-finish-release-action"

LABEL com.github.actions.name="Gitflow Finish Feature"
LABEL com.github.actions.description="GitHub Action to complete a release based off of the gitflow workflow."
# https://docs.github.com/actions/creating-actions/metadata-syntax-for-github-actions#branding
LABEL com.github.actions.icon="git-merge"
LABEL com.github.actions.color="white"

FROM mcr.microsoft.com/dotnet/runtime:5.0
COPY --from=build-env /out .
ENTRYPOINT [ "dotnet", "/GitflowFinishFeature.dll" ]