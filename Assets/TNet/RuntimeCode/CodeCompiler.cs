#if !MODDING && RUNTIME_CODE

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection.Emit;
using System.Text;
using Mono.CSharp;

namespace CSharpCompiler
{
	public class CodeCompiler : ICodeCompiler
	{
		static long assemblyCounter = 0;

		public CompilerResults CompileAssemblyFromDom (CompilerParameters options, CodeCompileUnit compilationUnit) { throw new NotImplementedException("This function is a stub and is not implemented"); }

		public CompilerResults CompileAssemblyFromDomBatch (CompilerParameters options, CodeCompileUnit[] ea) { throw new NotImplementedException("This function is a stub and is not implemented"); }

		/// <summary>
		/// Compiles an assembly from the source code contained within the specified file, using the specified compiler settings.
		/// </summary>

		public CompilerResults CompileAssemblyFromFile (CompilerParameters options, string fileName) { return CompileAssemblyFromFileBatch(options, new[] { fileName }); }

		/// <summary>
		/// Compiles an assembly from the source code contained within the specified files, using the specified compiler settings.
		/// </summary>

		public CompilerResults CompileAssemblyFromFileBatch (CompilerParameters options, string[] fileNames)
		{
			var settings = ParamsToSettings(options);

			foreach (var fileName in fileNames)
			{
				string path = Path.GetFullPath(fileName);
				var unit = new SourceFile(fileName, path, settings.SourceFiles.Count + 1);
				settings.SourceFiles.Add(unit);
			}

			return CompileFromCompilerSettings(settings, options.GenerateInMemory);
		}

		/// <summary>
		/// Compiles an assembly from the specified string containing source code, using the specified compiler settings.
		/// </summary>

		public CompilerResults CompileAssemblyFromSource (CompilerParameters options, string source) { return CompileAssemblyFromSourceBatch(options, new[] { source }); }

		/// <summary>
		/// Compiles an assembly from the specified array of strings containing source code, using the specified compiler settings.
		/// </summary>

		public CompilerResults CompileAssemblyFromSourceBatch (CompilerParameters options, string[] sources)
		{
			var settings = ParamsToSettings(options);

			int i = 0;

			foreach (var s in sources)
			{
				var source = s;
				Func<Stream> getStream = () => { return new MemoryStream(Encoding.UTF8.GetBytes(source ?? "")); };
				var fileName = i.ToString();
				var unit = new SourceFile(fileName, fileName, settings.SourceFiles.Count + 1, getStream);
				settings.SourceFiles.Add(unit);
				i++;
			}

			return CompileFromCompilerSettings(settings, options.GenerateInMemory);
		}

		CompilerResults CompileFromCompilerSettings (CompilerSettings settings, bool generateInMemory)
		{
			var compilerResults = new CompilerResults(new TempFileCollection(Path.GetTempPath()));
			var driver = new CustomDynamicDriver(new CompilerContext(settings, new CustomReportPrinter(compilerResults)));

			AssemblyBuilder outAssembly = null;
			
			try
			{
				driver.Compile(out outAssembly, AppDomain.CurrentDomain, generateInMemory);
			}
			catch (Exception e)
			{
				compilerResults.Errors.Add(new CompilerError()
				{
					IsWarning = false,
					ErrorText = e.Message,
				});
			}
			compilerResults.CompiledAssembly = outAssembly;

			return compilerResults;
		}

		CompilerSettings ParamsToSettings (CompilerParameters parameters)
		{
			var settings = new CompilerSettings();

			foreach (var assembly in parameters.ReferencedAssemblies) settings.AssemblyReferences.Add(assembly);

			settings.Encoding = Encoding.UTF8;
			settings.GenerateDebugInfo = parameters.IncludeDebugInformation;
			settings.MainClass = parameters.MainClass;
			settings.Platform = Platform.AnyCPU;
			settings.StdLibRuntimeVersion = RuntimeVersion.v4;

			if (parameters.GenerateExecutable)
			{
				settings.Target = Target.Exe;
				settings.TargetExt = ".exe";
			}
			else
			{
				settings.Target = Target.Library;
				settings.TargetExt = ".dll";
			}
			if (parameters.GenerateInMemory) settings.Target = Target.Library;

			if (string.IsNullOrEmpty(parameters.OutputAssembly))
			{
				parameters.OutputAssembly = settings.OutputFile = "DynamicAssembly_" + assemblyCounter + settings.TargetExt;
				assemblyCounter++;
			}
			
			settings.OutputFile = parameters.OutputAssembly;
			settings.Version = LanguageVersion.Default;
			settings.WarningLevel = parameters.WarningLevel;
			settings.WarningsAreErrors = parameters.TreatWarningsAsErrors;

			return settings;
		}
	}
}
#endif