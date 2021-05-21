using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Octokit;
using Serilog;
using CommandLine;
using Serilog.Events;
using Serilog.Context;

namespace GitflowFinishFeature
{
    partial class Program
    {
        // Adapted from https://semver.org/
        const string semverRegex = @"release\/v?(?<fullversion>(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)"
                                 + @"(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))"
                                 + @"?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?)$";
        const string Group = GithubActionWorkflowFormatter.Group;

        static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new GithubActionWorkflowFormatter(), LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .CreateLogger();

            return await Parser.Default.ParseArguments<CommandLineOptions>(args)
                               .MapResult(Execute, _ => Task.FromResult(1));
        }

        public static async Task<int> Execute(CommandLineOptions o)
        {
            IDisposable group = default;
            int returnCode = 0;
            try
            {
                // Create a group in the GitHub log
                group = LogContext.PushProperty(Group, "Initial Setup");
                var client = new GitHubClient(new ProductHeaderValue("CompleteReleaseAction"));
                client.Connection.Credentials = new Credentials(o.Token);

                Match repoText = Regex.Match(o.RepositoryName, @"(?<owner>[\w.-]+)\/(?<repo>[\w.-]+)");
                if (repoText.Success)
                {
                    string owner = repoText.Groups["owner"].Value;
                    string repoName = repoText.Groups["repo"].Value;
                    Log.Information("Fetching repository information for {Repo}", o.RepositoryName);
                    var repo = await client.Repository.Get(owner, repoName);
                    Log.Information("Fetching pull request #{PRNum}", o.PullRequestNumber);
                    var pullRequest = await client.PullRequest.Get(repo.Id, o.PullRequestNumber);
                    string headBranchName = pullRequest.Head.Ref;
                    string baseBranchName = pullRequest.Base.Ref;

                    group.Dispose();

                    if (pullRequest.Merged)
                    {
                        Match m = Regex.Match(headBranchName, semverRegex);
                        if (m.Success && m.Groups["fullversion"].Success)
                        {
                            group = LogContext.PushProperty(Group, "Applying Gitflow actions");

                            string version = m.Groups["fullversion"].Value;
                            string tagText = 'v' + version;
                            Log.Information("Valid gitflow and semver branch name found; tag will be created with {Version}", version);

                            Log.Information("Getting branch information for {Branch}", headBranchName);
                            var branch = await client.Repository.Branch.Get(repo.Id, headBranchName);
                            var mergeSha = pullRequest.MergeCommitSha;
                            var mergeCommitInfo = await client.Repository.Commit.Get(repo.Id, mergeSha);
                            var committer = mergeCommitInfo.Commit.Author;

                            var newTag = new NewTag
                            {
                                Message = $"Release version {version}",
                                Object = mergeSha,
                                Tagger = new Committer(committer.Name, committer.Email, DateTimeOffset.Now),
                                Type = TaggedType.Commit,
                                Tag = tagText
                            };

                            Log.Information("Creating new tag {Tag} on branch {BaseBranch} at commit {Commit}", version, baseBranchName, mergeSha);
                            var result = await client.Git.Tag.Create(repo.Id, newTag);
                            await client.Git.Reference.Create(repo.Id, new NewReference($"refs/tags/{result.Tag}", result.Sha));

                            Log.Information("Tag successfully created");

                            if (o.MergeIntoDevelopmentBranch)
                            {
                                Log.Information("Merging {ReleaseBranch} into {DevBranch}", headBranchName, o.DevelopmentBranchName);
                                NewMerge merge = new NewMerge(o.DevelopmentBranchName, headBranchName);
                                var mergeRequest = await client.Repository.Merging.Create(repo.Id, merge);

                                Log.Information("Successfully merged {ReleaseBranch} into {DevBranch} with commit {ID}",
                                    headBranchName, o.DevelopmentBranchName, mergeRequest.Sha);
                            }

                            if (o.DeleteReleaseBranch)
                            {
                                Log.Information("Deleting branch {Branch}", headBranchName);
                                await client.Git.Reference.Delete(repo.Id, $"heads/{headBranchName}");
                                Log.Information("Successfully deleted {Branch}", headBranchName);
                            }
                        }
                        else
                        {
                            Log.Warning("Head branch {Branch} does not match the required gitflow and semver syntax. No actions will be taken.", headBranchName);
                        }
                    }
                    else
                    {
                        Log.Warning("Pull request {Num} has not been merged. No actions will be taken.", pullRequest.Number);
                    }
                }
                else
                {
                    Log.Error("Invalid repository name passed; repository name should be of format \"owner/repo-name\"");
                }
            }
            catch (Exception ex)
            {
                Log.Error("{Type}: {Message}", ex.GetType(), ex.Message);
                returnCode = -1;
            }
            finally
            {
                // Close any open group if not already closed
                group?.Dispose();
            }
            return returnCode;
        }

    }
}
