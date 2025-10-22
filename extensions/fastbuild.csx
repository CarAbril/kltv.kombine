
/*---------------------------------------------------------------------------------------------------------

	Kombine FastBuild helper

	(C) Kollective Networks 2025

---------------------------------------------------------------------------------------------------------*/

using System.Linq;

public class FastBuildOptions {
	public string Executable { get; set; } = Host.IsWindows() ? "FBuild" : "fbuild";
	public bool EnableCache { get; set; } = false;
	public bool EnableDistribution { get; set; } = false;
	// CacheMode: r (read), w (write), rw (read-write)
	public string CacheMode { get; set; } = "rw";
	public string CachePath { get; set; } = string.Empty;
	public int WorkerConnectionLimit { get; set; } = 0;
	public bool Verbose { get; set; } = false;
	public bool ShowProgress { get; set; } = true;
	public KList AdditionalArgs { get; set; } = new KList();
}

internal static class FastBuildHelper {
	private static string NPath(string v) { return v.Replace("\\", "/"); }
	private static string Q(string s) { return "'" + s.Replace("'", "\''") + "'"; }

	public static KValue GenerateBffForExecutable(
		string title,
		KList sourcesC,
		KList sourcesCXX,
		KValue objectsOutDir,
		KList includeDirs,
		KList defines,
		KList switchesCC,
		KList switchesCXX,
		KList libDirs,
		KList libs,
		KList switchesLD,
		KValue linker,
		KValue cCompiler,
		KValue cxxCompiler,
		KValue output,
		bool shared,
		FastBuildOptions fbo,
		out string targetName
	){
		targetName = title;
		var sb = new System.Text.StringBuilder();
		sb.AppendLine(";===============================================================================");
		sb.AppendLine($"; FastBuild configuration - {title}");
		sb.AppendLine(";===============================================================================\n");
		// Settings
		if (!string.IsNullOrEmpty(fbo.CachePath)) {
			sb.AppendLine($"Settings {{ .CachePath = {Q(NPath(fbo.CachePath))} }}\n");
		} else {
			sb.AppendLine("Settings { .CachePath = '' }\n");
		}
		// Compilers
		sb.AppendLine($"Compiler('Clang-C') {{ .Executable = {Q(NPath(cCompiler))} }}");
		sb.AppendLine($"Compiler('Clang-CXX') {{ .Executable = {Q(NPath(cxxCompiler))} }}\n");
		// Build compile options
		string incs = string.Join(" ", includeDirs.Select(i => "-I" + NPath(i)));
		string defs = string.Join(" ", defines.Select(d => "-D" + d));
		string scc = string.Join(" ", switchesCC.Select(s => s.ToString()));
		string scxx = string.Join(" ", switchesCXX.Select(s => s.ToString()));
		// No -MMD here (FastBuild manages dependencies)
		string cOpts = $"{incs} {defs} {scc} -c \"%1\" -o \"%2\"".Trim();
		string cxxOpts = $"{incs} {defs} {scxx} -c \"%1\" -o \"%2\"".Trim();
		// Object lists
		bool anyObj = false;
		if (sourcesC.Count() != 0) {
			anyObj = true;
			sb.AppendLine("ObjectList('objects-c')");
			sb.AppendLine("{");
			sb.AppendLine($"    .CompilerInputFiles = {{ {string.Join(", ", sourcesC.Select(s => Q(NPath(s))))} }}");
			sb.AppendLine($"    .CompilerOutputPath = {Q(NPath(objectsOutDir))}");
			sb.AppendLine("    .Compiler = 'Clang-C'");
			sb.AppendLine($"    .CompilerOptions = {Q(cOpts)}");
			sb.AppendLine("}\n");
		}
		if (sourcesCXX.Count() != 0) {
			anyObj = true;
			sb.AppendLine("ObjectList('objects-cxx')");
			sb.AppendLine("{");
			sb.AppendLine($"    .CompilerInputFiles = {{ {string.Join(", ", sourcesCXX.Select(s => Q(NPath(s))))} }}");
			sb.AppendLine($"    .CompilerOutputPath = {Q(NPath(objectsOutDir))}");
			sb.AppendLine("    .Compiler = 'Clang-CXX'");
			sb.AppendLine($"    .CompilerOptions = {Q(cxxOpts)}");
			sb.AppendLine("}\n");
		}
		// Linker
		string ldirs = string.Join(" ", libDirs.Select(l => "-L" + NPath(l)));
		string llibs = string.Join(" ", libs.Select(l => "-l" + l));
		string sld = string.Join(" ", switchesLD.Select(s => s.ToString()));
		if (shared) sld = (sld + " -shared").Trim();
		string lopts = $"{ldirs} {llibs} -fuse-ld=lld {sld} \"%1\" -o \"%2\"".Trim();
		string exename = shared ? "link-shared" : "link-exe";
		if (shared) {
			sb.AppendLine($"Executable('{exename}')");
			sb.AppendLine("{");
			sb.AppendLine($"    .Linker = {Q(NPath(linker))}");
			sb.AppendLine($"    .LinkerOutput = {Q(NPath(output))}");
			sb.AppendLine($"    .LinkerOptions = {Q(lopts)}");
			string libsRef = anyObj ? "'objects-cxx', 'objects-c'" : "";
			sb.AppendLine($"    .Libraries = {{ {libsRef} }}");
			sb.AppendLine("}\n");
		} else {
			sb.AppendLine($"Executable('{exename}')");
			sb.AppendLine("{");
			sb.AppendLine($"    .Linker = {Q(NPath(linker))}");
			sb.AppendLine($"    .LinkerOutput = {Q(NPath(output))}");
			sb.AppendLine($"    .LinkerOptions = {Q(lopts)}");
			string libsRef = anyObj ? "'objects-cxx', 'objects-c'" : "";
			sb.AppendLine($"    .Libraries = {{ {libsRef} }}");
			sb.AppendLine("}\n");
		}
		sb.AppendLine($"Alias('all') {{ .Targets = {{ '{exename}' }} }}");
		return sb.ToString();
	}

