using System;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using System.ComponentModel;
using PluginCore.Localization;
using PluginCore;

namespace TypeScriptContext
{
    public delegate void ClasspathChangedEvent();
    public delegate void CompletionModeChangedEventHandler();

    [Serializable]
    public class TypeScriptSettings : ASCompletion.Settings.IContextSettings
    {
        [field: NonSerialized]
        public event ClasspathChangedEvent OnTSSPathChanged;

        #region IContextSettings Documentation

        const string DEFAULT_DOC_COMMAND = "http://www.google.com/search?q=$(ItmTypPkg)+$(ItmTypName)+$(ItmName)+site:http://haxe.org/api";
        protected string documentationCommandLine = DEFAULT_DOC_COMMAND;

        [Browsable(false)]
        [DisplayName("Documentation Command Line")]
        [LocalizedCategory("ASCompletion.Category.Documentation"), LocalizedDescription("ASCompletion.Description.DocumentationCommandLine"), DefaultValue(DEFAULT_DOC_COMMAND)]
        public string DocumentationCommandLine
        {
            get { return documentationCommandLine; }
            set { documentationCommandLine = value; }
        }

        #endregion

        #region IContextSettings Members

        const bool DEFAULT_CHECKSYNTAX = false;
        const bool DEFAULT_COMPLETIONENABLED = true;
        const bool DEFAULT_GENERATEIMPORTS = true;
        const bool DEFAULT_PLAY = true;
        const bool DEFAULT_LAZYMODE = false;
        const bool DEFAULT_LISTALL = true;
        const bool DEFAULT_QUALIFY = true;
        const bool DEFAULT_FIXPACKAGEAUTOMATICALLY = true;

        protected bool checkSyntaxOnSave = DEFAULT_CHECKSYNTAX;
        private bool lazyClasspathExploration = DEFAULT_LAZYMODE;
        protected bool completionListAllTypes = DEFAULT_LISTALL;
        protected bool completionShowQualifiedTypes = DEFAULT_QUALIFY;
        protected bool completionEnabled = DEFAULT_COMPLETIONENABLED;
        protected bool generateImports = DEFAULT_GENERATEIMPORTS;
        protected bool playAfterBuild = DEFAULT_PLAY;
        protected bool fixPackageAutomatically = DEFAULT_FIXPACKAGEAUTOMATICALLY;
        protected string[] userClasspath = null;
        protected InstalledSDK[] installedSDKs = null;

        [Browsable(false)]
        public string LanguageId
        {
            get { return "TS"; }
        }

        [Browsable(false)]
        public string DefaultExtension
        {
            get { return ".ts"; }
        }

        [Browsable(false)]
        public string CheckSyntaxRunning
        {
            get { return TextHelper.GetString("Info.HaXeRunning"); }
        }

        [Browsable(false)]
        public string CheckSyntaxDone
        {
            get { return TextHelper.GetString("Info.HaXeDone"); }
        }

