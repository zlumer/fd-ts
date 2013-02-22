using System;
using System.Collections.Generic;
using System.Text;
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
            tssProcess.StartInfo.Arguments = tssPath + " " + tsSourcePath;
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
        private T GetTSSResponse<T>(List<string> args)
        {
            if (tssProcess.HasExited)
                throw new Exception("tss process has exited!");

            tssProcess.StandardInput.WriteLine(String.Join(" ", args.ToArray()));
            string response = tssProcess.StandardOutput.ReadLine();
            T json = JsonConvert.DeserializeObject<T>(response);
            return json;
        }
        public TSSCompletionInfo GetCompletions(bool isMember, int line, int pos, string fileName)
        {
            List<string> args = new List<string> { "completions", isMember ? "true" : "false", line.ToString(), pos.ToString(), fileName };
            return GetTSSResponse<TSSCompletionInfo>(args);
        }
        public string GetType(int line, int pos, string fileName)
        {
            List<string> args = new List<string> { "type", line.ToString(), pos.ToString(), fileName };
            return GetTSSResponse<string>(args);
        }
        public TSSDefinitionResponse GetDefinition(int line, int pos, string fileName)
        {
            List<string> args = new List<string> { "definition", line.ToString(), pos.ToString(), fileName };
            return GetTSSResponse<TSSDefinitionResponse>(args);
        }
        public string Update(List<string> lines, string fileName)
        {
            List<string> args = new List<string> { "update", lines.Count.ToString(), fileName };
            tssProcess.StandardInput.WriteLine(String.Join(" ", args.ToArray()));
            lines.ForEach(tssProcess.StandardInput.WriteLine);
            return tssProcess.StandardOutput.ReadLine();
        }
        public string Dump(string dumpFile, string fileName)
        {
            var args = new List<string> { "dump", dumpFile, fileName };
            return GetTSSResponse<string>(args);
        }
        public string Reload()
        {
            List<string> args = new List<string> { "reload" };
            return GetTSSResponse<string>(args);
        }
        public void Finish()
        {
            tssProcess.StandardInput.WriteLine("quit");
            tssProcess.WaitForExit(3000);
        }
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