	public static KValue GenerateBffForStaticLib(
		string title,
		KList sourcesC,
		KList sourcesCXX,
		KValue objectsOutDir,
		KList includeDirs,
		KList defines,
		KList switchesCC,
		KList switchesCXX,
		KValue librarian,
		KValue output,
		FastBuildOptions fbo,
		out string targetName
	){
		targetName = title;
		var sb = new System.Text.StringBuilder();
		sb.AppendLine(";===============================================================================");
		sb.AppendLine($"; FastBuild configuration - {title}");
		sb.AppendLine(";===============================================================================\n");
		// Settings
		if (!string.IsNullOrEmpty(fbo.CachePath)) {
			sb.AppendLine($"Settings {{ .CachePath = {Q(NPath(fbo.CachePath))} }}\n");
		} else {
			sb.AppendLine("Settings { .CachePath = '' }\n");
		}
		// Compilers - use 'clang' and 'clang++' directly (FastBuild will find them in PATH)
		sb.AppendLine($"Compiler('Clang-C') {{ .Executable = 'clang' }}");
		sb.AppendLine($"Compiler('Clang-CXX') {{ .Executable = 'clang++' }}\n");
		// Build compile options
		string incs = string.Join(" ", includeDirs.Select(i => "-I" + NPath(i)));
		string defs = string.Join(" ", defines.Select(d => "-D" + d));
		string scc = string.Join(" ", switchesCC.Select(s => s.ToString()));
		string scxx = string.Join(" ", switchesCXX.Select(s => s.ToString()));
		string cOpts = $"{incs} {defs} {scc} -c \"%1\" -o \"%2\"".Trim();
		string cxxOpts = $"{incs} {defs} {scxx} -c \"%1\" -o \"%2\"".Trim();
		
		// Object lists - track which ones we create
		var objectListNames = new System.Collections.Generic.List<string>();
		
		if (sourcesC.Count() != 0) {
			sb.AppendLine("ObjectList('objects-c')");
			sb.AppendLine("{");
			sb.AppendLine($"    .CompilerInputFiles = {{ {string.Join(", ", sourcesC.Select(s => Q(NPath(s))))} }}");
			sb.AppendLine($"    .CompilerOutputPath = {Q(NPath(objectsOutDir))}");
			sb.AppendLine("    .Compiler = 'Clang-C'");
			sb.AppendLine($"    .CompilerOptions = {Q(cOpts)}");
			sb.AppendLine("}\n");
			objectListNames.Add("'objects-c'");
		}
		if (sourcesCXX.Count() != 0) {
			sb.AppendLine("ObjectList('objects-cxx')");
			sb.AppendLine("{");
			sb.AppendLine($"    .CompilerInputFiles = {{ {string.Join(", ", sourcesCXX.Select(s => Q(NPath(s))))} }}");
			sb.AppendLine($"    .CompilerOutputPath = {Q(NPath(objectsOutDir))}");
			sb.AppendLine("    .Compiler = 'Clang-CXX'");
			sb.AppendLine($"    .CompilerOptions = {Q(cxxOpts)}");
			sb.AppendLine("}\n");
			objectListNames.Add("'objects-cxx'");
		}
		
		// Librarian - only reference ObjectLists that were actually created
		sb.AppendLine("Library('lib-target')");
		sb.AppendLine("{");
		// Determine which compiler to use based on what we have
		string compilerName = objectListNames.Count > 0 ? 
			(objectListNames.Contains("'objects-cxx'") ? "'Clang-CXX'" : "'Clang-C'") : 
			"'Clang-C'";
		string compilerOptions = objectListNames.Count > 0 ? 
			(objectListNames.Contains("'objects-cxx'") ? Q(cxxOpts) : Q(cOpts)) : 
			Q(cOpts);
		sb.AppendLine($"    .Compiler = {compilerName}");
		sb.AppendLine($"    .CompilerOptions = {compilerOptions}");
		sb.AppendLine($"    .CompilerOutputPath = {Q(NPath(objectsOutDir))}");
		sb.AppendLine($"    .Librarian = {Q(NPath(librarian))}");
		sb.AppendLine($"    .LibrarianOutput = {Q(NPath(output))}");
		sb.AppendLine($"    .LibrarianOptions = 'rcs \"%2\" \"%1\"'");
		
		// Only add LibrarianAdditionalInputs if we have object lists
		if (objectListNames.Count > 0) {
			string inputs = string.Join(", ", objectListNames);
			sb.AppendLine($"    .LibrarianAdditionalInputs = {{ {inputs} }}");
		}
		sb.AppendLine("}\n");
		sb.AppendLine("Alias('all') { .Targets = { 'lib-target' } }");
		return sb.ToString();
	}

