using Meep.Tech.XBam.IO.JsonPorting.Configuration;

namespace Meep.Tech.XBam.IO.JsonPorting {

  /// <summary>
  /// Helpers to get mods and resources from the universe
  /// </summary>
  public static class ModelIOExtensions {

    /// <summary>
    /// Get the full mod by key from the universe.
    /// </summary>
    public static IModelPorter GetModelPorter<TModel>(this Universe universe) where TModel : class, IUnique
      => universe.GetExtraContext<ModelJsonPorterContext>()
        .GetPorter<TModel>();

    /// <summary>
    /// Get the full mod by key from the universe.
    /// </summary>
    public static IModelPorter GetModelPorter(this Universe universe, System.Type modelType)
      => universe.GetExtraContext<ModelJsonPorterContext>()
        .GetPorter(modelType);
  }
}
