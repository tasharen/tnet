#if !MODDING && RUNTIME_CODE
using Mono.CSharp;
using UnityEngine;
using System.Reflection;

namespace TNet
{
	/// <summary>
	/// Run-time code execution. Uses https://github.com/aeroson/mcs-ICodeCompiler (included)
	/// </summary>

	static public class RuntimeCode
	{
		/// <summary>
		/// Set to 'true' before executing code, then 'false' after code has been executed.
		/// </summary>

		[System.NonSerialized] static internal bool isExecuting = false;

		[System.NonSerialized] static int mLastAssemblyHash = 0;
		[System.NonSerialized] static Assembly[] mCachedAssemblies = null;
		[System.NonSerialized] static Sanitize mRuntimeSanitize;
		public delegate void Sanitize (ref string code);

		/// <summary>
		/// Execute the code within the specified file.
		/// </summary>

		static public object ExecuteFile (string path)
		{
			string code = Tools.ReadTextFile(path);
			if (!string.IsNullOrEmpty(code)) return Execute(code);
#if UNITY_EDITOR
			Debug.LogError("Can't open " + path);
#endif
			return null;
		}

		[System.NonSerialized] static Evaluator mEval;

		class CustomReportPrinter : ReportPrinter
		{
			public new int ErrorsCount { get; protected set; }

			public new int WarningsCount { get; private set; }

			public CustomReportPrinter () { }

			public override void Print (AbstractMessage msg, bool showFullPath)
			{
				if (msg.IsWarning)
				{
					++WarningsCount;
				}
				else
				{
					++ErrorsCount;
					Debug.LogError(msg.Text);
				}
			}

		}

		/// <summary>
		/// Add a new function to sanitize the input code.
		/// </summary>

		static public void AddRuntimeSanitizeFunction (Sanitize func) { mRuntimeSanitize += func; }

		/// <summary>
		/// Remove a previously added function to sanitize the input code.
		/// </summary>

		static public void RemoveRuntimeSanitizeFunction (Sanitize func) { mRuntimeSanitize -= func; }

		/// <summary>
		/// Execute the specified code.
		/// </summary>

		static public object Execute (string code)
		{
			if (mEval == null || TypeExtensions.assemblyHash != mLastAssemblyHash)
			{
				var cs = new CompilerSettings();
				var reporter = new CustomReportPrinter();
				var context = new CompilerContext(cs, reporter);
				mEval = new Evaluator(context);
				mCachedAssemblies = TypeExtensions.GetAssemblies(false);
				mLastAssemblyHash = TypeExtensions.assemblyHash;

				for (int i = 0, imax = mCachedAssemblies.Length; i < imax; ++i)
				{
					var assembly = mCachedAssemblies[i];
					try { mEval.ReferenceAssembly(assembly); }
					catch { }
				}

				isExecuting = true;
				{
					var sb = new System.Text.StringBuilder("");
					sb.AppendLine("using UnityEngine;");
					sb.AppendLine("using TNet;");
					mEval.Compile(sb.ToString());
				}
				isExecuting = false;
			}

			if (string.IsNullOrEmpty(code)) return null;
			if (code[code.Length - 1] != ';') code += ";";

			object result = null;
			bool result_set = false;
			isExecuting = true;

			// Explicitly disallow delegates as these can persist from one world to another
			if (code.Contains("delegate") ||
				code.Contains("Action") ||
				code.Contains(".Reflection") ||
				code.Contains(".Sockets") ||
				code.Contains(".IO") ||
				code.Contains("=>") ||
				code.Contains("CSharpCompiler") ||
				code.Contains("Assembly")
#if !UNITY_EDITOR
				|| code.Contains("GetProperty")
				|| code.Contains("GetValue")
				|| code.Contains("GetMethod")
#endif
				)
			{
				code = "Debug.LogError(\"Invalid input\");";
			}

			try
			{
				if (mRuntimeSanitize != null) mRuntimeSanitize(ref code);
				string s = mEval.Evaluate("{\n" + code + "\n}", out result, out result_set);
				if (!result_set && !string.IsNullOrEmpty(s)) Debug.LogError("Syntax error: " + s);
			}
			catch (System.Exception) { }

