using Serilog.Formatting;
using Serilog.Events;
using System.IO;

namespace GitflowFinishFeature
{
    /// <summary>
    /// Simple text formatter to log information in the GitHub action log to be properly formatted on the webpage.
    /// 
    /// See https://docs.github.com/en/actions/reference/workflow-commands-for-github-actions
    /// </summary>
    public class GithubActionWorkflowFormatter : ITextFormatter
    {
        public const string Group = "group";
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

            // If the log context property group has been set and it has not yet been stored, this is the first event
            // after it has been added to the log context. Hence, write it before the event.
            if (logEvent.Properties.TryGetValue(Group, out var value) && string.IsNullOrEmpty(activeGroup))
            {
                ScalarValue scalar = (ScalarValue)value;
                output.WriteLine($"::group::{scalar.Value}");
                activeGroup = value.ToString();
            }
            // If activeGroup has a value yet the log event doesn't have the property, it has been removed 
            // from the log context, so write the group closure message before proceeding.
            else if (!string.IsNullOrEmpty(activeGroup) && !logEvent.Properties.ContainsKey(Group))
            {
                output.WriteLine("::endgroup::");
                activeGroup = null;
            }

            output.WriteLine(level + logEvent.RenderMessage());
        }
    }
}
