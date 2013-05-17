using System;
using System.IO;
using System.ComponentModel;
using PluginCore.Localization;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using PluginCore;
using System.Text.RegularExpressions;

namespace TypeScriptContext
{
    public class PluginMain : IPlugin, InstalledSDKOwner
    {
        private String pluginName = "TypeScriptContext";
        private String pluginGuid = "1bbb3199-c159-4192-a1c5-97618ffe8636";
        private String pluginHelp = "www.flashdevelop.org/community/";
        private String pluginDesc = "TypeScript context for the ASCompletion engine.";
        private String pluginAuth = "FlashDevelop Team";
        private TypeScriptSettings settingObject;
        private Context contextInstance;
        private String settingFilename;

        #region Required Properties
        
        /// <summary>
        /// Api level of the plugin
        /// </summary>
        public Int32 Api
        {
            get { return 1; }
        }

        /// <summary>
        /// Name of the plugin
        /// </summary>
        public String Name
        {
            get { return this.pluginName; }
        }

        /// <summary>
        /// GUID of the plugin
        /// </summary>
        public String Guid
        {
            get { return this.pluginGuid; }
        }

        /// <summary>
        /// Author of the plugin
        /// </summary>
        public String Author
        {
            get { return this.pluginAuth; }
        }

        /// <summary>
        /// Description of the plugin
        /// </summary>
        public String Description
        {
            get { return this.pluginDesc; }
        }

        /// <summary>
        /// Web address for help
        /// </summary>
        public String Help
        {
            get { return this.pluginHelp; }
        }

        /// <summary>
        /// Object that contains the settings
        /// </summary>
        [Browsable(false)]
        public Object Settings
        {
            get { return this.settingObject; }
        }

        #endregion

        #region Required Methods

        /// <summary>
        /// Initializes the plugin
        /// </summary>
        public void Initialize()
        {
            this.InitBasics();
            this.LoadSettings();
            this.AddEventHandlers();
        }

        /// <summary>
        /// Disposes the plugin
        /// </summary>
        public void Dispose()
        {
            this.SaveSettings();
            if (Context.TemporaryOutputFile != null && File.Exists(Context.TemporaryOutputFile))
            {
                File.Delete(Context.TemporaryOutputFile);
            }
        }

        /// <summary>
        /// Handles the incoming events
        /// </summary>
        public void HandleEvent(Object sender, NotifyEvent e, HandlingPriority prority)
        {
            switch (e.Type)
            {
                case EventType.Command:
                    DataEvent de = e as DataEvent;
                    if (de == null) return;
                    if (de.Action == "ProjectManager.RunCustomCommand")
                    {
                    }
                    else if (de.Action == "ProjectManager.BuildingProject" || de.Action == "ProjectManager.TestingProject")
                    {
                        var completionHandler = contextInstance.completionModeHandler;
                        //if (completionHandler != null && !completionHandler.IsRunning())
                            //completionHandler.StartServer();
                    }
                    else if (de.Action == "ProjectManager.Project")
                    {
                    }
                    break;

                case EventType.UIStarted:
                    ValidateSettings();
                    contextInstance = new Context(settingObject);
                    // Associate this context with haXe language
                    ASCompletion.Context.ASContext.RegisterLanguage(contextInstance, "typescript");
                    break;
                case EventType.FileSwitch:
                    if (contextInstance != null)
                        contextInstance.OnFileSwitch();
                    break;
                case EventType.FileOpen:
                    var te = e as TextEvent;
                    if (contextInstance != null)
                        contextInstance.OnFileSwitch(te.Value);
                    break;
                case EventType.FileSave:
                    contextInstance.completionModeHandler.Reload();
                    break;
            }
        }

        #endregion

        #region Custom Methods

