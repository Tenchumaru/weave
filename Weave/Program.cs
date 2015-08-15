using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace Weave
{
    static partial class Program
    {
        public static string InputFilePath { get { return inputFilePath; } }
        public static bool IsFailing { get { return isFailing; } }
        public static bool HasFailed { get; set; }
        static string inputFilePath;
        static bool isRemoving;
        static bool isFailing;
        static string preInvokeMethodName;
        static string postInvokeMethodName;
        static SymbolReader symbolReader;
        static ModuleDefinition module;
        static IEnumerable<ModuleDefinition> modules;
        static DefaultAssemblyResolver resolver;

        static Program()
        {
            // TODO:  http://blogs.msdn.com/b/microsoft_press/archive/2010/02/03/jeffrey-richter-excerpt-2-from-clr-via-c-third-edition.aspx
            // This is in ..\..\EmbeddedAssemblyResolver.cs but might need updating.
            // The previous implementation used a post-build event to invoke ILMerge.
            // IF "$(ConfigurationName)" == "Release" ILMerge "/targetplatform:v4,%25ProgramFiles%25\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0\Profile\Client" /out:\local\bin\weave.exe "$(TargetPath)" "$(TargetDir)Adrezdi.Windows.dll" "$(TargetDir)Mono.Cecil.dll" "$(TargetDir)Mono.Cecil.Pdb.dll"
        }

        static void Main(string[] args)
        {
            var arguments = new Arguments();
            try
            {
                if(args.Length < 1)
                    arguments.Usage();
                arguments.Parse(args);
                isRemoving = arguments.IsRemoving;
                isFailing = arguments.IsFailing;
                preInvokeMethodName = arguments.PreInvokeMethod;
                postInvokeMethodName = arguments.PostInvokeMethod;
                inputFilePath = arguments.FilePaths[0];
                var outputFilePath = arguments.FilePaths.Count > 1 ? arguments.FilePaths[1] : inputFilePath;
                resolver = (DefaultAssemblyResolver)GlobalAssemblyResolver.Instance;
                string inputFolderPath = Path.GetDirectoryName(inputFilePath);
                resolver.RemoveSearchDirectory(".");
                resolver.RemoveSearchDirectory("bin");
                var searchFolders = new List<string>();
                searchFolders.Add(inputFolderPath);
                searchFolders.AddRange(arguments.SearchFolderPaths);
                searchFolders.AddRange(arguments.Assemblies.Select(s => Path.GetDirectoryName(s)));
                if(arguments.FrameworkFolder != null)
                    searchFolders.Add(arguments.FrameworkFolder);
                else
                {
                    var nakedModule = ModuleDefinition.ReadModule(inputFilePath);
                    var assemblyReference = nakedModule.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib");
                    if(assemblyReference != null)
                    {
                        if(Enumerable.SequenceEqual(assemblyReference.PublicKeyToken, new byte[] { 0x7c, 0xec, 0x85, 0xd7, 0xbe, 0xa7, 0x79, 0x8e }))
                        {
                            // This is Silverlight.
                            string programFilesFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                            string silverlightFolder = Path.Combine(programFilesFolder, "Microsoft Silverlight");
                            silverlightFolder = Directory.GetDirectories(silverlightFolder).OrderByDescending(s => s).First();
                            searchFolders.Add(silverlightFolder);
                            // The following also works.
                            //searchFolders.Add(@"C:\Program Files\Reference Assemblies\Microsoft\Framework\Silverlight\v4.0");
                        }
                        else
                        {
                            // Assume it's the full .NET framework.
                            // TODO:  handle WP7.
                            string programFilesFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                            string frameworkFolder = Path.Combine(programFilesFolder, @"Reference Assemblies\Microsoft\Framework\.NETFramework");
                            frameworkFolder = Directory.GetDirectories(frameworkFolder).OrderByDescending(s => s).First();
                            searchFolders.Add(frameworkFolder);
                        }
                    }
                }
                searchFolders = searchFolders.Select(s => Path.GetFullPath(s).ToLowerInvariant()).Distinct().ToList();
                searchFolders.ForEach(s => resolver.AddSearchDirectory(s));
                module = ReadModule(inputFilePath, resolver);
                var q = from p in arguments.Assemblies
                        let n = Path.GetFileNameWithoutExtension(p)
                        select resolver.Resolve(n).MainModule;
                modules = new[] { module }.Concat(q).ToList();
                symbolReader = new SymbolReader(module);
                if(WeaveDependency() | WeaveNotify() | WeaveOnDemand() | WeaveInitialValue() | WeavePrePost() | WeaveXmlSerializable() | (arguments.IsRenaming && WeaveRename()))
                    WriteModule(module, outputFilePath);
            }
            catch(Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                Console.Error.Flush();
                Console.WriteLine();
                arguments.Usage(1);
            }
            if(HasFailed)
                Environment.ExitCode = 1;
        }

        static ModuleDefinition ReadModule(string fileName, DefaultAssemblyResolver resolver)
        {
            var parameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadingMode = ReadingMode.Immediate,
                ReadSymbols = true,
            };
            var module = ModuleDefinition.ReadModule(fileName, parameters);
            return module;
        }

        static void WriteModule(ModuleDefinition module, string fileName)
        {
            var parameters = new WriterParameters { WriteSymbols = true };
            module.Write(fileName, parameters);
        }

        static IEnumerable<TypeDefinition> GetAllTypes(IEnumerable<TypeDefinition> types)
        {
            var allTypes = new List<TypeDefinition>(types);
            var q = from t in types
                    from n in GetAllTypes(t.NestedTypes)
                    select n;
            allTypes.AddRange(q);
            return allTypes;
        }

        static bool IsNamed(this CustomAttribute attribute, string name)
        {
            return attribute.AttributeType.Name == name + "Attribute";
        }

        static MethodReference ResolveMethod(ModuleDefinition module, TypeReference type, string methodName, params string[] parameterTypeNames)
        {
            var def = type as TypeDefinition ?? type.Resolve();
            var q = from m in def.Methods
                    where m.Name == methodName
                    && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameterTypeNames)
                    select m;
            MethodReference method = q.SingleOrDefault();
            CheckWarning(method != null, string.Format("cannot resolve {0}.{1}", type.Name, methodName));
            method = module.Import(method);
            return method;
        }

        static MethodReference ResolveMethod(PropertyDefinition contextProperty, TypeReference type, string methodName, params string[] parameterTypeNames)
        {
            var def = type as TypeDefinition ?? type.Resolve();
            var q = from m in def.Methods
                    where m.Name == methodName
                    && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameterTypeNames)
                    select m;
            MethodReference method = q.SingleOrDefault();
            CheckWarning(method != null, string.Format("cannot resolve {0}.{1}", type.Name, methodName), contextProperty);
            method = contextProperty.Module.Import(method);
            return method;
        }

        static T ConvertTo<T>(this decimal me)
        {
            try
            {
                var result = (T)Convert.ChangeType(me, typeof(T));
                return me.Equals(Convert.ToDecimal(result)) ? result : default(T);
            }
            catch(OverflowException)
            {
                return default(T);
            }
        }

        static void CheckWarning(bool expression, string message)
        {
            if(!expression)
                throw new WarningException(inputFilePath, message);
        }

        static void CheckWarning(bool expression, string message, PropertyReference contextProperty)
        {
            if(!expression)
            {
                string filePathAndLine = symbolReader.FormatProperty(contextProperty.PropertyType.FullName, contextProperty.Name);
                throw new WarningException(filePathAndLine, message);
            }
        }

        static void CheckMethodWarning(bool expression, string message, string typeName, string methodName)
        {
            if(!expression)
            {
                string filePathAndLine = symbolReader.FormatMethod(typeName, methodName);
                throw new WarningException(filePathAndLine, message);
            }
        }

        static void CheckCompilerGenerated(PropertyDefinition property, MethodDefinition methodDefinition)
        {
            char ch = methodDefinition.IsGetter ? 'g' : 's';
            CheckWarning(methodDefinition != null, "no " + ch + "etter for " + property.Name, property);
            var attribute = methodDefinition.CustomAttributes.FirstOrDefault(c => c.IsNamed("CompilerGenerated"));
            CheckWarning(attribute != null, property.Name + ' ' + ch + "etter was not compiler-generated", property);
            if(isRemoving && attribute != null)
                methodDefinition.CustomAttributes.Remove(attribute);
        }
    }

    public class WarningException : Exception
    {
        public WarningException(string filePathAndLine, string message) : base(ConstructMessage(filePathAndLine, message)) { }

        public static string ConstructMessage(string filePathAndLine, string message)
        {
            Program.HasFailed |= Program.IsFailing;
            return filePathAndLine + ": " + (Program.IsFailing ? "error" : "warning") + ": " + message;
        }
    }
}
