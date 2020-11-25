using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Collections.ObjectModel;

namespace ExifTool
{
    /// <summary>
    /// Returned from <see cref="Wrapper.Execute(IEnumerable{string})"/>
    /// </summary>
    public sealed class ExecuteResult
    {
        internal ExecuteResult(IList<string> stdOut, IList<string> stdErr)
        {
            StdOutLines = new ReadOnlyCollection<string>(stdOut);
            StdErrLines = new ReadOnlyCollection<string>(stdErr);
        }

        /// <summary>
        /// The lines captured from exiftool stdout stream
        /// </summary>
        public ReadOnlyCollection<string> StdOutLines { get; }

        /// <summary>
        /// The lines captured from exiftool stderr stream
        /// </summary>
        public ReadOnlyCollection<string> StdErrLines { get; }
    }

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
    public class Wrapper : IDisposable
    {
        private readonly Process _exiftoolproc;
        private bool disposedValue;
        private readonly object _instanceLock = new object();

        /// <summary>
        /// Create a new wrapper
        /// </summary>
        /// <param name="exifToolExePath">Full path to exiftool executable</param>
        public Wrapper(String exifToolExePath)
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
        public ExecuteResult Execute(IEnumerable<String> parameters)
        {
            if (disposedValue) throw new ObjectDisposedException(GetType().FullName);

            // Not multi-threaded, since we can't mix exiftool commands and preserve
            // the ordering of the exiftool child process outout streams
            lock (_instanceLock)
            {
                foreach (var arg in parameters)
                {
                    _exiftoolproc.StandardInput.WriteLine(arg);
                }

                using (var stdOutDataReceived = new AutoResetEvent(false))
                {
                    var stdOutLines = new List<String>();
                    var stdErrLines = new List<String>();

                    void StdOutAction(object sender, DataReceivedEventArgs args)
                    {
                        stdOutLines.Add(args.Data);
                        stdOutDataReceived.Set();
                    }

                    void StdErrAction(object sender, DataReceivedEventArgs args)
                    {
                        stdErrLines.Add(args.Data);
                    }

                    void WaitForExecuteComplete()
                    {
                        while (stdOutLines.Count==0 || !stdOutLines.Last().Contains(endOutputFlag))
                        {
                            stdOutDataReceived.WaitOne();
                        }
                    }

                    void RemoveEndOutputFlag()
                    {
                        var lastLine = stdOutLines.Last();
                        var cleaned = lastLine.Replace(endOutputFlag, "");
                        if (String.IsNullOrEmpty(cleaned))
                        {
                            stdOutLines.Remove(lastLine);
                        }
                        else
                        {
                            stdOutLines[stdOutLines.Count-1] = cleaned;
                        }
                    }

                    _exiftoolproc.OutputDataReceived += StdOutAction;
                    _exiftoolproc.ErrorDataReceived += StdErrAction;

                    _exiftoolproc.StandardInput.WriteLine("-execute");

                    WaitForExecuteComplete();

                    _exiftoolproc.OutputDataReceived -= StdOutAction;
                    _exiftoolproc.ErrorDataReceived -= StdErrAction;

                    RemoveEndOutputFlag();

                    return new ExecuteResult(stdOutLines, stdErrLines);
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
