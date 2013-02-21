using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using PluginCore;
using ProjectManager.Projects.Generic;
using PluginCore.Managers;
using System;
using System.Text.RegularExpressions;

namespace TypeScriptContext
{
    delegate void FallbackNeededHandler(bool notSupported);

    class TSSCompletionHandler
    {
        public event FallbackNeededHandler FallbackNeeded;

        private readonly Process tssProcess;
        private bool listening;
        private bool failure;

        public TSSCompletionHandler(Process tssProcess)
        {
            this.tssProcess = tssProcess;
        }

        public bool IsRunning()
        {
            try { return !tssProcess.HasExited; } 
            catch { return false; }
        }

        ~TSSCompletionHandler()
        {
            Stop();
        }

        private void InitProject()
        {

        }

        public string[] GetCompletion(string[] args)
        {
            if (!IsRunning()) StartServer();
            if (args == null)
                return new string[0];
            try
            {
                tssProcess.StandardInput.WriteLine("");
                tssProcess.WaitForInputIdle();
                string output = tssProcess.StandardOutput.ReadToEnd();
                return output.Split('\n');
            }
            catch(Exception ex)
            {
                TraceManager.AddAsync(ex.Message);
                if (!failure && FallbackNeeded != null)
                    FallbackNeeded(false);
                failure = true;
                return new string[0];
            }
        }

        public void StartServer()
        {
            if (IsRunning()) return;
            tssProcess.Start();
            if (!listening)
            {
                listening = true;
                tssProcess.BeginOutputReadLine();
                tssProcess.BeginErrorReadLine();
                tssProcess.OutputDataReceived += new DataReceivedEventHandler(haxeProcess_OutputDataReceived);
                tssProcess.ErrorDataReceived += new DataReceivedEventHandler(haxeProcess_ErrorDataReceived);
            }
        }

        void haxeProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            TraceManager.AddAsync(e.Data, 2);
        }

        void haxeProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            //TraceManager.AddAsync(e.Data);
            if (Regex.IsMatch(e.Data, "Error.*--wait"))
            {
                if (!failure && FallbackNeeded != null) 
                    FallbackNeeded(true);
                failure = true;
            }
        }

        public void Stop()
        {
            if (IsRunning())
                tssProcess.Kill();
        }
    }
}
