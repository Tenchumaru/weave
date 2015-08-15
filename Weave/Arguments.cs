using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Adrezdi;

namespace Weave
{
	public class Arguments
	{
		[CommandLine.OptionalValueArgument(LongName = "framework", ShortName = 'f', Usage = "the path to the framework folder")]
		public string FrameworkFolder { get; set; }
		[CommandLine.FlagArgument(LongName = "remove", ShortName = 'a', Usage = "remove weave-invoking attributes")]
		public bool IsRemoving { get; set; }
		[CommandLine.FlagArgument(LongName = "rename", ShortName = 'r', Usage = "perform renaming of private members")]
		public bool IsRenaming { get; set; }
		[CommandLine.FlagArgument(LongName = "error", ShortName = 'w', Usage = "consider warnings as errors")]
		public bool IsFailing { get; set; }
		[CommandLine.OptionalValueArgument(LongName = "search-folders", ShortName = 's', Usage = "additional assembly search folders")]
		public string SearchPathsString { get; set; }
		[CommandLine.OptionalValueArgument(LongName = "assemblies", ShortName = 'm', Usage = "additional assemblies")]
		public string AssembliesString { get; set; }
		[CommandLine.OptionalValueArgument(LongName = "pre-invoke", ShortName = 'e', Usage = "pre-pend a method invocation")]
		public string PreInvokeMethod { get; set; }
		[CommandLine.OptionalValueArgument(LongName = "post-invoke", ShortName = 'o', Usage = "post-pend a method invocation")]
		public string PostInvokeMethod { get; set; }
		public List<string> FilePaths { get; set; }
		public IEnumerable<string> SearchFolderPaths { get; private set; }
		public IEnumerable<string> Assemblies { get; private set; }

		public void Parse(string[] args)
		{
			var commandLine = new CommandLine();
			var arguments = commandLine.Parse<Arguments>(args, automatingUsage: true);
			FrameworkFolder = arguments.FrameworkFolder;
			IsFailing = arguments.IsFailing;
			IsRemoving = arguments.IsRemoving;
			IsRenaming = arguments.IsRenaming;
			PreInvokeMethod = arguments.PreInvokeMethod;
			PostInvokeMethod = arguments.PostInvokeMethod;
			SearchFolderPaths = !string.IsNullOrWhiteSpace(arguments.SearchPathsString)
				? arguments.SearchPathsString.Split(',', ';').ToArray() : new string[0];
			Assemblies = !string.IsNullOrWhiteSpace(arguments.AssembliesString)
				? arguments.AssembliesString.Split(',', ';').ToArray() : new string[0];
			FilePaths = commandLine.ExtraArguments.ToList();
		}

		public void Usage(int exitCode = 2)
		{
			var commandLine = new CommandLine();
			string usage = commandLine.Usage<Arguments>("inputDllFilePath [outputDllFilePath]");
			Console.Write(usage);
			Environment.Exit(exitCode);
		}
	}
}
