using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text;

namespace ExifToolUtils
{
    /// <summary>
    /// A wrapper for exiftool.exe to encapsulate the 'stay_open' behavior
    /// See:
    ///   https://exiftool.org/
    ///   https://exiftool.org/exiftool_pod.html#Advanced-options
    /// </summary>
    /// <remarks>
    /// Takes care of the details of IPC via stdin and stdout without blocking
    /// or causing the child process stream buffers to fill.
    /// </remarks>
    public class StayOpenWrapper : IDisposable
    {
        private readonly Process _exiftoolproc;
        private bool disposedValue;
        private readonly object _instanceLock = new object();

        /// <summary>
        /// Create a new wrapper
        /// </summary>
        /// <param name="exifToolExePath">Full path to exiftool executable</param>
        public StayOpenWrapper(String exifToolExePath)
        {
            _exiftoolproc = StartExifTool(exifToolExePath);
        }

        /// <summary>
        /// Pass the supplied parameter list to exiftool, execute and return the results
        /// </summary>
        /// <param name="parameters">
        /// The exiftool parameters.
        /// See https://exiftool.org/exiftool_pod.html#Other-options
        /// E.g. "-xmp", "-b", "image.jpg"
        /// </param>
        /// <returns>The output from exiftool stdout stream</returns>
        public (string StdOut, string StdErr) Execute(IEnumerable<String> parameters)
        {
            if (disposedValue) throw new ObjectDisposedException(GetType().FullName);

            // Not multi-threaded, since we can't mix exiftool commands and preserve
            // the ordering of the exiftool child process output streams
            lock (_instanceLock)
            {
                foreach (var arg in parameters)
                {
                    _exiftoolproc.StandardInput.WriteLine(arg);
                }

                using (var execComplete = new AutoResetEvent(false))
                {
                    var stdOut = new StringBuilder();
                    var stdErr = new StringBuilder();

                    (bool foundMarker, string cleaned) TryRemoveEndOfOutputMarker(string input)
                    {
                        var index = input.IndexOf(endOutputFlag);
                        if (index==-1)
                        {
                            return (false, input);
                        }
                        return (true, input.Substring(0, index));
                    }

                    void StdOutAction(object sender, DataReceivedEventArgs args)
                    {
                        var (detectedMarker, clean) = TryRemoveEndOfOutputMarker(args.Data);
                        if (!String.IsNullOrEmpty(clean))
                        {
                            stdOut.Append(clean);
                        }
                        if (detectedMarker)
                        {
                            execComplete.Set();
                        }
                    }

                    void StdErrAction(object sender, DataReceivedEventArgs args)
                    {
                        stdErr.Append(args.Data);
                    }

                    _exiftoolproc.OutputDataReceived += StdOutAction;
                    _exiftoolproc.ErrorDataReceived += StdErrAction;

                    _exiftoolproc.StandardInput.WriteLine("-execute");

                    execComplete.WaitOne();

                    _exiftoolproc.OutputDataReceived -= StdOutAction;
                    _exiftoolproc.ErrorDataReceived -= StdErrAction;

                    return (StdOut: stdOut.ToString(), StdErr: stdErr.ToString());
                }
            }
        }

        private const string endOutputFlag = "{ready}";

        private Process StartExifTool(String exifToolExePath)
        {
            var proc = new Process();
            proc.StartInfo.FileName = exifToolExePath;
            proc.StartInfo.Arguments = "-stay_open True -@ -"; // note the second hyphen
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.EnableRaisingEvents = true;

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            return proc;
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _exiftoolproc.StandardInput.WriteLine("-stay_open\nFalse");
                _exiftoolproc.WaitForExit();

                if (disposing)
                {
                    _exiftoolproc.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}