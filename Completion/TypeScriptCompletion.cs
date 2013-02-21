using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ASCompletion.Context;
using PluginCore;
using ProjectManager.Projects.Generic;
using ScintillaNet;

namespace TypeScriptContext
{
    internal class TypeScriptCompletion
    {
        /*private static readonly Regex reListEntry = new Regex("<i n=\"([^\"]+)\"><t>([^<]*)</t><d>([^<]*)</d></i>",
                                                              RegexOptions.Compiled | RegexOptions.Singleline);*/

        private readonly int position;
        private readonly TSSCompletion comp;
        private readonly ScintillaControl sci;
        //private readonly ArrayList tips;
        //private int nbErrors;

        public TypeScriptCompletion(ScintillaControl sci, int position, TSSCompletion handler)
        {
            this.sci = sci;
            this.position = position;
            this.comp = handler;

            comp.Update(new List<string>(sci.Text.Split('\n')), PluginBase.MainForm.CurrentDocument.FileName.Replace("\\", "/"));
            //tips = new ArrayList();
            //nbErrors = 0;
        }

        public TSSCompletionEntry[] getList()
        {
            var line = sci.LineFromPosition(position);
            var pos = position - sci.PositionFromLine(line);
            TSSCompletionInfo info = comp.GetCompletions(true, line + 1, pos + 1, PluginBase.MainForm.CurrentDocument.FileName.Replace("\\", "/"));
            return info.entries;
        }


        /*private static String htmlUnescape(String s)
        {
            return s.Replace("&lt;", "<").Replace("&gt;", ">");
        }


        private string[] buildTSSArgs()
        {
            // check haxe project & context
            if (PluginBase.CurrentProject == null
                || !(ASContext.Context is Context))
                return null;

            //PluginBase.MainForm.CallCommand("SaveAllModified", null);

            var hp = (PluginBase.CurrentProject as GenericProject);

            Console.WriteLine(String.Join("\n", hp.CompileTargets.ToArray()));

            // Current file
            var file = PluginBase.MainForm.CurrentDocument.FileName;

            // Locate carret position
            var pos = position; // sci.CurrentPos;
            // locate a . or (
            while (pos > 1 && sci.CharAt(pos - 1) != '.' && sci.CharAt(pos - 1) != '(')
                pos--;

            try
            {
                var bom = new Byte[4];
                var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.CanSeek)
                {
                    fs.Read(bom, 0, 4);
                    fs.Close();
                    if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
                    {
                        pos += 3; // Skip BOM
                    }
                }
            }
            catch
            {
            }

            // Build haXe command
            var paths = ProjectManager.PluginMain.Settings.GlobalClasspaths.ToArray();

            return paths;
        }

        private ArrayList parseLines(string[] lines)
        {
            var type = "";
            var error = "";

            Console.WriteLine(htmlUnescape(String.Join("\n", lines)));
            for (var i = 0; i < lines.Length; i++)
            {
                var l = lines[i].Trim();

                if (l.Length == 0)
                    continue;

                // Get list of properties
                switch (l)
                {
                    case "<list>":
                        {
                            var content = new ArrayList();
                            var xml = "";
                            while (++i < lines.Length)
                                xml += lines[i];
                            foreach (Match m in reListEntry.Matches(xml))
                            {
                                var seq = new ArrayList
                                              {
                                                  m.Groups[1].Value,
                                                  htmlUnescape(m.Groups[2].Value),
                                                  htmlUnescape(m.Groups[3].Value)
                                              };
                                content.Add(seq);
                            }

                            tips.Add("list");
                            tips.Add(content);

                            break;
                        }
                    case "<type>":
                        type = htmlUnescape(lines[++i].Trim('\r'));
                        tips.Add("type");
                        tips.Add(type);
                        break;
                    default:
                        if (l[0] == '<') continue;
                        if (l[0] == 1) continue; // ignore log
                        if (l[0] == 2) l = l.Substring(1);
                        if (nbErrors == 0)
                            error += l;
                        else if (nbErrors < 5)
                            error += "\n" + l;
                        else if (nbErrors == 5)
                            error += "\n...";
                        nbErrors++;
                        break;
                }
            }


            if (error != "")
            {
                tips.Clear();
                tips.Add("error");
                tips.Add(error);
            }
            return tips;
        }*/
    }
}
