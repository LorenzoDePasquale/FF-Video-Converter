using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FFVideoConverter
{
    public static class ClassExtensions
    {
        #region Native

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        #endregion

        #region Process

        //Suspends all threads of the current process
        public static void Suspend(this Process process)
        {
            IntPtr pOpenThread;

            foreach (ProcessThread pT in process.Threads)
            {
                pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);
                if (pOpenThread != IntPtr.Zero)
                {
                    SuspendThread(pOpenThread);
                    CloseHandle(pOpenThread);
                }
            }
        }

        //Resumes all threads of the current process
        public static void Resume(this Process process)
        {
            IntPtr pOpenThread;
            int suspendCount;

            foreach (ProcessThread pT in process.Threads)
            {
                pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);
                if (pOpenThread != IntPtr.Zero)
                {
                    do
                    {
                        suspendCount = ResumeThread(pOpenThread);
                    }
                    while (suspendCount > 0);
                    CloseHandle(pOpenThread);
                }
            }
        }

        #endregion

        #region HttpClient

        //Asynchroniously downloads a remote file into a stream, with progress reporting
        public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination, IProgress<(float, long, long)> progress)
        {
            using (HttpResponseMessage response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                if (response.Content.Headers.ContentLength == null)
                {
                    throw new Exception("Couldn't determine file size");
                }
                var totalBytes = response.Content.Headers.ContentLength.Value;

                using (Stream downloadStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var relativeProgress = new Progress<long>(currentBytes => progress.Report(((float)currentBytes / totalBytes, currentBytes, totalBytes)));
                    await downloadStream.CopyToAsync(destination, relativeProgress).ConfigureAwait(false);
                    progress.Report((1f, totalBytes, totalBytes));
                }
            }
        }

        //Asynchroniously copies a stream into another, with progress reporting
        public static async Task CopyToAsync(this Stream source, Stream destination, IProgress<long> progress)
        {
            var buffer = new byte[40960];
            long totalBytesRead = 0;
            int bytesRead;
            Stopwatch sw = new Stopwatch(); //Used to report progress every 200 milliseconds

            sw.Start();
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                if (sw.ElapsedMilliseconds > 500)
                {
                    sw.Restart();
                    progress.Report(totalBytesRead);
                }
            }
            sw.Stop();
        }

        #endregion
    }
}