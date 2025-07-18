using Autofac;

namespace DrugSearcher.Configuration;

public static class ContainerAccessor
{
    private static IContainer? _container;

    public static IContainer Container => _container ?? throw new InvalidOperationException("Container not initialized");

    public static bool IsInitialized => _container != null;

    public static void Initialize(IContainer container) => _container = container;

    public static T Resolve<T>() where T : notnull => Container.Resolve<T>();

    public static void Dispose()
    {
        _container?.Dispose();
        _container = null;
    }
}