        /// <summary>
        /// Initializes important variables
        /// </summary>
        public void InitBasics()
        {
            String dataPath = Path.Combine(PathHelper.DataDir, "TypeScriptContext");
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
            this.settingFilename = Path.Combine(dataPath, "Settings.fdb");
            this.pluginDesc = TextHelper.GetString("Info.Description");
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary>
        public void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.UIStarted | EventType.Command | EventType.FileSwitch | EventType.FileSave | EventType.FileOpen);
        }

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        public void LoadSettings()
        {
            this.settingObject = new TypeScriptSettings();
            if (!File.Exists(this.settingFilename)) this.SaveSettings();
            else
            {
                Object obj = ObjectSerializer.Deserialize(this.settingFilename, this.settingObject);
                this.settingObject = (TypeScriptSettings)obj;
            }
        }

        /// <summary>
        /// Fix some settings values when the context has been created
        /// </summary>
        private void ValidateSettings()
        {
            if (settingObject.InstalledSDKs == null || settingObject.InstalledSDKs.Length == 0)
            {
                string includedSDK = System.Environment.GetEnvironmentVariable("HAXEPATH");
                if (includedSDK == null)
                {
                    string programFiles = System.Environment.GetEnvironmentVariable("ProgramFiles");
                    if (Directory.Exists(Path.Combine(programFiles, @"Motion-Twin\haxe")))
                        includedSDK = Path.Combine(programFiles, @"Motion-Twin\haxe");
                    else if (Directory.Exists(@"C:\Motion-Twin\haxe")) includedSDK = @"C:\Motion-Twin\haxe";
                }
                if (includedSDK != null)
                {
                    InstalledSDK sdk = new InstalledSDK(this);
                    sdk.Path = includedSDK;
                    settingObject.InstalledSDKs = new InstalledSDK[] { sdk };
                }
            }
            else foreach (InstalledSDK sdk in settingObject.InstalledSDKs) ValidateSDK(sdk);

            settingObject.OnTSSPathChanged += SettingObjectOnTSSPathChanged;
        }

        /// <summary>
        /// Reload TSS if an important setting has changed
        /// </summary>
        private void SettingObjectOnTSSPathChanged()
        {
            if (contextInstance != null)
                contextInstance.OnTSSPathChange();
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        private void SaveSettings()
        {
            ObjectSerializer.Serialize(this.settingFilename, this.settingObject);
        }

        #endregion

        #region InstalledSDKOwner Membres

        public bool ValidateSDK(InstalledSDK sdk)
        {
            sdk.Owner = this;

            IProject project = PluginBase.CurrentProject;
            string path = sdk.Path;
            if (project != null)
                path = PathHelper.ResolvePath(path, Path.GetDirectoryName(project.ProjectPath));
            else
                path = PathHelper.ResolvePath(path);
            
            try
            {
                if (path == null || !Directory.Exists(path))
                {
                    //ErrorManager.ShowInfo("Path not found:\n" + sdk.Path);
                    return false;
                }
            }
            catch (Exception ex)
            {
                ErrorManager.ShowInfo("Invalid path (" + ex.Message + "):\n" + sdk.Path);
                return false;
            }

            string[] lookup = new string[] {
                Path.Combine(path, "CHANGES.txt"),
                Path.Combine(path, Path.Combine("doc", "CHANGES.txt"))
            };
            string descriptor = null;
            foreach(string p in lookup) 
                if (File.Exists(p)) {
                    descriptor = p;
                    break;
                }
            if (descriptor != null)
            {
                string raw = File.ReadAllText(descriptor);
                Match mVer = Regex.Match(raw, "[0-9\\-]+\\s*:\\s*([0-9.]+)");
                if (mVer.Success)
                {
                    sdk.Version = mVer.Groups[1].Value;
                    sdk.Name = "Haxe " + sdk.Version;
                    return true;
                }
                else ErrorManager.ShowInfo("Invalid changes.txt file:\n" + descriptor);
            }
            else ErrorManager.ShowInfo("No change.txt found:\n" + descriptor);
            return false;
        }

        #endregion
    
    }

}
