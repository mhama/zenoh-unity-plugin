using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;

namespace Zenoh.Tests
{
    internal sealed class ZenohdTestServer : IDisposable
    {
        public const int Port = 17447;
        public const string Endpoint = "tcp/127.0.0.1:17447";

        private const string ConfigRelativePath = "Assets/ZenohPackage/Tests/EditMode/router_localhost_config.json5";

        private Process process;

        private ZenohdTestServer(Process process)
        {
            this.process = process;
        }

        public static ZenohdTestServer Start()
        {
            if (CanConnect())
            {
                Assert.Ignore(Endpoint + " is already in use. Stop the existing process before running this test so TearDown can own zenohd cleanup.");
            }

            string zenohdPath = FindZenohdPath();
            if (string.IsNullOrEmpty(zenohdPath))
            {
                Assert.Ignore("zenohd was not found. Set ZENOH_TEST_ZENOHD_PATH to the zenohd executable path, or add zenohd to PATH.");
            }

            string configPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigRelativePath);
            if (!File.Exists(configPath))
            {
                Assert.Fail("zenohd config file was not found: " + configPath);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = zenohdPath,
                Arguments = "--config \"" + configPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process startedProcess = Process.Start(startInfo);
            if (startedProcess == null)
            {
                Assert.Fail("Failed to start zenohd.");
            }

            ZenohdTestServer server = new ZenohdTestServer(startedProcess);
            if (!WaitForStartup(TimeSpan.FromSeconds(5)))
            {
                string exitMessage = startedProcess.HasExited ? " Exit code: " + startedProcess.ExitCode + "." : string.Empty;
                server.Dispose();
                Assert.Fail("zenohd did not start listening on " + Endpoint + " within 5 seconds." + exitMessage);
            }

            return server;
        }

        public void Dispose()
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            finally
            {
                process.Dispose();
                process = null;
            }
        }

        private static bool WaitForStartup(TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (CanConnect())
                {
                    return true;
                }

                Thread.Sleep(100);
            }

            return false;
        }

        private static bool CanConnect()
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult result = client.BeginConnect("127.0.0.1", Port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(100))
                    {
                        return false;
                    }

                    client.EndConnect(result);
                    return true;
                }
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private static string FindZenohdPath()
        {
            string explicitPath = Environment.GetEnvironmentVariable("ZENOH_TEST_ZENOHD_PATH");
            if (IsExecutableFile(explicitPath))
            {
                return explicitPath;
            }

            string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "zenohd.exe" : "zenohd";
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                string[] directories = path.Split(Path.PathSeparator);
                foreach (string directory in directories)
                {
                    string candidate = Path.Combine(directory, fileName);
                    if (IsExecutableFile(candidate))
                    {
                        return candidate;
                    }
                }
            }

            string[] commonPaths =
            {
                "/opt/homebrew/bin/zenohd",
                "/usr/local/bin/zenohd"
            };

            foreach (string candidate in commonPaths)
            {
                if (IsExecutableFile(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsExecutableFile(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }
    }
}