        [Browsable(false)]
        [DisplayName("Check Syntax On Save")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.CheckSyntaxOnSave"), DefaultValue(DEFAULT_CHECKSYNTAX)]
        public bool CheckSyntaxOnSave
        {
            get { return checkSyntaxOnSave; }
            set { checkSyntaxOnSave = value; }
        }

        [Browsable(false)]
        [DisplayName("User Classpath")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.UserClasspath")]
        public string[] UserClasspath
        {
            get { return userClasspath; }
            set
            {
                userClasspath = value;
                FireChanged();
            }
        }

        [Browsable(false)]
        [DisplayName("Installed Haxe SDKs")]
        [LocalizedCategory("ASCompletion.Category.Language"), LocalizedDescription("HaXeContext.Description.HaXePath")]
        public InstalledSDK[] InstalledSDKs
        {
            get { return installedSDKs; }
            set
            {
                installedSDKs = value;
                FireChanged();
            }
        }

        [Browsable(false)]
        public InstalledSDK GetDefaultSDK()
        {
            if (installedSDKs == null || installedSDKs.Length == 0)
                return InstalledSDK.INVALID_SDK;

            foreach (InstalledSDK sdk in installedSDKs)
                if (sdk.IsValid) return sdk;
            return InstalledSDK.INVALID_SDK;
        }

        [Browsable(false)]
        [DisplayName("Enable Completion")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.CompletionEnabled"), DefaultValue(DEFAULT_COMPLETIONENABLED)]
        public bool CompletionEnabled
        {
            get { return completionEnabled; }
            set { completionEnabled = value; }
        }

        [Browsable(false)]
        [DisplayName("Generate Imports")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.GenerateImports"), DefaultValue(DEFAULT_GENERATEIMPORTS)]
        public bool GenerateImports
        {
            get { return generateImports; }
            set { generateImports = value; }
        }

        /// <summary>
        /// In completion, show all known types in project
        /// </summary>
        [Browsable(false)]
        [DisplayName("List All Types In Completion")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.CompletionListAllTypes"), DefaultValue(DEFAULT_LISTALL)]
        public bool CompletionListAllTypes
        {
            get { return completionListAllTypes; }
            set { completionListAllTypes = value; }
        }

        /// <summary>
        /// In completion, show qualified type names (package + type)
        /// </summary>
        [Browsable(false)]
        [DisplayName("Show QualifiedTypes In Completion")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.CompletionShowQualifiedTypes"), DefaultValue(DEFAULT_QUALIFY)]
        public bool CompletionShowQualifiedTypes
        {
            get { return completionShowQualifiedTypes; }
            set { completionShowQualifiedTypes = value; }
        }

        /// <summary>
        /// Defines if each classpath is explored immediately (PathExplorer) 
        /// </summary>
        [Browsable(false)]
        [DisplayName("Lazy Classpath Exploration")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.LazyClasspathExploration"), DefaultValue(DEFAULT_LAZYMODE)]
        public bool LazyClasspathExploration
        {
            get { return lazyClasspathExploration; }
            set { lazyClasspathExploration = value; }
        }

        [Browsable(false)]
        [DisplayName("Play After Build")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.PlayAfterBuild"), DefaultValue(DEFAULT_PLAY)]
        public bool PlayAfterBuild
        {
            get { return playAfterBuild; }
            set { playAfterBuild = value; }
        }

        [Browsable(false)]
        [DisplayName("Fix Package Automatically")]
        [LocalizedCategory("ASCompletion.Category.Common"), LocalizedDescription("ASCompletion.Description.FixPackageAutomatically"), DefaultValue(DEFAULT_FIXPACKAGEAUTOMATICALLY)]
        public bool FixPackageAutomatically
        {
            get { return fixPackageAutomatically; }
            set { fixPackageAutomatically = value; }
        }

        #endregion

        #region TypeScript specific members

        const string DEFAULT_TSS_PATH = @"Tools\tstools\tss.js";
        const string DEFAULT_NODE_PATH = "node.exe";

        const bool DEFAULT_DISABLEMIXEDCOMPLETION = false;
        const bool DEFAULT_DISABLECOMPLETIONONDEMAND = true;

        private string tssPath = DEFAULT_TSS_PATH;
        private string nodePath = DEFAULT_NODE_PATH;

        private bool disableMixedCompletion = DEFAULT_DISABLEMIXEDCOMPLETION;
        private bool disableCompletionOnDemand = DEFAULT_DISABLECOMPLETIONONDEMAND;

        [DisplayName("TSS Path")]
        [LocalizedCategory("ASCompletion.Category.Language"), LocalizedDescription("TypeScriptContext.Description.TSSPath"), DefaultValue(DEFAULT_TSS_PATH)]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string TSSPath
        {
            get { return tssPath; }
            set
            {
                tssPath = value;
                FireChanged();
            }
        }
        [DisplayName("Node.js Path")]
        [LocalizedCategory("ASCompletion.Category.Language"), LocalizedDescription("TypeScriptContext.Description.NodePath"), DefaultValue(DEFAULT_NODE_PATH)]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string NodePath
        {
            get { return nodePath; }
            set { nodePath = value; }
        }

        [Browsable(false)]
        [DisplayName("Disable Mixed Completion")]
        [LocalizedCategory("ASCompletion.Category.Language"), LocalizedDescription("HaXeContext.Description.DisableMixedCompletion"), DefaultValue(DEFAULT_DISABLEMIXEDCOMPLETION)]
        public bool DisableMixedCompletion
        {
            get { return disableMixedCompletion; }
            set { disableMixedCompletion = value; }
        }

        [DisplayName("Disable Completion On Demand")]
        [Browsable(false)]
        [LocalizedCategory("ASCompletion.Category.Language"), LocalizedDescription("HaXeContext.Description.DisableCompletionOnDemand"), DefaultValue(DEFAULT_DISABLECOMPLETIONONDEMAND)]
        public bool DisableCompletionOnDemand
        {
            get { return disableCompletionOnDemand; }
            set { disableCompletionOnDemand = value; }
        }

        #endregion

        [Browsable(false)]
        private void FireChanged()
        {
            if (OnTSSPathChanged != null) OnTSSPathChanged();
        }
    }
}
