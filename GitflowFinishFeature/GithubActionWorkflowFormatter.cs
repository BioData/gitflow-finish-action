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

            bool groupPresent = logEvent.Properties.TryGetValue(Group, out var value);
            object actualValue = ((ScalarValue)value).Value;

            // If the log context property group has been set and it has not yet been stored, this is the first event
            // after it has been added to the log context. Hence, write it before the event.
            bool newGroupPushed = groupPresent && string.IsNullOrEmpty(activeGroup);
            // If activeGroup has a value yet the log event doesn't have the property, it has been removed 
            // from the log context, so write the group closure message before proceeding.
            bool existingGroupDisposed = !string.IsNullOrEmpty(activeGroup) && !groupPresent;
            // If we have an active group and the new event has a group property, but this does not match the current group,
            // this means the original was disposed but a new group property has been added to the log context. Hence, we need
            // to close out the old group and open a new group.
            bool changedGroup = !string.IsNullOrEmpty(activeGroup) && groupPresent && !activeGroup.Equals(actualValue);

            if (existingGroupDisposed || changedGroup)
            {
                output.WriteLine("::endgroup::");
                activeGroup = null;
            }
            if (newGroupPushed || changedGroup)
            {
                activeGroup = actualValue.ToString();
                output.WriteLine($"::group::{actualValue}");
            }

            output.WriteLine(level + logEvent.RenderMessage());
        }
    }
}
