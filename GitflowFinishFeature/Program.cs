using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Octokit;
using Serilog;
using CommandLine;
using Serilog.Formatting;
using Serilog.Events;
using System.IO;
using Serilog.Context;

namespace GitflowFinishFeature
{
    class Program
    {
        // Adapted from https://semver.org/
        const string semverRegex = @"release\/(?<fullversion>(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?)$";

        public class Options
        {
            [Option("pr-num", Required = true)]
            public int PullRequestNumber { get; set; }
            [Option(Required = true)]
            public string Token { get; set; }
            [Option("repo-name", Required = true)]
            public string RepositoryName { get; set; }
            [Option("dev-branch-name", Required = false, Default = "develop")]
            public string DevelopmentBranchName { get; set; }
            [Option("del-rel-branch", Required = false, Default = true)]
            public bool DeleteReleaseBranch  { get; set; }
        }

        const string Group = "group";
        class GithubActionWorkflowFormatter : ITextFormatter
        {
            string activeGroup;

            public void Format(LogEvent logEvent, TextWriter output)
            {
                string level = logEvent.Level switch
                {
                    LogEventLevel.Debug => "::debug::",
                    LogEventLevel.Warning => "::warning::",
                    LogEventLevel.Error or LogEventLevel.Fatal => "::error::",
                    _ => string.Empty
                };

                if (logEvent.Properties.TryGetValue(Group, out var value) && string.IsNullOrEmpty(activeGroup))
                {
                    ScalarValue scalar = (ScalarValue)value;
                    output.WriteLine($"::group::{scalar.Value}");
                    activeGroup = value.ToString();
                }
                else if (!string.IsNullOrEmpty(activeGroup) && !logEvent.Properties.ContainsKey(Group))
                {
                    output.WriteLine("::endgroup::");
                    activeGroup = null;
                }

                output.WriteLine(level + logEvent.RenderMessage());
            }
        }

        static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new GithubActionWorkflowFormatter(), LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .CreateLogger();

            return await Parser.Default.ParseArguments<Options>(args)
                               .MapResult(Execute, _ => Task.FromResult(1));
        }

        public static async Task<int> Execute(Options o)
        {
            IDisposable group = default;
            int returnCode = 0;
            try
            {
                group = LogContext.PushProperty(Group, "Initial Setup");
                var client = new GitHubClient(new ProductHeaderValue("CompleteFeatureAction"));
                client.Connection.Credentials = new Credentials(o.Token);
                string[] splitNames = o.RepositoryName.Split('/');

                Log.Information("Fetching repository information for {Repo}", o.RepositoryName);
                var repo = await client.Repository.Get(splitNames[0], splitNames[1]);
                Log.Information("Fetching pull request #{PRNum}", o.PullRequestNumber);
                var pullRequest = await client.PullRequest.Get(repo.Id, o.PullRequestNumber);
                string branchName = pullRequest.Head.Ref;

                group.Dispose();

                if (pullRequest.Merged)
                {
                    Match m = Regex.Match(branchName, semverRegex);
                    if (m.Success && m.Groups["fullversion"].Success)
                    {
                        string version = m.Groups["fullversion"].Value;
                        Log.Information("Valid gitflow and semver branch name found; tag will be created with {Version}", version);

                        group = LogContext.PushProperty(Group, "Applying gitflow actions");

                        Log.Information("Getting branch information for {Branch}", branchName);
                        var branch = await client.Repository.Branch.Get(repo.Id, branchName);
                        var sha = pullRequest.MergeCommitSha;
                        var committer = (await client.Repository.Commit.Get(repo.Id, sha)).Commit.Author;

                        var newTag = new NewTag
                        {
                            Message = $"Release version {version}",
                            Object = sha,
                            Tagger = new Committer(committer.Name, committer.Email, DateTimeOffset.Now),
                            Type = TaggedType.Commit,
                            Tag = version
                        };

                        Log.Information("Creating new tag {Tag}", version);
                        var result = await client.Git.Tag.Create(repo.Id, newTag);
                        await client.Git.Reference.Create(repo.Id, new NewReference($"refs/tags/{result.Tag}", result.Sha));

                        Log.Information("Tag successfully created");

                        Log.Information("Merging {ReleaseBranch} into {DevBranch}", branchName, o.DevelopmentBranchName);
                        NewMerge merge = new NewMerge(o.DevelopmentBranchName, branchName);
                        var mergeRequest = await client.Repository.Merging.Create(repo.Id, merge);

                        if (o.DeleteReleaseBranch)
                        {
                            Log.Information("Deleting branch {Branch}", branchName);
                            await client.Git.Reference.Delete(repo.Id, $"heads/{branchName}");
                        }

                    }
                    else
                    {
                        Log.Warning("Head branch {Branch} does not match the required gitflow and semver syntax", branchName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("{Type}: {Message}", ex.GetType(), ex.Message);
                returnCode = -1;
            }
            finally
            {
                group?.Dispose();
            }
            return returnCode;
        }

    }
}
