using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spark.Compiler;
using Spark.Compiler.NodeVisitors;
using Spark.FileSystem;
using Spark.Parser.Code;
using Spark.Parser.Markup;

namespace Spark.Parser.Syntax
{
    public class OxygeneSyntaxProvider : AbstractSyntaxProvider
    {
        private readonly OxygeneMarkupGrammar _grammar;

        public OxygeneSyntaxProvider(IParserSettings settings)
        {
            _grammar = new OxygeneMarkupGrammar(settings);
        }

        public override IList<Chunk> GetChunks(VisitorContext context, string path)
        {
            context.SyntaxProvider = this;
            context.ViewPath = path;

            var sourceContext = CreateSourceContext(context.ViewPath, context.ViewFolder);
            var position = new Position(sourceContext);

            var result = _grammar.Nodes(position);
            if (result.Rest.PotentialLength() != 0)
            {
                ThrowParseException(context.ViewPath, position, result.Rest);
            }

            context.Paint = result.Rest.GetPaint();

            var nodes = result.Value;
            foreach (var visitor in BuildNodeVisitors(context))
            {
                visitor.Accept(nodes);
                nodes = visitor.Nodes;
            }

            var chunkBuilder = new ChunkBuilderVisitor(context);
            chunkBuilder.Accept(nodes);
            return chunkBuilder.Chunks;
        }

        public override IList<Node> IncludeFile(VisitorContext context, string path, string parse)
        {
            var existingPath = context.ViewPath;

            var directoryPath = Path.GetDirectoryName(context.ViewPath);
            var relativePath = path.Replace('/', '\\');
            while (relativePath.StartsWith("..\\"))
            {
                directoryPath = Path.GetDirectoryName(directoryPath);
                relativePath = relativePath.Substring(3);
            }
            context.ViewPath = Path.Combine(directoryPath, relativePath);

            var sourceContext = CreateSourceContext(context.ViewPath, context.ViewFolder);

            if (parse == "text")
            {
                var encoded = sourceContext.Content
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
                return new[] { new TextNode(encoded) };
            }


            var position = new Position(sourceContext);
            var result = _grammar.Nodes(position);
            if (result.Rest.PotentialLength() != 0)
            {
                ThrowParseException(context.ViewPath, position, result.Rest);
            }
            context.Paint = context.Paint.Union(result.Rest.GetPaint());

            var namespaceVisitor = new NamespaceVisitor(context);
            namespaceVisitor.Accept(result.Value);

            var includeVisitor = new IncludeVisitor(context);
            includeVisitor.Accept(namespaceVisitor.Nodes);

            context.ViewPath = existingPath;
            return includeVisitor.Nodes;
        }

        public override Snippets ParseFragment(Position begin, Position end)
        {
            var result = _grammar.Expression(begin.Constrain(end));

            var unparsedLength = result.Rest.PotentialLength();
            if (unparsedLength == 0)
                return result.Value;

            var snippets = new Snippets(result.Value);

            snippets.Add(new Snippet
            {
                Value = result.Rest.Peek(unparsedLength),
                Begin = result.Rest,
                End = result.Rest.Advance(unparsedLength)
            });

            return snippets;

        }

        private IList<INodeVisitor> BuildNodeVisitors(VisitorContext context)
        {
            return new INodeVisitor[]
                       {
                           new NamespaceVisitor(context),
                           new IncludeVisitor(context),
                           new PrefixExpandingVisitor(context),
                           new SpecialNodeVisitor(context),
                           new CacheAttributeVisitor(context),
                           new ForEachAttributeVisitor(context),
                           new ConditionalAttributeVisitor(context),
                           new OmitExtraLinesVisitor(context),
                           new TestElseElementVisitor(context),
                           new OnceAttributeVisitor(context),
                           new UrlAttributeVisitor(context),
                           new BindingExpansionVisitor(context)
                       };
        }
    }
}

