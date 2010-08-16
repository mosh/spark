﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Spark.Compiler.Oxygene;
using Spark.Compiler;
using Spark.Tests.Stubs;
using Spark.Tests.Models;
using NUnit.Framework.SyntaxHelpers;

namespace Spark.Tests.Compiler
{
    [TestFixture]
    class OxygeneViewCompilerTester
    {
        [SetUp]
        public void Init()
        {
        }

        private static void DoCompileView(ViewCompiler compiler, IList<Chunk> chunks)
        {
            compiler.CompileView(new[] { chunks }, new[] { chunks });
        }

        [Test]
        public void MakeAndCompile()
        {
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };

            DoCompileView(compiler, new[] { new SendLiteralChunk { Text = "hello world" } });

            var instance = compiler.CreateInstance();
            string contents = instance.RenderView();

            Assert.That(contents.Contains("hello world"));
        }


        [Test]
        public void UnsafeLiteralCharacters()
        {
            var text = "hello\t\r\n'world";
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };
            DoCompileView(compiler, new[] { new SendLiteralChunk { Text = text } });

            Assert.That(compiler.SourceCode.Contains("Write('hello"));

            var instance = compiler.CreateInstance();
            string contents = instance.RenderView();

            Assert.AreEqual(text, contents);
        }

        [Test]
        public void SimpleOutput()
        {
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };
            DoCompileView(compiler, new[] { new SendExpressionChunk { Code = "3 + 4" } });
            var instance = compiler.CreateInstance();
            string contents = instance.RenderView();
            Assert.AreEqual("7", contents);
        }

        [Test]
        public void LocalVariableDecl()
        {
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };
            DoCompileView(compiler, new Chunk[]
                                    {
                                        new LocalVariableChunk { Name = "i", Value = "5" }, 
                                        new SendExpressionChunk { Code = "i" }
                                    });
            var instance = compiler.CreateInstance();
            string contents = instance.RenderView();

            Assert.AreEqual("5", contents);
        }

        [Test]
        public void ForEachLoop()
        {
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };
            DoCompileView(compiler, new Chunk[]
                                    {
                                        new LocalVariableChunk {Name = "data", Value = "array of System.Object ([3,4,5])"},
                                        new SendLiteralChunk {Text = "<ul>"},
                                        new ForEachChunk
                                        {
                                            //Code = "var item in data",
                                            Code = "item in data", // for each item in data
                                            Body = new Chunk[]
                                                   { 
                                                       new SendLiteralChunk {Text = "<li>"},
                                                       new SendExpressionChunk {Code = "item"},
                                                       new SendLiteralChunk {Text = "</li>"}
                                                   }
                                        },
                                        new SendLiteralChunk {Text = "</ul>"}
                                    });
            var instance = compiler.CreateInstance();
            var contents = instance.RenderView();
            Assert.AreEqual("<ul><li>3</li><li>4</li><li>5</li></ul>", contents);
        }

        //[Test]
        //public void GlobalVariables()
        //{
        //    var compiler = new CSharpViewCompiler { BaseClass = "Spark.SparkViewBase" };
        //    DoCompileView(compiler, new Chunk[]
        //                            {
        //                                new SendExpressionChunk{Code="title"},
        //                                new AssignVariableChunk{ Name="item", Value="8"},
        //                                new SendLiteralChunk{ Text=":"},
        //                                new SendExpressionChunk{Code="item"},
        //                                new GlobalVariableChunk{ Name="title", Value="\"hello world\""},
        //                                new GlobalVariableChunk{ Name="item", Value="3"}
        //                            });
        //    var instance = compiler.CreateInstance();
        //    var contents = instance.RenderView();
        //    Assert.AreEqual("hello world:8", contents);
        //}

        [Test]
        public void TargetNamespace()
        {
            var compiler = new OxygeneViewCompiler
            {
                BaseClass = "Spark.SparkViewBase",
                Descriptor = new SparkViewDescriptor { TargetNamespace = "Testing.Target.Namespace" },
                UseNamespaces = new List<string>() { "System.Collections.Generic" } 
            };
            DoCompileView(compiler, new Chunk[] { new SendLiteralChunk { Text = "Hello" } });
            var instance = compiler.CreateInstance();
            Assert.AreEqual("Testing.Target.Namespace", instance.GetType().Namespace);

        }


        [Test, ExpectedException(typeof(CompilerException))]
        public void ProvideFullException()
        {
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };
            DoCompileView(compiler, new Chunk[]
                                    {
                                        new SendExpressionChunk {Code = "NoSuchVariable"}
                                    });
        }

        [Test]
        public void IfTrueCondition()
        {
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };

            var trueChunks = new Chunk[] { new SendLiteralChunk { Text = "wastrue" } };

            DoCompileView(compiler, new Chunk[]
                                    {
                                        new SendLiteralChunk {Text = "<p>"},
                                        new LocalVariableChunk{Name="arg", Value="5"},
                                        new ConditionalChunk{Type=ConditionalType.If, Condition="arg = 5", Body=trueChunks},
                                        new SendLiteralChunk {Text = "</p>"}
                                    });
            var instance = compiler.CreateInstance();
            var contents = instance.RenderView();
            Assert.AreEqual("<p>wastrue</p>", contents);
        }

        [Test]
        public void IfFalseCondition()
        {
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };

            var trueChunks = new Chunk[] { new SendLiteralChunk { Text = "wastrue" } };

            DoCompileView(compiler, new Chunk[]
                                    {
                                        new SendLiteralChunk {Text = "<p>"},
                                        new LocalVariableChunk{Name="arg", Value="5"},
                                        new ConditionalChunk{Type=ConditionalType.If, Condition="arg = 6", Body=trueChunks},
                                        new SendLiteralChunk {Text = "</p>"}
                                    });
            var instance = compiler.CreateInstance();
            var contents = instance.RenderView();
            Assert.AreEqual("<p></p>", contents);
        }

        [Test]
        public void IfElseFalseCondition()
        {
            var compiler = new OxygeneViewCompiler { BaseClass = "Spark.SparkViewBase" };

            var trueChunks = new Chunk[] { new SendLiteralChunk { Text = "wastrue" } };
            var falseChunks = new Chunk[] { new SendLiteralChunk { Text = "wasfalse" } };

            DoCompileView(compiler, new Chunk[]
                                    {
                                        new SendLiteralChunk {Text = "<p>"},
                                        new LocalVariableChunk{Name="arg", Value="5"},
                                        new ConditionalChunk{Type=ConditionalType.If, Condition="arg = 6", Body=trueChunks},
                                        new ConditionalChunk{Type=ConditionalType.Else, Body=falseChunks},
                                        new SendLiteralChunk {Text = "</p>"}
                                    });
            var instance = compiler.CreateInstance();
            var contents = instance.RenderView();
            Assert.AreEqual("<p>wasfalse</p>", contents);
        }

        //[Test]
        //public void LenientSilentNullDoesNotCauseWarningCS0168()
        //{
        //    var compiler = new CSharpViewCompiler()
        //    {
        //        BaseClass = "Spark.Tests.Stubs.StubSparkView",
        //        NullBehaviour = NullBehaviour.Lenient
        //    };
        //    var chunks = new Chunk[]
        //                 {
        //                     new ViewDataChunk { Name="comment", Type="Spark.Tests.Models.Comment"},
        //                     new SendExpressionChunk {Code = "comment.Text", SilentNulls = true}
        //                 };
        //    compiler.CompileView(new[] { chunks }, new[] { chunks });
        //    Assert.That(compiler.SourceCode.Contains("catch(System.NullReferenceException)"));
        //}

        //[Test]
        //public void LenientOutputNullDoesNotCauseWarningCS0168()
        //{
        //    var compiler = new CSharpViewCompiler()
        //    {
        //        BaseClass = "Spark.Tests.Stubs.StubSparkView",
        //        NullBehaviour = NullBehaviour.Lenient
        //    };
        //    var chunks = new Chunk[]
        //                 {
        //                     new ViewDataChunk { Name="comment", Type="Spark.Tests.Models.Comment"},
        //                     new SendExpressionChunk {Code = "comment.Text", SilentNulls = false}
        //                 };
        //    compiler.CompileView(new[] { chunks }, new[] { chunks });
        //    Assert.That(compiler.SourceCode.Contains("catch(System.NullReferenceException)"));
        //}

        //[Test]
        //public void StrictNullUsesException()
        //{
        //    var compiler = new CSharpViewCompiler()
        //    {
        //        BaseClass = "Spark.Tests.Stubs.StubSparkView",
        //        NullBehaviour = NullBehaviour.Strict
        //    };
        //    var chunks = new Chunk[]
        //                 {
        //                     new ViewDataChunk { Name="comment", Type="Spark.Tests.Models.Comment"},
        //                     new SendExpressionChunk {Code = "comment.Text", SilentNulls = false}
        //                 };
        //    compiler.CompileView(new[] { chunks }, new[] { chunks });
        //    Assert.That(compiler.SourceCode.Contains("catch(System.NullReferenceException ex)"));
        //    Assert.That(compiler.SourceCode.Contains("ArgumentNullException("));
        //    Assert.That(compiler.SourceCode.Contains(", ex);"));
        //}

        [Test]
        public void PageBaseTypeOverridesBaseClass()
        {
            var compiler = new OxygeneViewCompiler()
            {
                BaseClass = "Spark.Tests.Stubs.StubSparkView",
                NullBehaviour = NullBehaviour.Strict
            };
            DoCompileView(compiler, new Chunk[]
                                    {
                                        new PageBaseTypeChunk {  BaseClass="Spark.Tests.Stubs.StubSparkView2"},
                                        new SendLiteralChunk{ Text = "Hello world"}
                                    });
            var instance = compiler.CreateInstance();
            Assert.That(instance, Is.InstanceOfType(typeof(StubSparkView2)));
        }


        [Test]
        public void PageBaseTypeWorksWithOptionalModel()
        {
            var compiler = new OxygeneViewCompiler()
            {
                BaseClass = "Spark.Tests.Stubs.StubSparkView",
                NullBehaviour = NullBehaviour.Strict
            };
            DoCompileView(compiler, new Chunk[]
                                    {
                                        new PageBaseTypeChunk {BaseClass = "Spark.Tests.Stubs.StubSparkView2"},
                                        new ViewDataModelChunk {TModel = "Spark.Tests.Models.Comment"},
                                        new SendLiteralChunk {Text = "Hello world"}
                                    });
            var instance = compiler.CreateInstance();
            Assert.That(instance, Is.InstanceOfType(typeof(StubSparkView2)));
            Assert.That(instance, Is.InstanceOfType(typeof(StubSparkView2<Comment>)));
        }

        [Test]
        public void PageBaseTypeWorksWithGenericParametersIncluded()
        {
            var compiler = new OxygeneViewCompiler()
            {
                BaseClass = "Spark.Tests.Stubs.StubSparkView",
                NullBehaviour = NullBehaviour.Strict
            };
            DoCompileView(compiler, new Chunk[]
                                    {
                                        new PageBaseTypeChunk {BaseClass = "Spark.Tests.Stubs.StubSparkView3<Spark.Tests.Models.Comment, string>"},
                                        new SendLiteralChunk {Text = "Hello world"}
                                    });
            var instance = compiler.CreateInstance();
            Assert.That(instance, Is.InstanceOfType(typeof(StubSparkView2)));
            Assert.That(instance, Is.InstanceOfType(typeof(StubSparkView2<Comment>)));
            Assert.That(instance, Is.InstanceOfType(typeof(StubSparkView3<Comment, string>)));
        }
 
    }
}