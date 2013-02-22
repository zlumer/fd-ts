using System.Collections.Generic;
using PluginCore;
using ScintillaNet;

namespace TypeScriptContext
{
    internal class TypeScriptCompletion
    {
        private readonly int position;
        private readonly TSSCompletion comp;
        private readonly ScintillaControl sci;

        public TypeScriptCompletion(ScintillaControl sci, int position, TSSCompletion handler)
        {
            this.sci = sci;
            this.position = position;
            this.comp = handler;

            comp.Update(new List<string>(sci.Text.Split('\n')), PluginBase.MainForm.CurrentDocument.FileName.Replace("\\", "/"));
        }

        public TSSCompletionEntry[] getList()
        {
            var line = sci.LineFromPosition(position);
            var pos = position - sci.PositionFromLine(line);
            TSSCompletionInfo info = comp.GetCompletions(true, line + 1, pos + 1, PluginBase.MainForm.CurrentDocument.FileName.Replace("\\", "/"));
            return info.entries;
        }
    }
}
