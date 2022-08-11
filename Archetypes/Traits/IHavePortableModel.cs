using Meep.Tech.XBam.Configuration;

namespace Meep.Tech.XBam.IO.JsonPorting {

  /// <summary>
  /// Indicates this archetype has built in instructions for creating a model porter.
  /// </summary>
  public interface IHavePortableModel<TModel> 
    : ITrait<IHavePortableModel<TModel>>,
      IFactory
    where TModel : class, IUnique
  {

    /// <summary>
    /// Used to create a model impoerter for the given type.
    /// </summary>
    /// <returns></returns>
    protected internal ModelPorter<TModel> CreateModelPorter();

    string ITrait<IHavePortableModel<TModel>>.TraitName
      => $"Has Model Import Settings";

    string ITrait<IHavePortableModel<TModel>>.TraitDescription
      => $"This Archetype tree provides instructions on how to import the model type: {typeof(TModel).FullName}";
  }
}
