# Exiftool Utils
A collection of exiftool related code to make Powershell automation of exiftool easier.
See [ExifTool](https://exiftool.org/)
## Exiftool::StayOpenWrapper
ExifTool has a mode (-stay-open) where it runs in the background and reads commands from a file or stdin. See documentation: [Exiftool advanced options](https://exiftool.org/exiftool_pod.html#Advanced-options)
Doing this reliably in Powershell is difficult: 
 - To prevent the child process (exiftool) standard output (stdout &   stderr) buffers filling and subsequently blocking the child process,    stdout & stderr have to be read asynchronously. See the   [documentations](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.beginoutputreadline?view=net-5.0#System_Diagnostics_Process_BeginOutputReadLine)- This requires using events to receive output- Powershell events use the [ThreadPool and therefore are unordered](https://stackoverflow.com/questions/61313233/powershell-events-received-out-of-sequence).
Hence this wrapper that:
 - Runs Exiftool as a child process Reliably sends commands and   retrieves the output. 
