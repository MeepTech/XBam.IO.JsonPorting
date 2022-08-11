using Meep.Tech.XBam.IO.JsonPorting.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meep.Tech.XBam.IO.JsonPorting {

  /// <summary>
  /// Used to in and export models to the data folders.
  /// </summary>
  public partial interface IModelPorter {

    /// <summary>
    /// The sub folder in the data folder that contains this type of model.
    /// ex: '_items'
    /// </summary>
    string ModelSaveToSubFolderName { 
      get;
    }

    /// <summary>
    /// The universe this is for
    /// </summary>
    Universe Universe {
      get;
      internal protected set;
    }

    /// <summary>
    /// The base type of the model
    /// </summary>
    System.Type ModelBaseType {
      get;
    }

    /// <summary>
    /// Get the data folder for the given unique model
    /// </summary>
    string GetDataFolder(string uniqueModelId, Dictionary<string, object> context = null);

    /// <summary>
    /// Get the metadata for the given unique model
    /// </summary>
    Metadata GetMetadata(string uniqueModelId, Dictionary<string, object> context = null);

    /// <summary>
    /// Get the metadata for all models of this type
    /// </summary>
    IEnumerable<Metadata> GetAllMetadatas(string fromRootFolderLocation = null);

    /// <summary>
    /// Serialize save data of this model to the file system and cache.
    /// </summary>
    void Save(IUnique model);

    /// <summary>
    /// Delete the given model from save data
    /// </summary>
    void Delete(string uniqueModelId, Dictionary<string, object> context = null, bool removeFromCache = true);

    /// <summary>
    /// Check if a model exists in serialized save data.
    /// Can also be used to check if it's just in the cache already.
    /// </summary>
    bool Exists(string uniqueModelId, Dictionary<string, object> context = null, bool checkCache = false);

    /// <summary>
    /// Load the model from memory or cache by unique id.
    /// This throws an exception if the model is not found.
    /// </summary>
    IUnique Load(string uniqueModelId, Dictionary<string, object> context = null, bool skipCache = false);

    /// <summary>
    /// Try to load the model from memory or cache by unique id.
    /// </summary>
    bool TryToLoad(string uniqueModelId, out IUnique model, Dictionary<string, object> context = null, bool skipCache = false);
  }

  /// <summary>
  /// Used to in and export models to the data folders.
  /// </summary>
  public class ModelPorter<TModel>
    : IModelPorter
    where TModel : class, IUnique {
    bool? _isCacheable;
    Universe IModelPorter.Universe { get => Universe; set => Universe = value; }

    /// <summary>
    /// The base type this is in charge of porting.
    /// </summary>
    public Type ModelBaseType
      => typeof(TModel);

    /// <summary>
    /// If this is working with a cacheable model
    /// </summary>
    public bool IsCacheable
      => _isCacheable ??= typeof(ICached).IsAssignableFrom(typeof(TModel));

    ///<summary><inheritdoc/></summary>
    Universe Universe { get; set; }

    ///<summary><inheritdoc/></summary>
    public virtual string ModelSaveToSubFolderName {
      get;
      init;
    } = "_" + typeof(TModel).Name.ToLower();

    /// <summary>
    /// Used to get the path within the /data/ folder that the /ModelSaveToSubFolderName/ folder should be placed at.
    /// The input is extra context provided by the user or from GetFolderLocationContextFromModel, and may be null
    /// EX: if = '/tests/models/': 
    ///     path to folder = '/data/tests/models/ModelSaveToSubFolderName/'
    /// </summary>
    public virtual Func<Dictionary<string, object>, string> GetPreModelFolderPath {
      get;
      init;
    } = null;

    /// <summary>
    /// Used to get the path within the /ModelSaveToSubFolderName/ folder that the folder for this model should be placed at.
    /// The input is extra context provided by the user or from GetFolderLocationContextFromModel, and may be null
    /// EX: if = '/tests/models/': 
    ///     path to folder = '/data/ModelSaveToSubFolderName/tests/models/CurrentModelsFolder/'
    /// </summary>
    public virtual Func<Dictionary<string, object>, string> GetPostModelFolderPath {
      get;
      init;
    } = null;

    /// <summary>
    /// Used to auto gen context from a model for helping with GetPreModelFolderPath and GetPostModelFolderPath.
    /// </summary>
    public virtual Func<TModel, Dictionary<string, object>> GetFolderLocationContextFromModel {
      get;
      init;
    } = null;

    /// <summary>
    /// Used to get the file name for items ported by this file. This name is used in the metadata.
    /// </summary>
    public virtual Func<TModel, string> GetMainSaveDataFileName {
      get;
      init;
    } = model => model.Id;

    /// <summary>
    /// The type of metadata to produce for these models
    /// </summary>
    public virtual System.Type MetadataType {
      get;
      init;
    } = typeof(IModelPorter.Metadata);

    #region IO

    /// <summary>
    /// Get the data folder for the given unique model
    /// </summary>
    public string GetDataFolder(TModel model)
      => GetDataFolder(model.Id, GetFolderLocationContextFromModel?.Invoke(model));

    /// <summary>
    /// Get the data folder for the given unique model
    /// </summary>
    public virtual string GetDataFolder(string uniqueModelId, Dictionary<string, object> context = null)
      => Path.Combine(
        Universe.GetExtraContext<ModelJsonPorterContext>().RootDataFolder,
        GetPreModelFolderPath?.Invoke(context),
        ModelSaveToSubFolderName,
        GetPostModelFolderPath?.Invoke(context),
        uniqueModelId
      );

    string IModelPorter.GetDataFolder(string uniqueModelId, Dictionary<string, object> context)
      => GetDataFolder(uniqueModelId, context);

    /// <summary>
    /// Get the metadata for the given unique model
    /// </summary>
    public IModelPorter.Metadata GetMetadata(TModel model)
      => GetMetadata(model.Id, GetFolderLocationContextFromModel?.Invoke(model));

    /// <summary>
    /// Get the metadata for the given unique model
    /// </summary>
    public IModelPorter.Metadata GetMetadata(string uniqueModelId, Dictionary<string, object> context = null)
      => GetMetadataFromFolder(GetDataFolder(uniqueModelId, context));

    /// <summary>
    /// Get the metadata for the given unique model
    /// </summary>
    protected virtual IModelPorter.Metadata GetMetadataFromFolder(string folderLocation) {
      if (MetadataType == typeof(IModelPorter.Metadata)) {
        return new IModelPorter.Metadata(
          Directory.EnumerateFiles(folderLocation).First(f => f.EndsWith(IModelPorter.Metadata.MainDataFileExtension)),
          folderLocation,
          Directory.GetLastWriteTime(folderLocation),
          typeof(TModel),
          Universe
        );
      }
      else {
        return (IModelPorter.Metadata)Activator.CreateInstance(
          MetadataType,
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
          null,
          new object[] {
            Directory.EnumerateFiles(folderLocation).First(f => f.EndsWith(IModelPorter.Metadata.MainDataFileExtension)),
            folderLocation,
            Directory.GetLastWriteTime(folderLocation),
            typeof(TModel),
            Universe
          },
          null
        );
      }
    }

    /// <summary>
    /// Get the metadata for all models of this type
    /// </summary>
    public virtual IEnumerable<IModelPorter.Metadata> GetAllMetadatas(string fromSubFolderLocation = null) {
      string rootDataLocation = Universe.GetExtraContext<ModelJsonPorterContext>().RootDataFolder;
      if (fromSubFolderLocation is not null) {
        rootDataLocation = Path.Combine(rootDataLocation, fromSubFolderLocation);
      }

      foreach(var modelSubFolder in Directory.EnumerateDirectories(rootDataLocation, ModelSaveToSubFolderName)) {
        foreach (var modelMainDataFile in Directory.EnumerateFiles(rootDataLocation, "*" + IModelPorter.Metadata.MainDataFileExtension)) {
          yield return GetMetadataFromFolder(Path.GetDirectoryName(modelMainDataFile));
        }
      }
    }

    /// <summary>
    /// Serialize save data of this model to the file system and cache.
    /// </summary>
    public void Save(TModel model) {
      SaveToFolder(
        model,
        GetDataFolder(model),
        GetMainSaveDataFileName(model) + IModelPorter.Metadata.MainDataFileExtension
      );

      if (IsCacheable && model is ICached cached) {
        ICached.Set(cached);
      }
    }

    /// <summary>
    /// Serialize save data of this model to the file system and cache.
    /// </summary>
    public virtual void SaveToFolder(TModel model, string saveToFolder, string saveToFileName) {
      throw new NotImplementedException();
    }

    void IModelPorter.Save(IUnique model)
      => Save((TModel)model);

    /// <summary>
    /// Load the model from memory or cache by unique id.
    /// This throws an exception if the model is not found.
    /// </summary>
    public TModel Load(string uniqueModelId, Dictionary<string, object> context = null, bool skipCache = false) {
      if (IsCacheable && !skipCache) {
        TModel cachedItem = ICached.GetFromCache(uniqueModelId) as TModel;
        if (cachedItem is not null) {
          return cachedItem;
        }
      }

      return LoadFromFolder(GetDataFolder(uniqueModelId, context));
    }

    /// <summary>
    /// Load the item from the data folder.
    /// </summary>
    protected virtual TModel LoadFromFolder(string folderLocation) {
      throw new NotImplementedException();
    }

    IUnique IModelPorter.Load(string uniqueModelId, Dictionary<string, object> context, bool skipCache)
      => Load(uniqueModelId, context, skipCache);

    /// <summary>
    /// Try to load the model from memory or cache by unique id.
    /// </summary>
    public virtual bool TryToLoad(string uniqueModelId, out TModel model, Dictionary<string, object> context = null, bool skipCache = false) {
      throw new NotImplementedException();
    }

    bool IModelPorter.TryToLoad(string uniqueModelId, out IUnique model, Dictionary<string, object> context, bool skipCache)
      => TryToLoad(uniqueModelId, out var found, context, skipCache)
        ? (model = found) != null
        : (model = null) != null;

    /// <summary>
    /// Check if a model exists in serialized save data.
    /// Can also be used to check if it's just in the cache already.
    /// </summary>
    public bool Exists(TModel model, bool checkCache = false)
      => Exists(model.Id, GetFolderLocationContextFromModel?.Invoke(model), checkCache);

    /// <summary>
    /// Check if a model exists in serialized save data.
    /// Can also be used to check if it's just in the cache already.
    /// </summary>
    public virtual bool Exists(string uniqueModelId, Dictionary<string, object> context = null, bool checkCache = false) {
      if (IsCacheable && checkCache) {
        if ((ICached.TryToGetFromCache(uniqueModelId) as TModel) != null) {
          return true;
        }
      }

      var saveToFolder = GetDataFolder(uniqueModelId, context);
      if (Directory.Exists(saveToFolder)) {
        if (Directory.EnumerateFiles(saveToFolder).Any(f => f.EndsWith(IModelPorter.Metadata.MainDataFileExtension))) {
          return true;
        }
      }

      return false;
    }

    bool IModelPorter.Exists(string uniqueModelId, Dictionary<string, object> context, bool checkCache)
      => Exists(uniqueModelId, context, checkCache);

    /// <summary>
    /// Delete the given model from save data
    /// </summary>
    public virtual void Delete(string uniqueModelId, Dictionary<string, object> context = null, bool removeFromCache = true) {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Delete the given model from save data
    /// </summary>
    public bool Delete(string uniqueModelId, out TModel deleted, Dictionary<string, object> context = null, bool removeFromCache = true) {
      if (Exists(uniqueModelId)) {
        deleted = Load(uniqueModelId);
        Delete(uniqueModelId, context, removeFromCache);
        return true;
      }

      deleted = null;
      return false;
    }

    /// <summary>
    /// Delete the given model from save data
    /// </summary>
    public void Delete(TModel model, Dictionary<string, object> context, bool removeFromCache)
      => Delete(model.Id, context, removeFromCache);

    #endregion
  }
}
