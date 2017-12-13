namespace FuncAppEnd2EndMonitoring
{
    public static class CorrelationHelper
    {
        public static string GetOperationId(string requestId)
        {
            // Returns the root ID from the '|' to the first '.' if any.
            // Following the HTTP Protocol for Correlation - Hierarchical Request-Id schema is used
            // https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/HierarchicalRequestId.md
            int rootEnd = requestId.IndexOf('.');
            if (rootEnd < 0)
                rootEnd = requestId.Length;

            int rootStart = requestId[0] == '|' ? 1 : 0;
            return requestId.Substring(rootStart, rootEnd - rootStart);
        }
    }
}
