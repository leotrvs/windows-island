using System;
using System.Threading.Tasks;

namespace WindowsIsland
{
    // A permission that Claude Code is about to ask for, handed to the island by its hook so the answer can be
    // clicked on the island. Used on the UI thread only. "none" means no decision: Claude then shows its own prompt.
    sealed class ApiPermission
    {
        public string Session;
        public string Tool;
        public string Summary;

        readonly TaskCompletionSource<string> source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> Answer { get { return source.Task; } }
        public bool IsClosed { get; private set; }
        public event Action Closed;

        // "allow", "deny" or "none"
        public void Resolve(string decision)
        {
            if (IsClosed) return;
            IsClosed = true;
            source.TrySetResult(decision == "allow" || decision == "deny" ? decision : "none");
            Closed?.Invoke();
        }

        public void Cancel()
        {
            Resolve("none");
        }
    }
}
