using CommandLine;

namespace GitflowFinishFeature
{
    public class CommandLineOptions
    {
        [Option("pr-num", Required = true, HelpText = "The number of the pull request to finish.")]
        public int PullRequestNumber { get; set; }
        [Option(Required = true, HelpText = "Authentication token to allow the action to execute.")]
        public string Token { get; set; }
        [Option("repo-name", Required = true, HelpText = "The full name of the containing repository in the form owner/repo.")]
        public string RepositoryName { get; set; }
        [Option("merge-into-dev", Default = false, HelpText = "Sets whether to merge the release branch back into the development branch automatically.")]
        public bool? MergeIntoDevelopmentBranch { get; set; }
        [Option("dev-branch-name", Required = false, Default = "develop", HelpText = "The name of the development branch to merge the release branch into.")]
        public string DevelopmentBranchName { get; set; }
        [Option("del-rel-branch", Required = false, Default = true, HelpText = "Sets whether to delete the release branch after completing all gitflow actions.")]
        public bool? DeleteReleaseBranch { get; set; }
    }
}
