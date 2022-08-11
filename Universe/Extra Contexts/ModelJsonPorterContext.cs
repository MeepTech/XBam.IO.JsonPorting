using Meep.Tech.Collections.Generic;
using Meep.Tech.XBam.Configuration;
using Meep.Tech.XBam.IO.Configuration;
using Meep.Tech.XBam.Reflection;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Meep.Tech.XBam.IO.JsonPorting.Configuration {

  /// <summary>
  /// An override for ModelIOContext that enables json importing and exporting to the file system.
  /// </summary>
  public class ModelJsonPorterContext : ModelIOContext {
    readonly string _rootApplicationPersistentDataFolder;
    readonly Dictionary<System.Type, IModelPorter> _portersByBaseModelType
      = new();
    readonly Dictionary<System.Type, IModelPorter> _portersByModelType
      = new();

    /// <summary>
    /// The default folder to save model data to
    /// </summary>
    public const string DataFolderName
      = "data";

    /// <summary>
    /// The root data folder, where in game models are saved to
    /// </summary>
    public string RootDataFolder {
      get;
    }

    /// <summary>
    /// Make a new model import context
    /// </summary>
    public ModelJsonPorterContext([NotNull] string rootApplicationPersistentDataFolder, IEnumerable<IModelPorter> modelPorters = null) {
      _rootApplicationPersistentDataFolder = rootApplicationPersistentDataFolder;
      modelPorters?.ForEach(p => {
        p.Universe = Universe;
        _portersByBaseModelType[p.ModelBaseType] = p;
      });

      RootDataFolder = Path.Combine(rootApplicationPersistentDataFolder, DataFolderName);
    }

    protected override bool TryToFetchModelByTypeAndId(Type type, string uniqueId, out IUnique? model, out Exception? error) {
      if(Universe.GetExtraContext<ModelJsonPorterContext>().TryToGetPorter(type, out var porter)) {
        if (porter.TryToLoad(uniqueId, out model)) {
          error = null;
          return true;
        } else {
          model = null;
          error = new KeyNotFoundException($"Could not find model of type: {type.FullName}, with id: {uniqueId}, not found.");
        }
      } else {
        model = null;
        error = new InvalidOperationException($"Type: {type.FullName} does not have a porter for importing and exporting data.");
      }

      return false;
    }

    #region Porter Access

    /// <summary>
    /// All of the model porters
    /// </summary>
    public IEnumerable<IModelPorter> Porters
      => _portersByBaseModelType.Values;

    /// <summary>
    /// Get a porter for a model type.
    /// Throws if one is not found.
    /// </summary>
    public ModelPorter<TModel> GetPorter<TModel>() where TModel : class, IUnique
      => (ModelPorter<TModel>)GetPorter(typeof(TModel));

    /// <summary>
    /// Get a porter for a model type.
    /// Throws if one is not found.
    /// </summary>
    public IModelPorter GetPorter(Type modelType) {
      if (_portersByModelType.TryGetValue(modelType, out var cachedPorter)) {
        return cachedPorter;
      }
      else {
        var found =
          _portersByBaseModelType
            .First(
              p => p.Key.IsAssignableFrom(modelType)
            ).Value;

        _portersByModelType[modelType] = found;
        return found;
      }
    }

    /// <summary>
    /// Try to get a porter for a model type.
    /// </summary>
    public bool TryToGetPorter<TModel>(Type modelType, out ModelPorter<TModel> porter) where TModel : class, IUnique
      => TryToGetPorter(modelType, out IModelPorter found)
        ? (porter = found as ModelPorter<TModel>) is not null
        : (porter = null) is not null;

    /// <summary>
    /// Try to get a porter for a model type.
    /// </summary>
    public bool TryToGetPorter(Type modelType, out IModelPorter porter) {
      if (_portersByModelType.TryGetValue(modelType, out var cachedPorter)) {
        porter = cachedPorter;
        return true;
      }
      else {
        porter =
          _portersByBaseModelType
            .FirstOrDefault(
              p => p.Key.IsAssignableFrom(modelType)
            ).Value;

        if (porter != null) {
          _portersByModelType[modelType] = porter;
          return true;
        }

        return false;
      }
    }

    #endregion

    /// <summary>
    /// set up the initial loader configuation settings
    /// </summary>
    protected override Action<Loader> OnLoaderInitializationStart => loader => {
      loader.Options.DataFolderParentFolderLocation = _rootApplicationPersistentDataFolder;
    };

    /// <summary>
    /// Grab any porters declared in code
    /// </summary>
    protected override Action<bool, Type, Archetype, Exception, bool> OnLoaderArchetypeInitializationComplete
      => (success, type, archetype, error, isSplayed) => {
        if (success && !isSplayed) {
          foreach (System.Type iHavePorterInterface in type.GetAllInheritedGenericTypes(typeof(IHavePortableModel<>))) {
            var forModelType = iHavePorterInterface.GetGenericArguments().First();
            if (!_portersByBaseModelType.ContainsKey(forModelType)) {
              _portersByBaseModelType[forModelType] = (IModelPorter)iHavePorterInterface
                .GetMethod(
                  "CreateModelPorter",
                  System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
                ).Invoke(archetype, new object[0]);
            }
          }
        }
      };

    /// <summary>
    /// Add in the special converters logic
    /// </summary>
    protected override Action<MemberInfo, JsonProperty> OnLoaderModelJsonPropertyCreationComplete
      => (MemberInfo memberInfo, JsonProperty defaultJsonProperty) => {
        AutoPortAttribute attribute;
        if ((attribute = memberInfo.GetCustomAttribute<AutoPortAttribute>()) != null) {
          var itemType = memberInfo is FieldInfo f ? f.FieldType : memberInfo is PropertyInfo p ? p.PropertyType : null;
          defaultJsonProperty.Converter = typeof(IUnique).IsAssignableFrom(itemType)
            ? new PortableModelJsonConverter(Universe)
            : (itemType.IsAssignableToGeneric(typeof(IReadOnlyDictionary<,>))
              && typeof(IUnique).IsAssignableFrom(itemType.GetGenericArguments().Last())
              && (typeof(string) == itemType.GetGenericArguments().First())
            ) ? attribute.PreserveKeys
                ? (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(
                    typeof(PortableModelsDictionaryWithKeysJsonConverter<>).MakeGenericType(itemType.GetGenericArguments().Last()),
                    Universe)
                : (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(
                    typeof(PortableModelsDictionaryJsonConverter<>).MakeGenericType(itemType.GetGenericArguments().Last()),
                    Universe)
              : itemType.IsAssignableToGeneric(typeof(IEnumerable<>)) && typeof(IUnique).IsAssignableFrom(itemType.GetGenericArguments().First())
                ? (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(
                  typeof(PortableModelsCollectionJsonConverter<>).MakeGenericType(itemType.GetGenericArguments().First()),
                  Universe)
                : throw new InvalidOperationException($"{nameof(AutoPortAttribute)} only works on properties that inherit from IUnique, or IDictionary<string,IUnique>, or IEnumerable<IUnique>. {itemType.FullName} is an invalid type.");

          defaultJsonProperty.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        }
      };
  }
}
