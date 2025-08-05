using Microsoft.Extensions.DependencyInjection;

namespace DrugSearcher.Configuration;

public static class ContainerAccessor
{
    private static IServiceProvider? _container;

    public static IServiceProvider Container =>
        _container ?? throw new InvalidOperationException("Container not initialized");

    public static bool IsInitialized => _container != null;

    public static void Initialize(IServiceProvider container) =>
        _container = container;

    public static T Resolve<T>() where T : notnull =>
        Container.GetRequiredService<T>();

    public static void Dispose()
    {
        if (_container is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _container = null;
    }
}