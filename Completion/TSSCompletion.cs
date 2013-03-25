using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace TypeScriptContext
{
    class TSSCompletion
    {
        private Process tssProcess;

        public void Init(string nodePath, string tssPath, string tsSourcePath)
        {
            tssProcess = new Process();
            tssProcess.StartInfo.FileName = nodePath;
            tssProcess.StartInfo.Arguments = tssPath + " \"" + Path.GetFullPath(tsSourcePath) + "\"";
            tssProcess.StartInfo.UseShellExecute = false;
            tssProcess.StartInfo.RedirectStandardInput = true;
            tssProcess.StartInfo.RedirectStandardOutput = true;
            tssProcess.StartInfo.RedirectStandardError = true;
            tssProcess.StartInfo.CreateNoWindow = true;
            tssProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            tssProcess.Start();

            Console.WriteLine(tssProcess.StartInfo.FileName + " " + tssProcess.StartInfo.Arguments);

            Console.WriteLine(tssProcess.StandardOutput.ReadLine());
        }
        private T GetTSSResponse<T>(Dictionary<string, Object> args)
        {
            if (tssProcess.HasExited)
                throw new Exception("tss process has exited!");

            tssProcess.StandardInput.WriteLine(JsonConvert.SerializeObject(args));
            string response = tssProcess.StandardOutput.ReadLine();
            TSSResponse<T> json = JsonConvert.DeserializeObject<TSSResponse<T>>(response);
            if (json.error != null)
                throw new Exception(json.error);
            return json.success;
        }
        private T GetTSSResponse<T>(string command, Dictionary<string, Object> args)
        {
            args.Add("command", command);
            return GetTSSResponse<T>(args);
        }
        private T GetTSSResponse<T>(string command, string fileName, Dictionary<string, Object> args)
        {
            args.Add("file", fileName);
            return GetTSSResponse<T>(command, args);
        }
        private T GetTSSResponse<T>(string command, string fileName, int line, int pos, Dictionary<string, Object> args)
        {
            args.Add("line", line);
            args.Add("col", pos);
            return GetTSSResponse<T>(command, fileName, args);
        }
        private Dictionary<string, Object> no_args()
        {
            return new Dictionary<string, Object>();
        }
        public TSSCompletionInfo GetCompletions(bool isMember, int line, int pos, string fileName)
        {
            Dictionary<string, Object> args = new Dictionary<string, object>() { { "member", isMember } };
            return GetTSSResponse<TSSCompletionInfo>("completions", fileName, line, pos, args);
        }
        public string GetType(int line, int pos, string fileName)
        {
            return GetTSSResponse<string>("type", fileName, line, pos, no_args());
        }
        public string GetSymbol(int line, int pos, string fileName)
        {
            return GetTSSResponse<string>("symbol", fileName, line, pos, no_args());
        }
        public TSSDefinitionResponse GetDefinition(int line, int pos, string fileName)
        {
            return GetTSSResponse<TSSDefinitionResponse>("definition", fileName, line, pos, no_args());
        }
        public string Update(List<string> lines, string fileName)
        {
            Dictionary<string, object> args = new Dictionary<string, object>() { { "command", "update" }, { "count", lines.Count }, { "file", fileName } };
            var req = JsonConvert.SerializeObject(args);
            tssProcess.StandardInput.WriteLine(req);
            lines.ForEach(tssProcess.StandardInput.WriteLine);
            var response = tssProcess.StandardOutput.ReadLine();
            return response;
        }
        public string Dump(string dumpFile, string fileName)
        {
            Dictionary<string, Object> args = new Dictionary<string, object>() { { "outFile", dumpFile } };
            return GetTSSResponse<string>("dump", fileName, args);
        }
        public string Reload()
        {
            return GetTSSResponse<string>("reload", no_args());
        }
        public void Finish()
        {
            tssProcess.StandardInput.WriteLine(new Dictionary<string, object>() { { "command", "quit" } });
            tssProcess.WaitForExit(3000);
        }
    }
    class TSSResponse<T>
    {
        public T success;
        public string error;
    }
    class TSSCompletionInfo
    {
        public bool maybeInaccurate;
        public bool isMemberCompletion;
        public TSSCompletionEntry[] entries;
    }
    class TSSCompletionEntry
    {
        public string name;
        public string type; // () => string
        public string kind; // method, property
        public string kindModifiers; // declare, public, private
        public string fullSymbolName; // String.toString
        public string docComment;
    }
    class TSSDefinitionResponse
    {
        public TSSDefinitionInfo def;
        public string file;
        public int[] min;
        public int[] lim;
    }
    class TSSDefinitionInfo
    {
        public int unitIndex;
        public int minChar;
        public int limChar;
        public string kind;
        public string name;
        public string containerKind;
        public string containerName;
    }
}