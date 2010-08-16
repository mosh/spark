using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Spark.Compiler.Oxygene.ChunkVisitors;

namespace Spark.Compiler.Oxygene
{
    public class OxygeneViewCompiler : ViewCompiler
    {
        public override void CompileView(IEnumerable<IList<Chunk>> viewTemplates, IEnumerable<IList<Chunk>> allResources)
        {
            GenerateSourceCode(viewTemplates, allResources);

            var batchCompiler = new BatchCompiler();
            var assembly = batchCompiler.Compile(Debug, "oxygene", SourceCode);
            CompiledType = assembly.GetType(ViewClassFullName);
        }

        public override void GenerateSourceCode(IEnumerable<IList<Chunk>> viewTemplates, IEnumerable<IList<Chunk>> allResources)
        {
            var globalSymbols = new Dictionary<string, object>();

            var writer = new StringWriter();
            var source = new SourceWriter(writer);

            var usingGenerator = new UsingNamespaceVisitor(source);
            var baseClassGenerator = new BaseClassVisitor { BaseClass = BaseClass };
            var globalsGenerator = new GlobalMembersVisitor(source, globalSymbols, NullBehaviour);
            var globalsImplementationGenerator = new GlobalMembersImplementationVisitor(source, globalSymbols, NullBehaviour);

            if (string.IsNullOrEmpty(TargetNamespace))
            {
                source.WriteLine("namespace ;");
            }
            else
            {
                source.WriteLine(string.Format("namespace {0};", TargetNamespace));
            }

            source.WriteLine("interface");



            // using <namespaces>;

            if (UseNamespaces != null)
            {
                foreach (var ns in UseNamespaces ?? new string[0])
                {
                    usingGenerator.UsingNamespace(ns);
                }

                if (usingGenerator._namespaceAdded.Count > 0)
                {
                    source.Write(";").WriteLine("");
                }
            }


            //foreach (var ns in UseNamespaces ?? new string[0])
            //{
            //    usingGenerator.UsingNamespace(ns);
            //}

            foreach (var assembly in UseAssemblies ?? new string[0])
            {
                usingGenerator.UsingAssembly(assembly);
            }

            foreach (var resource in allResources)
            {
                usingGenerator.Accept(resource);
            }

            foreach (var resource in allResources)
            {
                baseClassGenerator.Accept(resource);
            }

            var viewClassName = "View" + GeneratedViewId.ToString("n");

            if (string.IsNullOrEmpty(TargetNamespace))
            {
                ViewClassFullName = viewClassName;
            }
            else
            {
                ViewClassFullName = TargetNamespace + "." + viewClassName;

                //source
                //    .WriteLine()
                //    .WriteLine(string.Format("namespace {0}", TargetNamespace))
                //    .WriteLine("{").AddIndent();
            }


            source.WriteLine();

            var descriptorText = new StringBuilder();

            if (Descriptor != null)
            {
                // [SparkView] attribute
                descriptorText.Append("[Spark.SparkViewAttribute(");
                if (TargetNamespace != null)
                    descriptorText.Append(String.Format("    TargetNamespace:=\"{0}\",", TargetNamespace));
                descriptorText.Append("    Templates := array of System.String ([");
                descriptorText.Append("      ");
                descriptorText.Append(string.Join(",\r\n      ",
                                                               Descriptor.Templates.Select(
                                                                   //t => "\"" + t.Replace("\\", "\\\\") + "\"").ToArray()));
                                                                   t => "'" + t+ "'").ToArray()));
                descriptorText.Append("    ]))]");
            }

            // public class ViewName : BasePageType 
            source
                .WriteLine("type ")
                .WriteLine(descriptorText.ToString())
                .Write(viewClassName)
                .Write(" = public class (")
                .WriteCode(baseClassGenerator.BaseClassTypeName)
                .Write(")")
                .WriteLine();

            source.WriteLine();
            source.WriteLine("private class var ");
            EditorBrowsableStateNever(source, 4);
            source.WriteLine("_generatedViewId : System.Guid  := new System.Guid('{0:n}');", GeneratedViewId);

            source.WriteLine("public property GeneratedViewId : System.Guid ");
            source.WriteLine("read _generatedViewId; override;");

            if (Descriptor != null && Descriptor.Accessors != null)
            {
                foreach (var accessor in Descriptor.Accessors)
                {
                    source.WriteLine();
                    source.Write("public ").WriteLine(accessor.Property);
                    source.Write("{ get { return ").Write(accessor.GetValue).WriteLine("; } }");
                }
            }

            // properties and macros
			// Note: macros are methods, implementation is delegated to later on...
            foreach (var resource in allResources)
			{
                globalsGenerator.Accept(resource);
			}


            // public void RenderViewLevelx()
            int renderLevel = 0;
            foreach (var viewTemplate in viewTemplates)
            {
                source.WriteLine();
                source.WriteLine("public");
                EditorBrowsableStateNever(source, 4);
                source.WriteLine(string.Format("method RenderViewLevel{0};", renderLevel));
                ++renderLevel;
            }

            // public void RenderView()

            source.WriteLine();
            source.WriteLine("public");
            EditorBrowsableStateNever(source, 4);
            source.WriteLine("method Render; override;");
            source.WriteLine();


            source.WriteLine("end;");

            source.WriteLine("");

            source.WriteLine("implementation");

            globalsImplementationGenerator.ViewClassName = viewClassName;

            foreach (var resource in allResources)
            {
                globalsImplementationGenerator.Accept(resource);
            }

            // public void RenderViewLevelx()
            renderLevel = 0;
            foreach (var viewTemplate in viewTemplates)
            {
                source.WriteLine();
                source.WriteLine(string.Format("method {0}.RenderViewLevel{1}();", viewClassName, renderLevel));
                source.WriteLine("begin").AddIndent();
                var viewGenerator = new GeneratedCodeVisitor(source, globalSymbols, NullBehaviour);
                viewGenerator.Accept(viewTemplate);
                source.RemoveIndent().WriteLine("end;");
                ++renderLevel;
            }

            // public void RenderView()

            source.WriteLine();
            source.WriteLine(string.Format("method {0}.Render();", viewClassName));
            source.WriteLine("begin").AddIndent();
            for (var invokeLevel = 0; invokeLevel != renderLevel; ++invokeLevel)
            {
                if (invokeLevel != renderLevel - 1)
                {
                    source.WriteLine("using OutputScope do begin RenderViewLevel{0}(); Content['view'] := Output; end;", invokeLevel);
                }
                else
                {

                    source.WriteLine("        RenderViewLevel{0};", invokeLevel);
                }
            }
            source.RemoveIndent().WriteLine("end;");

            // end class
            source.WriteLine("end.");

            SourceCode = source.ToString();
            SourceMappings = source.Mappings;
        }

        private static void EditorBrowsableStateNever(SourceWriter source, int indentation)
        {
            source
                .Indent(indentation)
                .WriteLine("[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
        }

    }
}
