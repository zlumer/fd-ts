using System.Collections.Generic;
using PluginCore;
using ScintillaNet;

namespace TypeScriptContext
{
    internal class TypeScriptCompletion
    {
        private readonly int position;
        private readonly int line;
        private readonly int pos;
        private readonly string filename;
        private readonly TSSCompletion comp;
        private readonly ScintillaControl sci;

        public TypeScriptCompletion(ScintillaControl sci, int position, TSSCompletion handler)
        {
            this.sci = sci;
            this.position = position;
            this.comp = handler;
            
            line = sci.LineFromPosition(position) + 1;
            pos = position - sci.PositionFromLine(line - 1) + 1;
            filename = PluginBase.MainForm.CurrentDocument.FileName.Replace("\\", "/");

            comp.Update(new List<string>(sci.Text.Replace("\r\n", "\n").Split('\n')), filename);
        }

        public TSSCompletionEntry[] getList(bool hasDot)
        {
            try
            {
                TSSCompletionInfo info = comp.GetCompletions(hasDot, line, pos, filename);
                return info.entries;
            }
            catch (System.Exception e)
            {
                return null;
            }
        }
        public TSSDefinitionResponse getDefinition()
        {
            TSSDefinitionResponse r = comp.GetDefinition(line, pos, filename);
            return r;
        }
        public string getSymbolType()
        {
            try
            {
                var s = comp.GetType(line, pos, filename);
                return s;
            }
            catch (System.Exception e)
            {
                return "any";
            }
        }
    }
}
