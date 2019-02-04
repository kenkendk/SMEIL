using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SMEIL.Parser.AST;

namespace SMEIL.Parser
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Console.WriteLine($"Usage: dotnet {Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location)} <filename> [top-level network]");
                return;
            }

            try
            {                
                var state = Loader.LoadModuleAndImports(args[0], args.Skip(1).FirstOrDefault(), args.Skip(2).ToArray());
                state.Validate();

                var generator = new Codegen.VHDL.VHDLGenerator(state);
                var rs = new Codegen.VHDL.VHDLGenerator.RenderState();

                foreach (var nv in state.AllInstances.OfType<Instance.Network>())
                {
                    foreach (var p in nv.Instances.OfType<Instance.Process>())
                    {
                        var doc = generator.GenerateProcess(rs, p);
                        File.WriteAllText($"test.{p.Name}.vhdl", doc);
                        Console.WriteLine(doc);
                    }
                }
            }
            catch (ParserException ex)
            {
                Console.WriteLine("[{0}:{1}] \"{2}\": {3}", ex.Location.Line, ex.Location.LineOffset, ex.Location.Text, ex.Message);
            }
        }
    }
}
