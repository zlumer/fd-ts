using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using PluginCore.Managers;
using ASCompletion.Context;
using ASCompletion.Model;
using PluginCore.Localization;
using PluginCore.Controls;
using PluginCore.Helpers;
using PluginCore;
using ASCompletion.Completion;
using AS3Context;

namespace TypeScriptContext
{
    public class Context : AS2Context.Context
    {
        #region initialization
        new static readonly protected Regex re_CMD_BuildCommand =
            new Regex("@tsc[\\s]+(?<params>.*)", RegexOptions.Compiled | RegexOptions.Multiline);

        static readonly protected Regex re_genericType =
                    new Regex("(?<gen>[^<]+)<(?<type>.+)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private TypeScriptSettings hxsettings;

        public Context(TypeScriptSettings initSettings)
        {
            hxsettings = initSettings;

            /* AS-LIKE OPTIONS */

            hasLevels = false;
            docType = "Void"; // "flash.display.MovieClip";

            /* DESCRIBE LANGUAGE FEATURES */

            // language constructs
            features.hasPackages = true;
            features.hasImports = true;
            features.hasImportsWildcard = false;
            features.hasClasses = true;
            features.hasExtends = true;
            features.hasImplements = true;
            features.hasInterfaces = true;
            features.hasEnums = true;
            features.hasTypeDefs = true;
            features.hasGenerics = true;
            features.hasEcmaTyping = true;
            features.hasVars = true;
            features.hasConsts = false;
            features.hasMethods = true;
            features.hasStatics = true;
            features.hasTryCatch = true;
            features.hasInference = true;
            features.checkFileName = false;

            // haxe directives
            features.hasDirectives = true;
            features.Directives = new List<string>();
            features.Directives.Add("true");

            // allowed declarations access modifiers
            Visibility all = Visibility.Public | Visibility.Private;
            features.classModifiers = all;
            features.varModifiers = all;
            features.methodModifiers = all;

            // default declarations access modifiers
            features.classModifierDefault = Visibility.Public;
            features.enumModifierDefault = Visibility.Public;
            features.typedefModifierDefault = Visibility.Public;
            features.varModifierDefault = Visibility.Private;
            features.methodModifierDefault = Visibility.Private;

            // keywords
            features.dot = ".";
            features.voidKey = "void";
            features.objectKey = "any";
            features.booleanKey = "bool";
            features.numberKey = "number";
            features.arrayKey = "T[]";
            features.importKey = "import";
            features.importKeyAlt = "using";
            features.typesPreKeys = new string[] { "import", "new", "extends", "implements", "using" };
            features.codeKeywords = new string[] { 
                "enum", "typedef", "class", "interface", "var", "function", "new", "cast", "return", "break", 
                "continue", "callback", "if", "else", "for", "while", "do", "switch", "case", "default", "type",
                "null", "untyped", "true", "false", "try", "catch", "throw", "inline", "dynamic",
                "extends", "using", "import", "implements"
            };
            features.varKey = "var";
            features.overrideKey = "override";
            features.functionKey = "function";
            features.staticKey = "static";
            features.publicKey = "public";
            features.privateKey = "private";
            features.intrinsicKey = "extern";
            features.inlineKey = "inline";

            /* INITIALIZATION */

            settings = initSettings;

            OnFileSwitch();
            //BuildClassPath(); // defered to first use
        }
        #endregion

        #region classpath management

        /// <summary>
        /// Classpathes & classes cache initialisation
        /// </summary>
        public override void BuildClassPath()
        {
            ReleaseClasspath();
            started = true;
            if (hxsettings == null) throw new Exception("BuildClassPath() must be overridden");
            if (contextSetup == null)
            {
                contextSetup = new ContextSetupInfos();
                contextSetup.Lang = settings.LanguageId;
                contextSetup.Platform = "Flash Player";
                contextSetup.Version = "10.0";
            }

            // external version definition
            platform = contextSetup.Platform;

            // NOTE: version > 10 for non-Flash platforms
            string lang;
            features.Directives = new List<string>();
            lang = platform;
            features.Directives.Add(lang);

            //
            // Class pathes
            //
            classPath = new List<PathModel>();
            // haXe std
            string hxPath = PluginBase.CurrentProject != null
                    ? PluginBase.CurrentProject.CurrentSDK
                    : PathHelper.ResolvePath(hxsettings.GetDefaultSDK().Path);
            if (hxPath != null)
            {
                string haxeCP = Path.Combine(hxPath, "std");
                if (Directory.Exists(haxeCP))
                {
                    PathModel std = PathModel.GetModel(haxeCP, this);
                    if (!std.WasExplored && !Settings.LazyClasspathExploration)
                    {
                        string[] keep = new string[] { "sys", "haxe", "libs" };
                        List<String> hide = new List<string>();
                        foreach (string dir in Directory.GetDirectories(haxeCP))
                            if (Array.IndexOf<string>(keep, Path.GetFileName(dir)) < 0)
                                hide.Add(Path.GetFileName(dir));
                        ManualExploration(std, hide);
                    }
                    AddPath(std);

                    if (!string.IsNullOrEmpty(lang))
                    {
                        PathModel specific = PathModel.GetModel(Path.Combine(haxeCP, lang), this);
                        if (!specific.WasExplored && !Settings.LazyClasspathExploration)
                        {
                            ManualExploration(specific, null);
                        }
                        AddPath(specific);
                    }
                }
            }
            else
                ;// OnCompletionModeChange();

            // add external pathes
            List<PathModel> initCP = classPath;
            classPath = new List<PathModel>();
            if (contextSetup.Classpath != null)
            {
                foreach (string cpath in contextSetup.Classpath)
                    AddPath(cpath.Trim());
            }

            // add user pathes from settings
            if (settings.UserClasspath != null && settings.UserClasspath.Length > 0)
            {
                foreach (string cpath in settings.UserClasspath) AddPath(cpath.Trim());
            }
            // add initial pathes
            foreach (PathModel mpath in initCP) AddPath(mpath);

            // parse top-level elements
            InitTopLevelElements();
            if (cFile != null) UpdateTopLevelElements();

            // add current temporaty path
            if (temporaryPath != null)
            {
                string tempPath = temporaryPath;
                temporaryPath = null;
                SetTemporaryPath(tempPath);
            }
            FinalizeClasspath();

            //if (completionModeHandler == null) 
                //OnCompletionModeChange();
        }

        override protected bool ExplorePath(PathModel path)
        {
            if (!path.WasExplored && !path.IsVirtual && !path.IsTemporaryPath)
            {
                string haxelib = Path.Combine(path.Path, "haxelib.xml");
                if (File.Exists(haxelib))
                {
                    string src = File.ReadAllText(haxelib);
                    if (src.IndexOf("<project name=\"nme\"") >= 0)
                    {
                        ManualExploration(path, new string[] { 
                            "js", "jeash", "neash", "flash", "neko", "tools", "samples", "project" });
                    }
                }
            }
            return base.ExplorePath(path);
        }

        /// <summary>
        /// Parse a packaged library file
        /// </summary>
        /// <param name="path">Models owner</param>
        public override void ExploreVirtualPath(PathModel path)
        {
            try
            {
                if (File.Exists(path.Path))
                {
                    SwfOp.ContentParser parser = new SwfOp.ContentParser(path.Path);
                    parser.Run();
                    AbcConverter.Convert(parser, path, this);
                }
            }
            catch (Exception ex)
            {
                string message = TextHelper.GetString("Info.ExceptionWhileParsing");
                TraceManager.AddAsync(message + " " + path.Path);
                TraceManager.AddAsync(ex.Message);
            }
        }

        /// <summary>
        /// Delete current class's cached file
        /// </summary>
        public override void RemoveClassCompilerCache()
        {
            // not implemented - is there any?
        }
        #endregion

        #region class resolution
        /// <summary>
        /// Evaluates the visibility of one given type from another.
        /// Caller is responsible of calling ResolveExtends() on 'inClass'
        /// </summary>
        /// <param name="inClass">Completion context</param>
        /// <param name="withClass">Completion target</param>
        /// <returns>Completion visibility</returns>
        public override Visibility TypesAffinity(ClassModel inClass, ClassModel withClass)
        {
            // same file
            if (withClass != null && inClass.InFile == withClass.InFile)
                return Visibility.Public | Visibility.Private;
            // inheritance affinity
            ClassModel tmp = inClass;
            while (!tmp.IsVoid())
            {
                if (tmp == withClass)
                    return Visibility.Public | Visibility.Private;
                tmp = tmp.Extends;
            }
            // same package
            if (withClass != null && inClass.InFile.Package == withClass.InFile.Package)
                return Visibility.Public;
            // public only
            else
                return Visibility.Public;
        }

        /// <summary>
        /// Check if a type is already in the file's imports
        /// Throws an Exception if the type name is ambiguous 
        /// (ie. same name as an existing import located in another package)
        /// </summary>
        /// <param name="member">Element to search in imports</param>
        /// <param name="atLine">Position in the file</param>
        public override bool IsImported(MemberModel member, int atLine)
        {
            int p = member.Name.IndexOf('#');
            if (p > 0)
            {
                member = member.Clone() as MemberModel;
                member.Name = member.Name.Substring(0, p);
            }

            FileModel cFile = ASContext.Context.CurrentModel;
            string fullName = member.Type;
            string name = member.Name;
            int lineMin = (ASContext.Context.InPrivateSection) ? cFile.PrivateSectionIndex : 0;
            int lineMax = atLine;
            foreach (MemberModel import in cFile.Imports)
            {
                if (import.LineFrom >= lineMin && import.LineFrom <= lineMax && import.Name == name)
                {
                    if (import.Type != fullName) throw new Exception(TextHelper.GetString("Info.AmbiguousType"));
                    return true;
                }
                else if (import.Name == "*" && import.Type.Replace("*", name) == fullName)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Prepare haXe intrinsic known vars/methods/classes
        /// </summary>
        protected override void InitTopLevelElements()
        {
            string filename = "toplevel.hx";
            topLevel = new FileModel(filename);

            // search top-level declaration
            foreach (PathModel aPath in classPath)
                if (File.Exists(Path.Combine(aPath.Path, filename)))
                {
                    filename = Path.Combine(aPath.Path, filename);
                    topLevel = GetCachedFileModel(filename);
                    break;
                }

            if (File.Exists(filename))
            {
                // copy declarations as file-level
                ClassModel tlClass = topLevel.GetPublicClass();
                if (!tlClass.IsVoid())
                {
                    topLevel.Members = tlClass.Members;
                    tlClass.Members = null;
                    topLevel.Classes = new List<ClassModel>();
                }
            }
            // not found
            else
            {
                //ErrorHandler.ShowInfo("Top-level elements class not found. Please check your Program Settings.");
            }

            topLevel.Members.Add(new MemberModel("this", "", FlagType.Variable, Visibility.Public));
            topLevel.Members.Add(new MemberModel("super", "", FlagType.Variable, Visibility.Public));
            //topLevel.Members.Add(new MemberModel("Void", "", FlagType.Intrinsic, Visibility.Public));
            topLevel.Members.Sort();
            foreach (MemberModel member in topLevel.Members)
                member.Flags |= FlagType.Intrinsic;
        }

        /// <summary>
        /// Retrieves a package content
        /// </summary>
        /// <param name="name">Package path</param>
        /// <param name="lazyMode">Force file system exploration</param>
        /// <returns>Package folders and types</returns>
        public override FileModel ResolvePackage(string name, bool lazyMode)
        {
            if ((settings.LazyClasspathExploration || lazyMode) && majorVersion >= 9 && name == "flash") 
                name = "flash9";
            return base.ResolvePackage(name, lazyMode);
        }
        #endregion

        #region Custom code completion

        internal TSSCompletion completionModeHandler;
        private Dictionary<string, TSSCompletion> handlers;

        /// <summary>
        /// Handles file switch
        /// </summary>
        internal void OnFileSwitch(string filename)
        {
            if (handlers == null)
                handlers = new Dictionary<string, TSSCompletion>();

            if (!handlers.ContainsKey(filename))
            {
                handlers[filename] = new TSSCompletion();
                handlers[filename].Init(hxsettings.NodePath, hxsettings.TSSPath, filename);
            }
            completionModeHandler = handlers[filename];
        }
        internal void OnFileSwitch()
        {
            OnFileSwitch(PluginBase.MainForm.CurrentDocument.FileName);
        }

        /// <summary>
        /// Let contexts handle code completion
        /// </summary>
        /// <param name="sci">Scintilla control</param>
        /// <param name="expression">Completion context</param>
        /// <param name="autoHide">Auto-started completion (is false when pressing Ctrl+Space)</param>
        /// <returns>Null (not handled) or member list</returns>
        public override MemberList ResolveDotContext(ScintillaNet.ScintillaControl sci, ASExpr expression, bool autoHide)
        {
            if (autoHide && !hxsettings.DisableCompletionOnDemand)
                return null;

            // auto-started completion, can be ignored for performance (show default completion tooltip)
            if (expression.Value.IndexOf(".") < 0 || (autoHide && !expression.Value.EndsWith(".")))
                if (hxsettings.DisableMixedCompletion) return new MemberList();
                else return null;

            // empty expression
            if (expression.Value == "")
                return null; // not supported yet

            MemberList list = new MemberList();

            TypeScriptCompletion hc = new TypeScriptCompletion(sci, expression.Position, completionModeHandler);
            TSSCompletionEntry[] al = hc.getList();
            if (al == null || al.Length == 0)
                return null;

            foreach (var e in al)
            {
                var isMethod = (e.kind == "method");
                FlagType flag = isMethod ? FlagType.Function : FlagType.Variable;
                MemberModel member = new MemberModel();
                member.Name = e.name;
                member.Access = Visibility.Public;
                member.Flags = flag;

                var type = e.type;
                if (isMethod)
                    populateFunctionMemberModel(member, type);
                else
                    member.Type = type;

                list.Add(member);
            }
            return list;
        }

        List<string> splitTypes(string type)
        {
            List<string> list = new List<string>();
            StringReader reader = new StringReader(type);
            int nested = 0;
            char[] buffer = new char[1];
            string temp = "";
            while (reader.Read(buffer, 0, 1) > 0)
            {
                char c = buffer[0];
                temp += c.ToString();
                if (c == '(')
                {
                    nested++;
                }
                else if (nested > 0 && c == ')')
                {
                    nested--;
                }
                if (nested == 0 && temp.EndsWith(","))
                {
                    list.Add(temp.Substring(0, temp.Length - 1).Trim());
                    temp = "";
                }
            }
            list.Add(temp);
            return list;
        }
        private void populateFunctionMemberModel(MemberModel member, string type)
        {
            member.Type = type.Substring(type.LastIndexOf("=>") + "=>".Length).Trim();
            var start = type.IndexOf("(");
            var end = type.LastIndexOf(")");
            var temp = type.Substring(start + 1, end - start - 1);

            var p = new List<MemberModel>();
            var l = splitTypes(temp);
            foreach (var t in l)
            {
                if (String.IsNullOrEmpty(t))
                    continue;

                var m = new MemberModel();
                m.Name = t.Split(':')[0];
                m.Type = t.Substring(m.Name.Length + 1).Trim();
                p.Add(m);
            }
            member.Parameters = p;
        }

        /// <summary>
        /// Let contexts handle code completion
        /// </summary>
        /// <param name="sci">Scintilla control</param>
        /// <param name="expression">Completion context</param>
        /// <returns>Null (not handled) or function signature</returns>
        public override MemberModel ResolveFunctionContext(ScintillaNet.ScintillaControl sci, ASExpr expression, bool autoHide)
        {
            if (autoHide && !hxsettings.DisableCompletionOnDemand)
                return null;

            string[] parts = expression.Value.Split('.');
            string name = parts[parts.Length - 1];
            
            MemberModel member = new MemberModel();

            // Do not show error
            string val = expression.Value;
            if (val == "for" || 
                val == "while" ||
                val == "if" ||
                val == "switch" ||
                val == "function" ||
                val == "catch" ||
                val == "trace")
                return null;

            TypeScriptCompletion hc = new TypeScriptCompletion(sci, expression.Position - 1, completionModeHandler);
            member.Name = name;
            member.Flags = FlagType.Function;
            member.Access = Visibility.Public;
            var t = hc.getSymbolType();

            if (t == "any" || t.StartsWith("TSS command"))
            {
                sci.CallTipShow(sci.CurrentPos, t);
                sci.CharAdded += new ScintillaNet.CharAddedHandler(removeTip);
            }
            else
            {
                populateFunctionMemberModel(member, t);
            }
            
            return member;
        }

        void removeTip(ScintillaNet.ScintillaControl sender, int ch)
        {
            sender.CallTipCancel();
            sender.CharAdded -= removeTip;
        }
        #endregion

        #region command line compiler

        static public string TemporaryOutputFile;

        /// <summary>
        /// Retrieve the context's default compiler path
        /// </summary>
        public override string GetCompilerPath()
        {
            return hxsettings.GetDefaultSDK().Path;
        }

        /*
        /// <summary>
        /// Check current file's syntax
        /// </summary>
        public override void CheckSyntax()
        {
            EventManager.DispatchEvent(this, new NotifyEvent(EventType.ProcessStart));
            TypeScriptCompletion hc = new TypeScriptCompletion(ASContext.CurSciControl, 0, completionModeHandler);
            ArrayList result = hc.getList();
            if (result.Count == 0 || (string)result[0] != "error")
            {
                EventManager.DispatchEvent(this, new TextEvent(EventType.ProcessEnd, "Done(0)"));
                return;
            }
            foreach (string line in result)
            {
                if (line != "error") TraceManager.Add(line);
            }
            EventManager.DispatchEvent(this, new TextEvent(EventType.ProcessEnd, "Done(1)"));
        }*/

        /// <summary>
        /// Run haXe compiler in the current class's base folder with current classpath
        /// </summary>
        /// <param name="append">Additional comiler switches</param>
        public override void RunCMD(string append)
        {
            if (!IsFileValid || !File.Exists(CurrentFile))
                return;

            string haxePath = PathHelper.ResolvePath(hxsettings.GetDefaultSDK().Path);
            if (!Directory.Exists(haxePath) && !File.Exists(haxePath))
            {
                ErrorManager.ShowInfo(TextHelper.GetString("Info.InvalidHaXePath"));
                return;
            }

            SetStatusText(settings.CheckSyntaxRunning);

            try
            {
                // save modified files if needed
                if (outputFile != null) MainForm.CallCommand("SaveAllModified", null);
                else MainForm.CallCommand("SaveAllModified", ".hx");

                // change current directory
                string currentPath = System.IO.Directory.GetCurrentDirectory();
                string filePath = (temporaryPath == null) ? Path.GetDirectoryName(cFile.FileName) : temporaryPath;
                filePath = NormalizePath(filePath);
                System.IO.Directory.SetCurrentDirectory(filePath);
                
                // prepare command
                string command = haxePath;
                if (Path.GetExtension(command) == "") command = Path.Combine(command, "haxe.exe");

                command += ";";
                if (cFile.Package.Length > 0) command += cFile.Package+".";
                string cname = cFile.GetPublicClass().Name;
                if (cname.IndexOf('<') > 0) cname = cname.Substring(0, cname.IndexOf('<'));
                command += cname;

                // classpathes
                string hxPath = PathHelper.ResolvePath(hxsettings.GetDefaultSDK().Path);
                foreach (PathModel aPath in classPath)
                    if (aPath.Path != temporaryPath
                        && !aPath.Path.StartsWith(hxPath, StringComparison.OrdinalIgnoreCase))
                        command += " -cp \"" + aPath.Path.TrimEnd('\\') + "\"";
                command = command.Replace(filePath, "");

                // run
                MainForm.CallCommand("RunProcessCaptured", command + " " + append);
                // restaure current directory
                if (System.IO.Directory.GetCurrentDirectory() == filePath)
                    System.IO.Directory.SetCurrentDirectory(currentPath);
            }
            catch (Exception ex)
            {
                ErrorManager.ShowError(ex);
            }
        }

        /// <summary>
        /// Calls RunCMD with additional parameters taken from the classes @haxe doc tag
        /// </summary>
        public override bool BuildCMD(bool failSilently)
        {
            if (!File.Exists(CurrentFile))
                return false;
            // check if @haxe is defined
            Match mCmd = null;
            ClassModel cClass = cFile.GetPublicClass();
            if (IsFileValid)
            {
                if (cFile.Comments != null)
                    mCmd = re_CMD_BuildCommand.Match(cFile.Comments);
                if ((mCmd == null || !mCmd.Success) && cClass.Comments != null)
                    mCmd = re_CMD_BuildCommand.Match(cClass.Comments);
            }

            if (mCmd == null || !mCmd.Success)
            {
                if (!failSilently)
                {
                    MessageBar.ShowWarning(TextHelper.GetString("Info.InvalidForQuickBuild"));
                }
                return false;
            }

            // build command
            string command = mCmd.Groups["params"].Value.Trim();
            try
            {
                command = Regex.Replace(command, "[\\r\\n]\\s*\\*", "", RegexOptions.Singleline);
                command = " " + MainForm.ProcessArgString(command) + " ";
                if (command == null || command.Length == 0)
                {
                    if (!failSilently)
                        throw new Exception(TextHelper.GetString("Info.InvalidQuickBuildCommand"));
                    return false;
                }
                outputFile = null;
                outputFileDetails = "";
                trustFileWanted = false;

                // get some output information url
                MatchCollection mPar = re_SplitParams.Matches(command + "-eof");
                int mPlayIndex = -1;
                bool noPlay = false;
                if (mPar.Count > 0)
                {
                    string op;
                    for (int i = 0; i < mPar.Count; i++)
                    {
                        op = mPar[i].Groups["switch"].Value;
                        int start = mPar[i].Index + mPar[i].Length;
                        int end = (mPar.Count > i + 1) ? mPar[i + 1].Index : start;
                        if ((op == "-swf") && (outputFile == null) && (mPlayIndex < 0))
                        {
                            if (end > start)
                                outputFile = command.Substring(start, end - start).Trim();
                        }
                        else if (op == "-swf-header")
                        {
                            if (end > start)
                            {
                                string[] dims = command.Substring(start, end - start).Trim().Split(':');
                                if (dims.Length > 2) outputFileDetails = ";" + dims[0] + ";" + dims[1];
                            }
                        }
                        else if (op == "-play")
                        {
                            if (end > start)
                            {
                                mPlayIndex = i;
                                outputFile = command.Substring(start, end - start).Trim();
                            }
                        }
                        else if (op == "-trust")
                        {
                            trustFileWanted = true;
                        }
                        else if (op == "-noplay")
                        {
                            noPlay = true;
                        }
                    }
                }
                if (outputFile != null && outputFile.Length == 0) outputFile = null;

                // cleaning custom switches
                if (mPlayIndex >= 0)
                {
                    command = command.Substring(0, mPar[mPlayIndex].Index) + command.Substring(mPar[mPlayIndex + 1].Index);
                }
                if (trustFileWanted)
                {
                    command = command.Replace("-trust", "");
                }
                if (noPlay || !settings.PlayAfterBuild)
                {
                    command = command.Replace("-noplay", "");
                    outputFile = null;
                    runAfterBuild = false;
                }
                else runAfterBuild = (outputFile != null);
            }
            catch (Exception ex)
            {
                ErrorManager.ShowError(ex);
                return false;
            }

            // run
            RunCMD(command);
            return true;
        }
        #endregion
    }
}
