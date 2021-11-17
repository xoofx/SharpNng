// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using CppAst;
using CppAst.CodeGen.Common;
using CppAst.CodeGen.CSharp;
using Zio.FileSystems;

namespace SharpNng.CodeGen
{
    /// <summary>
    /// Programs that generate the P/Invoke for SharpNng.
    /// </summary>
    class Program
    {
        private static readonly string NngFolder = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\..\ext\nng"));

        static void Main(string[] args)
        {
            var program = new Program();
            program.GeneratePInvoke();
        }

        /// <summary>
        /// Generates the PInvoke layer from nng C header files.
        /// </summary>
        public void GeneratePInvoke()
        {
            var srcFolder = Path.Combine(NngFolder, "include");
            var destFolder = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\..\nng.NET"));

            if (!Directory.Exists(srcFolder))
            {
                throw new DirectoryNotFoundException($"The source folder `{srcFolder}` doesn't exist");
            }
            if (!Directory.Exists(destFolder))
            {
                throw new DirectoryNotFoundException($"The destination folder `{destFolder}` doesn't exist");
            }

            var csOptions = new CSharpConverterOptions()
            {
                DefaultClassLib = "nng",
                DefaultNamespace = "nng",
                DefaultOutputFilePath = "/nng.generated.cs",
                DefaultDllImportNameAndArguments = "nngDll",
                GenerateAsInternal = false,
                DispatchOutputPerInclude = false,
                DefaultMarshalForString = new CSharpMarshalAttribute(CSharpUnmanagedKind.CustomMarshaler) { MarshalTypeRef = "typeof(FastUtf8StringMarshaller)" },
                Defines =
                {
                    "NNG_SHARED_LIB",
                    "NNG_ELIDE_DEPRECATED",
                },
                MappingRules =
                {
                    e => e.Map<CppFunction>("nng_aio_alloc").Discard(),
                    e => e.Map<CppFunction>("nng_http_handler_alloc").Discard(),
                    e => e.Map<CppFunction>("nng_http_handler_set_data").Discard(),
                    e => e.Map<CppFunction>("nng_thread_create").Discard(),
                    e => e.MapMacroToConst("NNG_DURATION_.*", "int"),
                    e => e.MapMacroToConst("NNG_FLAG_.*", "int"),
                    e => e.MapMacroToConst("NNG_OPT_.*", "char*"),
                }
            };
            csOptions.IncludeFolders.Add(srcFolder);

            var files = Directory.GetFiles(srcFolder, "*.h", SearchOption.AllDirectories).Where(x => !x.Contains("compat") && !x.Contains("supplemental")).ToList();

            var csCompilation = CSharpConverter.Convert(files, csOptions);

            if (csCompilation.HasErrors)
            {
                foreach (var message in csCompilation.Diagnostics.Messages)
                {
                    Console.Error.WriteLine(message);
                }
                Console.Error.WriteLine("Unexpected parsing errors");
                Environment.Exit(1);
            }

            // Remove the generated namespace
            var csFile = csCompilation.Members.OfType<CSharpGeneratedFile>().First();
            var csNamespace = csFile.Members.OfType<CSharpNamespace>().First();

            var usings = csNamespace.Members.OfType<CSharpUsingDeclaration>().ToList();
            foreach (var use  in usings)
            {
                csNamespace.Members.Remove(use);
            }

            var nngClass = csNamespace.Members.OfType<CSharpClass>().First();
            csNamespace.Members.Remove(nngClass);
            csFile.Members.Remove(csNamespace);

            foreach (var use in usings)
            {
                csFile.Members.Add(use);
            }
            csFile.Members.Add(nngClass);

            // Use proper string marshaller
            foreach (var member in nngClass.Members.OfType<CSharpMethod>())
            {
                if (member.ReturnType is CSharpTypeWithAttributes typeWithAttribute && typeWithAttribute.ElementType is CSharpPrimitiveType primitiveType && primitiveType.Kind == CSharpPrimitiveKind.String)
                {
                    var marshalAttribute = typeWithAttribute.Attributes.OfType<CSharpMarshalAttribute>().First();
                    marshalAttribute.UnmanagedType = CSharpUnmanagedKind.CustomMarshaler;
                    marshalAttribute.MarshalTypeRef = "typeof(ReturnUtf8StringMarshaller)";
                }

            }
            nngClass.Attributes.Add(new CSharpFreeAttribute("System.Security.SuppressUnmanagedCodeSecurity"));

            // Make record struct for all structs postfix with `_s`
            foreach (var member in nngClass.Members.OfType<CSharpStruct>())
            {
                if (member.Name.EndsWith("_s"))
                {
                    member.IsRecord = true;
                }
            }

            // Write generated file back to disk
            var fs = new PhysicalFileSystem();
            {
                var subfs = new SubFileSystem(fs, fs.ConvertPathFromInternal(destFolder));
                var codeWriter = new CodeWriter(new CodeWriterOptions(subfs));
                csCompilation.DumpTo(codeWriter);
            }
        }
    }
}
