/*---------------------------------------------------------------------------------------------------------

	Kombine Publishing Script

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/
#load "xml2md.csx"

// Other options to take into consideration
// -p:PublishReadyToRun=true 
// -p:PublishSingleFile=true -p:PublishReadyToRun=true
// -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true 

/// <summary>
/// Builds all the diferent packages for the tool
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int publish(string[] args){
	int ExitCode; 

	// Apply the version number
	// ------------------------------------------------------------------------------------------
	ApplyVersionNumber();

	// Windows
	// ------------------------------------------------------------------------------------------
	Msg.Print("Building for windows");
	ExitCode = Exec("dotnet","build -c Release -r win-x64",true);	
	if (ExitCode != 0)
		return ExitCode;	
	Msg.Print("Generate documentation from the XML generated into the doc folder");
	XmlToMarkdown.Convert("out/bin/win-x64/release/mkb.xml","doc/api.md");
	Msg.Print("Creating output folder.");
	Folders.Create("out/pkg");
	Msg.Print("Compress the reference assembly");
	Compress.Zip.CompressFile("out/bin/win-x64/release/ref/mkb.dll","out/pkg/kombine.ref.zip");
	Msg.Print("[Windows] Compress the unpacked tool");
	Compress.Zip.CompressFolder("out/bin/win-x64/release/","out/pkg/kombine.debug.win.zip",true,false);
	// Generate the single file package
	Msg.Print("[Windows] Generate the single file tool");
	ExitCode = Exec("dotnet","publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true",true);	
	if (ExitCode != 0)
		return ExitCode;	
	Msg.Print("[Windows] Compress the single file tool");		
	Compress.Zip.CompressFile("out/pub/win-x64/release/mkb.exe","out/pkg/kombine.win.zip");
	// Linux
	// ------------------------------------------------------------------------------------------
	Msg.Print("Building for windows");
	ExitCode = Exec("dotnet","build -c Release -r linux-x64",true);	
	if (ExitCode != 0)
		return ExitCode;	
	Msg.Print("[Linux] Compress the unpacked tool");
	Compress.Tar.CompressFolder("out/bin/linux-x64/release/","out/pkg/kombine.debug.lnx.tar.gz",true,true);
	// Generate the single file package
	Msg.Print("[Linux] Generate the single file tool");
	ExitCode = Exec("dotnet","publish -c Release -r linux-x64 -p:PublishSingleFile=true --self-contained true",true);	
	if (ExitCode != 0)
		return ExitCode;	
	Msg.Print("[Linux] Compress the single file tool");		
	Compress.Tar.CompressFile("out/pub/linux-x64/release/mkb","out/pkg/kombine.lnx.tar.gz");
	// OSX
	// ------------------------------------------------------------------------------------------
	Msg.Print("Building for Mac OSX");
	ExitCode = Exec("dotnet","build -c Release -r osx-x64",true);	
	if (ExitCode != 0)
		return ExitCode;	
	Msg.Print("[MacOS] Compress the unpacked tool");
	Compress.Tar.CompressFolder("out/bin/osx-x64/release/","out/pkg/kombine.debug.osx.tar.gz",true,true);
	// Generate the single file package
	Msg.Print("[MacOS] Generate the single file tool");
	ExitCode = Exec("dotnet","publish -c Release -r osx-x64 -p:PublishSingleFile=true --self-contained true",true);	
	if (ExitCode != 0)
		return ExitCode;	
	Msg.Print("[MacOS] Compress the single file tool");		
	Compress.Tar.CompressFile("out/pub/osx-x64/release/mkb","out/pkg/kombine.osx.tar.gz");
	// ------------------------------------------------------------------------------------------
	RestoreVersionNumber();
	Msg.Print("Done!");
	return 0;
}

public int intellisense(string[] args){

	Msg.Print("Generating intellisense");
	Msg.Print("Building for windows");
	if (Host.IsWindows()){
		int ExitCode = Exec("dotnet","build -c Release -r win-x64",true);	
		if (ExitCode != 0)
			return ExitCode;
		Files.Copy("out/bin/win-x64/release/mkb.dll","examples/childs/mkb.dll");
		Files.Copy("out/bin/win-x64/release/mkb.dll","examples/clang/mkb.dll");
		Files.Copy("out/bin/win-x64/release/mkb.dll","examples/folders/mkb.dll");
		Files.Copy("out/bin/win-x64/release/mkb.dll","examples/scripts/mkb.dll");
		Files.Copy("out/bin/win-x64/release/mkb.dll","examples/sdl2/mkb.dll");
		Files.Copy("out/bin/win-x64/release/mkb.dll","examples/simple/mkb.dll");
		Files.Copy("out/bin/win-x64/release/mkb.dll","examples/types/mkb.dll");
		Files.Copy("out/bin/win-x64/release/mkb.dll","examples/network/mkb.dll");
	}	
	Msg.Print("Done!");
	return 0;
}

public void ApplyVersionNumber(){
	Files.Copy("src/version.cs","src/version.cs.bak");
	string file = "src/version.cs";
	string content = Files.ReadTextFile(file);
	content = content.Replace("[BUILD]",GetVersionBuildNumber());
	Files.WriteTextFile(file,content);
}

public void RestoreVersionNumber(){
	Files.Delete("src/version.cs");
	Files.Move("src/version.cs.bak","src/version.cs");
}

public string GetVersionBuildNumber() {
	DateTime currentTime = DateTime.UtcNow;
	long now = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();			
	DateTime currentYear = new DateTime(DateTime.Now.Year, 1, 1);
	long year = ((DateTimeOffset)currentYear).ToUnixTimeSeconds();
	long bn = now - year;
	string buildNumber = "24" + (bn / 60).ToString("D6");
	return buildNumber;
}