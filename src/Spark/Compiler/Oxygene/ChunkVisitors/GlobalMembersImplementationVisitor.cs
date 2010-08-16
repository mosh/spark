using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Spark.Compiler.ChunkVisitors;

namespace Spark.Compiler.Oxygene.ChunkVisitors
{

    /// <summary>
    /// GlobalMembersVisitor adds code to declaration section
    /// GlobalMembersImplementationVisitor adds code to implementation section
    /// </summary>
    class GlobalMembersImplementationVisitor : ChunkVisitor
    {
        private readonly SourceWriter _source;
        private readonly Dictionary<string, object> _globalSymbols;
        private readonly NullBehaviour _nullBehaviour;
        readonly Dictionary<string, string> _viewDataAdded = new Dictionary<string, string>();
        readonly Dictionary<string, GlobalVariableChunk> _globalAdded = new Dictionary<string, GlobalVariableChunk>();

        public GlobalMembersImplementationVisitor(SourceWriter output, Dictionary<string, object> globalSymbols, NullBehaviour nullBehaviour)
        {
            _source = output;
            _globalSymbols = globalSymbols;
            _nullBehaviour = nullBehaviour;
        }

        public string ViewClassName { get; set; } 


        private SourceWriter CodeIndent(Chunk chunk)
        {
            if (_source.AdjustDebugSymbols)
            {
                if (chunk != null && chunk.Position != null)
                {
                    _source.StartOfLine = false;
                    return _source
                        //.WriteDirective("#line {0} \"{1}\"", chunk.Position.Line, chunk.Position.SourceContext.FileName)
                        .Indent(chunk.Position.Column - 1);
                }

                _source.StartOfLine = false;
                //return _source.WriteLine("#line default");
                return _source.WriteLine("");
            }

            return _source;
        }

        //private void CodeHidden()
        //{
        //    if (_source.AdjustDebugSymbols)
        //        _source.WriteDirective("#line hidden");
        //}

        //private void CodeDefault()
        //{
        //    if (_source.AdjustDebugSymbols)
        //        _source.WriteDirective("#line default");
        //}

        //protected override void Visit(GlobalVariableChunk chunk)
        //{
        //    if (!_globalSymbols.ContainsKey(chunk.Name))
        //        _globalSymbols.Add(chunk.Name, null);

        //    if (_globalAdded.ContainsKey(chunk.Name))
        //    {
        //        if (_globalAdded[chunk.Name].Type != chunk.Type ||
        //            _globalAdded[chunk.Name].Value != chunk.Value)
        //        {
        //            throw new CompilerException(string.Format("The global named {0} cannot be declared repeatedly with different types or values",
        //                                                      chunk.Name));
        //        }
        //        return;
        //    }

        //    var type = chunk.Type ?? "object";
        //    var typeParts = type.ToString().Split(' ', '\t');
        //    if (typeParts.Contains("const") || typeParts.Contains("readonly"))
        //    {
        //        _source.WriteFormat("\r\n    {0} {1} = {2};",
        //                             type, chunk.Name, chunk.Value);
        //    }
        //    else
        //    {
        //        _source.WriteFormat(
        //            "\r\n    {0} _{1} = {2};\r\n    public {0} {1} {{ get {{return _{1};}} set {{_{1} = value;}} }}",
        //            type, chunk.Name, chunk.Value);
        //    }
        //    _source.WriteLine();
        //}


        //protected override void Visit(ViewDataModelChunk chunk)
        //{
        //    if (Snippets.IsNullOrEmpty(chunk.TModelAlias))
        //        return;

        //    _source
        //        .WriteCode(chunk.TModel)
        //        .Write(" ")
        //        .WriteCode(chunk.TModelAlias)
        //        .WriteLine();
        //    CodeIndent(chunk).WriteLine("{get {return ViewData.Model;}}");
        //    CodeDefault();
        //}

        //protected override void Visit(ViewDataChunk chunk)
        //{
        //    var key = chunk.Key;
        //    var name = chunk.Name;
        //    var type = chunk.Type ?? "object";

        //    if (!_globalSymbols.ContainsKey(name))
        //        _globalSymbols.Add(name, null);

        //    if (_viewDataAdded.ContainsKey(name))
        //    {
        //        if (_viewDataAdded[name] != key + ":" + type)
        //        {
        //            throw new CompilerException(
        //                string.Format("The view data named {0} cannot be declared with different types '{1}' and '{2}'",
        //                              name, type, _viewDataAdded[name]));
        //        }
        //        return;
        //    }

        //    _viewDataAdded.Add(name, key + ":" + type);
        //    _source.WriteCode(type).Write(" ").WriteLine(name);
        //    if (Snippets.IsNullOrEmpty(chunk.Default))
        //    {
        //        CodeIndent(chunk)
        //            .Write("{get {return (")
        //            .WriteCode(type)
        //            .Write(")ViewData.Eval(\"")
        //            .Write(key)
        //            .WriteLine("\");}}");
        //    }
        //    else
        //    {
        //        CodeIndent(chunk)
        //            .Write("{get {return (")
        //            .WriteCode(type)
        //            .Write(")(ViewData.Eval(\"")
        //            .Write(key)
        //            .Write("\")??")
        //            .WriteCode(chunk.Default)
        //            .WriteLine(");}}");
        //    }
        //    CodeDefault();
        //}

        protected override void Visit(ExtensionChunk chunk)
        {
            chunk.Extension.VisitChunk(this, OutputLocation.ClassMembers, chunk.Body, _source.GetStringBuilder());
        }

        // just declare methods, leave implementation is left for GeneratedCodeVistor 
        protected override void Visit(MacroChunk chunk)
        {
            _source.Write("\r\n");
            _source.Write(string.Format("method {0}.{1}(", this.ViewClassName, chunk.Name));
            string delimiter = "";
            foreach (var parameter in chunk.Parameters)
            {
                _source.Write(delimiter).Write(parameter.Name).Write(" : ").WriteCode(parameter.Type);
                delimiter = "; ";
            }
            _source.WriteLine(") : System.Object;");

            CodeIndent(chunk).WriteLine("begin");
            _source.WriteLine("        using(OutputScope(new System.IO.StringWriter())) do");
            _source.WriteLine("        begin");

            var variables = new Dictionary<string, object>();
            foreach (var param in chunk.Parameters)
            {
                variables.Add(param.Name, null);
            }
            var generator = new GeneratedCodeVisitor(_source, variables, _nullBehaviour);
            generator.Accept(chunk.Body);

            _source.WriteLine("            result := self.ToMvcHtmlString(Output);");
            _source.WriteLine("        end;");
            _source.WriteLine("    end;");
        }
    }
}