	public static ToolResult RunFastBuild(KValue bffPath, string alias, FastBuildOptions fbo, uint concurrency, bool verbose, bool abortWhenFailed) {
		Tool tool = new Tool("FastBuild");
    	string args = $"-config \"{bffPath}\" {alias}";
		if (fbo.EnableCache) {
			if (fbo.CacheMode == "r") args += " -cacheReadOnly";
			else if (fbo.CacheMode == "w") args += " -cacheWriteOnly";
			else args += " -cache";
		}
		if (fbo.EnableDistribution) args += " -dist";
		if (fbo.WorkerConnectionLimit > 0) args += $" -distlimit {fbo.WorkerConnectionLimit}";
		if (fbo.ShowProgress) args += " -progress"; else args += " -quiet";
		if (fbo.Verbose || verbose) args += " -verbose";
		if (concurrency > 0) args += $" -j{concurrency}";
		if (fbo.AdditionalArgs.Count() > 0) args += " " + fbo.AdditionalArgs.Flatten();
		Msg.Print($"FastBuild: {fbo.Executable} {args}");
		ToolResult res = tool.CommandSync(fbo.Executable, args, bffPath);
		if (abortWhenFailed && res.Status == ToolStatus.Failed) {
			Msg.PrintError("FastBuild execution failed.");
			Msg.PrintError("- Ensure FastBuild is installed: https://www.fastbuild.org/docs/quickstartguide.html");
			Msg.PrintError("- Configure clang.Options.FastBuildOptions.Executable to the FBuild binary path");
			Msg.PrintError("- Or disable FastBuild: clang.Options.UseFastBuildMode = FastBuildMode.Disabled");
			Msg.PrintAndAbort("Error: FastBuild failed.");
		}
		return res;
	}

	public static bool IsFastBuildInstalled(FastBuildOptions? fbo = null, bool verbose = false) {
		FastBuildOptions opts = fbo ?? new FastBuildOptions();
		Tool tool = new Tool("FastBuild");
		string args = "-version";
		if (verbose) {
			Msg.Print($"Probing FastBuild: {opts.Executable} {args}");
		}
		ToolResult res = tool.CommandSync(opts.Executable, args, "fastbuild-version");
		// Consider installed if the process could be executed and didn't hard-fail
		bool ok = res.Status != ToolStatus.Failed;
		if (verbose) {
			if (ok) {
				foreach (string s in res.Stdout) { if (!string.IsNullOrWhiteSpace(s)) Msg.Print(s); }
				foreach (string s in res.Stderr) { if (!string.IsNullOrWhiteSpace(s)) Msg.PrintWarning(s); }
			} else {
				Msg.PrintWarning("FastBuild not found or failed to execute.");
			}
		}
		return ok;
	}
}

public static class FastBuildInstall {
	// Convenience global helper to probe FastBuild without needing to reference options
	public static bool IsFastBuildInstalled() => FastBuildHelper.IsFastBuildInstalled(null, false);
}

internal static partial class FastBuildHelperExtensions {}