			isExecuting = false;
			return result_set ? result : null;
		}

		/// <summary>
		/// Compile and add a new assembly to the game. All the classes and types within will be usable as if they were a part of the base game.
		/// For example TypeExtensions.GetType("name") will find your type by name.
		/// </summary>

		static public Assembly Add (params string[] code)
		{
			var asm = Compile(code);
			if (asm != null) TypeExtensions.AddAssembly(asm);
			return asm;
		}

		/// <summary>
		/// Error message, set if the assembly fails to compile.
		/// </summary>

		static public string error = null;

		/// <summary>
		/// This function lets you compile run-time code files into a separate assembly that only exists in memory.
		/// This assembly will not be added to the list of game assemblies. Call RuntimeCode.Add instead if you want it to be added.
		/// </summary>

		static public Assembly Compile (params string[] code)
		{
			error = null;

			try
			{
				foreach (var s in code)
				{
					if (s.Contains(".Reflection") || s.Contains(".IO") || s.Contains(".Sockets"))
					{
						error = "Reflection, IO and Sockets are disabled for security reasons";
						return null;
					}
				}

				var compiler = new CSharpCompiler.CodeCompiler();
				var param = new System.CodeDom.Compiler.CompilerParameters() { GenerateInMemory = true, GenerateExecutable = false };
				var assemblies = TypeExtensions.GetAssemblies();

				foreach (var asm in assemblies)
				{
					try
					{
						var loc = asm.Location;
						param.ReferencedAssemblies.Add(loc);
					}
					catch (System.Exception) { }
				}

				var result = compiler.CompileAssemblyFromSourceBatch(param, code);
				var errors = result.Errors;

				if (errors != null && errors.Count > 0)
				{
					var msg = new System.Text.StringBuilder();
					foreach (var error in errors) msg.AppendLine(error.ToString());
					error = msg.ToString();
					return null;
				}
				return result.CompiledAssembly;
			}
			catch (System.Exception ex)
			{
				error = ex.Message + "\n" + ex.StackTrace;
			}
			return null;
		}
	}
}
#else
using System.Reflection;

namespace TNet
{
	/// <summary>
	/// Run-time code execution. Uses https://github.com/aeroson/mcs-ICodeCompiler (included)
	/// </summary>

	static public class RuntimeCode
	{
		/// <summary>
		/// Set to 'true' before executing code, then 'false' after code has been executed.
		/// </summary>

		static internal bool isExecuting = false;

		/// <summary>
		/// Execute the code within the specified file.
		/// </summary>

		static public object ExecuteFile (string path) { return null; }

		static Sanitize mRuntimeSanitize;
		public delegate void Sanitize (ref string code);

		/// <summary>
		/// Add a new function to sanitize the input code.
		/// </summary>

		static public void AddRuntimeSanitizeFunction (Sanitize func) { mRuntimeSanitize += func; }

		/// <summary>
		/// Remove a previously added function to sanitize the input code.
		/// </summary>

		static public void RemoveRuntimeSanitizeFunction (Sanitize func) { mRuntimeSanitize -= func; }

		/// <summary>
		/// Execute the specified code.
		/// </summary>

		static public object Execute (string code)
		{
			error = "Enable RUNTIME_CODE #define in order to use run-time code compilation";
#if UNITY_EDITOR
			UnityEngine.Debug.LogError(error);
#endif
			return null;
		}

		/// <summary>
		/// Compile and add a new assembly to the game. All the classes and types within will be usable as if they were a part of the base game.
		/// For example TypeExtensions.GetType("name") will find your type by name.
		/// </summary>

		static public Assembly Add (params string[] code) { return null; }

		/// <summary>
		/// Error message, set if the assembly fails to compile.
		/// </summary>

		static public string error = null;

		/// <summary>
		/// This function lets you compile run-time code files into a separate assembly that only exists in memory.
		/// This assembly will not be added to the list of game assemblies. Call RuntimeCode.Add instead if you want it to be added.
		/// </summary>

		static public Assembly Compile (params string[] code)
		{
			error = "Enable RUNTIME_CODE #define in order to use run-time code compilation";
#if UNITY_EDITOR
			UnityEngine.Debug.LogError(error);
#endif
			return null;
		}
	}
}
#endif