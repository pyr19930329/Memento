namespace Memento.MCP.Services;

/// <summary>全局异常处理 — 捕获未处理的异常并打印友好信息</summary>
public static class GlobalException
{
    /// <summary>注册全局异常处理器，在 Program.cs 启动时调用一次</summary>
    public static void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Console.Error.WriteLine($"[FATAL] 未捕获的异常: {ex?.GetType().Name}");
            Console.Error.WriteLine($"  Message: {ex?.Message}");
            Console.Error.WriteLine($"  StackTrace: {ex?.StackTrace}");
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Console.Error.WriteLine($"[FATAL] 异步任务异常: {args.Exception?.GetType().Name}");
            Console.Error.WriteLine($"  Message: {args.Exception?.Message}");
            Console.Error.WriteLine($"  StackTrace: {args.Exception?.StackTrace}");
            args.SetObserved();
        };
    }
}
