using QnxPortScan.Core.Rules;

namespace QnxPortScan.Tests;

internal static class TestRules
{
    public static RuleCatalog Catalog { get; } = RuleCatalog.FromJson("""
        [
          { "id": "T-INC", "title": "linux headers", "kind": "Include", "severity": "Error",
            "patterns": ["linux/", "sys/epoll.h"], "message": "m", "suggestion": "s" },
          { "id": "T-CALL", "title": "epoll", "kind": "Call", "severity": "Error",
            "patterns": ["epoll_wait", "epoll_create1"], "message": "m", "suggestion": "s" },
          { "id": "T-ID", "title": "populate", "kind": "Identifier", "severity": "Warning",
            "patterns": ["MAP_POPULATE"], "message": "m", "suggestion": "s" },
          { "id": "T-LIT", "title": "procfs", "kind": "Literal", "severity": "Warning",
            "patterns": ["^/proc/self/maps$", "/proc/cpuinfo"], "message": "m", "suggestion": "s" }
        ]
        """);
}
