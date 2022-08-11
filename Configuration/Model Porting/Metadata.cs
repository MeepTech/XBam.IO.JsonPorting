using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Meep.Tech.XBam.IO.JsonPorting {
  public partial interface IModelPorter {

    /// <summary>
    /// Metadata for a portable model.
    /// </summary>
    public class Metadata {

      /// <summary>
      /// The file extension expexcted on the main data file for a model
      /// </summary>
      public const string MainDataFileExtension = ".data.json";

      /// <summary>
      /// The key for the data. 
      /// Taken from the folder name
      /// </summary>
      public string Key {
        get;
      }

      /// <summary>
      /// The location of the folder this model's data is saved to.
      /// </summary>
      public string Folder {
        get;
      }

      /// <summary>
      /// The name of the data item.
      /// Taken from the name of it's .data.json file.
      /// </summary>
      public string Name {
        get;
      }

      /// <summary>
      /// The universe
      /// </summary>
      public Universe Universe {
        get;
      }

      /// <summary>
      /// The type of model this is for
      /// </summary>
      public System.Type ModelType {
        get;
      }

      /// <summary>
      /// Lazy loaded link to the model
      /// </summary>
      public IUnique Model
        => _model ??= Universe.GetModelPorter(ModelType).Load(Key);
      IUnique _model;

      /// <summary>
      /// The location of the folder this model's data is saved to.
      /// </summary>
      public string MainDataFileLocation
        => _mainDataFileLocation ??= Path.Combine(Folder, MainDataFile);
      string _mainDataFileLocation;

      /// <summary>
      /// The location of the folder this model's data is saved to.
      /// </summary>
      public string MainDataFile
        => _mainDataFileName ??= Path.ChangeExtension(Name, MainDataFileExtension);
      string _mainDataFileName;

      /// <summary>
      /// When this was last updated.
      /// </summary>
      public DateTime LastUpdated {
        get;
      }

      /// <summary>
      /// Used to extend metadata
      /// </summary>
      protected internal Metadata(string modelItemName, string modelSaveToFolder, DateTime lastUpdated, Type modelType, Universe universe) {
        Universe = universe;
        Name = modelItemName;
        Folder = modelSaveToFolder;
        Key = Path.GetFileNameWithoutExtension(modelSaveToFolder);
        LastUpdated = lastUpdated;
        ModelType = modelType;  
      }

      /// <summary>
      /// Overrideable function to do other stuff on load.
      /// </summary>
      protected virtual void OnLoad(JObject json, string folder) { }
    }
  }
}
