# Intelmeter fork of GXDLMSDirector

This is an unofficial community fork, maintained under the Intelmeter GitHub account. It is not an official Gurux release and does not imply Gurux endorsement. Original Gurux attribution is retained below.

License: **GPL-2.0-only**. The fork's contributions are distributed under the same license; original Gurux and third-party notices remain applicable. See [LICENSE](LICENSE), [MODIFICATIONS.txt](MODIFICATIONS.txt), [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), and [RELEASE-COMPLIANCE.txt](RELEASE-COMPLIANCE.txt).

Build on Windows with the .NET 10 SDK. Clone both Intelmeter repositories into sibling directories named GXDLMSDirector and Gurux.DLMS.UI.Net, then run from their parent directory:

    dotnet build GXDLMSDirector/Development/GXDLMSDirector46.csproj -c Release -t:Rebuild

The protocol library remains the official Gurux.DLMS 9.0.2609.2201 NuGet package. This GPL dependency is not exempt from redistribution obligations merely because it is unmodified.

---

![alt tag](ui.png)


See An [Gurux](http://www.gurux.org/ "Gurux") for an overview.

Join the Gurux Community or follow [@Gurux](https://twitter.com/guruxorg "@Gurux") for project updates.

With GXDLMSDirector application you to read you DLMS/COSEM compatible electricity, gas or water meters. 
You can control your meters with easy to use user interface. 

Best part is that you can control your different meters with same application. 
You do not need manufactur specific applications. 

For more info check out [GXDLMSDirector](http://www.gurux.fi/index.php?q=GXDLMSDirector "GXDLMSDirector").

We are updating documentation on Gurux web page. 

Because DLMS manufacturers are using different parameters for meters we are updating [default meters settings](http://www.gurux.fi/index.php?q=GXDLMSDirectorExample).
We'll try to make your meter reading as easy as possible.

GXDLMSDirector [Help](http://www.gurux.fi/index.php?q=GXDLMSDirectorHelp "help") aids you to get started.

If you have problems or ideas you can ask your questions in Gurux GXDLMSDirector [Forum](http://www.gurux.fi/index.php?q=GXDLMSCOSEMDirectorForum).

If you want to build source codes you need Nuget package manager for Visual Studio.
You can get it here:
https://visualstudiogallery.msdn.microsoft.com/27077b70-9dad-4c64-adcf-c7cf6bc9970c