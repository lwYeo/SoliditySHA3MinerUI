using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;

namespace SoliditySHA3MinerUI.Helper
{
    public static class Processor
    {
        public static Version MinimumDotnetCoreVersion => new Version("2.1.3");

        public static Version GetUIVersion => Assembly.GetExecutingAssembly().GetName().Version;

        public static Version GetUIVersionCompat
        {
            get
            {
                var uiVersion = GetUIVersion;
                return new Version(uiVersion.Major, uiVersion.Minor, uiVersion.Build);
            }
        }

        public static bool GetLocalMinerVersion(out Version version)
        {
            version = new Version();
            try
            {
                if (MinerInstance.MinerPath.Exists)
                {
                    version = new Version(FileVersionInfo.GetVersionInfo(MinerInstance.MinerPath.FullName).ProductVersion);
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        public static bool CheckDotnetCoreVersion(out Version dotnetCoreVersion)
        {
            dotnetCoreVersion = new Version();
            try
            {
                var processOutput = FileSystem.GetProcessOutput("dotnet", "--info");
                if (string.IsNullOrWhiteSpace(processOutput)) return false;

                var rawVersions = processOutput.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).
                                                Where(o => o.TrimStart().StartsWith("Microsoft.NETCore.App")).
                                                Select(o => o.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]).
                                                ToArray();
                if (!rawVersions.Any()) return false;

                var errorMessage = string.Empty;
                var versions = rawVersions.Select(r =>
                {
                    try
                    {
                        var version = string.Empty;
                        foreach (var c in r.TrimStart())
                        {
                            if (!char.IsDigit(c) && !c.Equals('.')) break;
                            version += c;
                        }
                        return new Version(version);
                    }
                    catch
                    {
                        if (!string.IsNullOrEmpty(errorMessage)) errorMessage += Environment.NewLine;
                        errorMessage += "Error parsing string to version: " + r;
                        return new Version();
                    }
                }).ToList();

                if (!string.IsNullOrWhiteSpace(errorMessage)) ShowMessageBox("Error parsing dotnet core version", errorMessage);

                var highestFoundVersion = dotnetCoreVersion;
                versions.ForEach(v => { if (v >= highestFoundVersion) highestFoundVersion = v; });

                dotnetCoreVersion = highestFoundVersion;
                return highestFoundVersion >= MinimumDotnetCoreVersion;
            }
            catch (Exception ex)
            {
                ShowMessageBox("Error getting dotnet core version", ex.Message);
                return false;
            }
        }

        public static bool GetApiUri(string rawPath, out string uriPath, out string errorMessage)
        {
            var defaultUriPath = "http://127.0.0.1:4078";
            var httpBind = rawPath.ToString();
            errorMessage = string.Empty;
            uriPath = string.Empty;

            if (string.IsNullOrWhiteSpace(httpBind)) httpBind = defaultUriPath;
            else if (httpBind == "0")
            {
                errorMessage = "JSON-API is disabled";
                return false;
            }

            if (!httpBind.StartsWith("http://") || httpBind.StartsWith("https://")) httpBind = "http://" + httpBind;
            if (!httpBind.EndsWith("/")) httpBind += "/";

            if (!int.TryParse(httpBind.Split(':')[2].TrimEnd('/'), out int port))
            {
                errorMessage = "Invalid port provided for JSON-API";
                return false;
            }

            var tempIPAddress = httpBind.Split(new string[] { "//" }, StringSplitOptions.None)[1].Split(':')[0];
            if (!IPAddress.TryParse(tempIPAddress, out IPAddress ipAddress))
            {
                errorMessage = "Invalid IP address provided for JSON-API";
                return false;
            }

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try { socket.Bind(new IPEndPoint(ipAddress, port)); }
                catch (Exception)
                {
                    errorMessage = "JSON-API failed to bind to: " + httpBind;
                    return false;
                }
            };

            uriPath = httpBind;
            return true;
        }

        public static void BuildTree(TreeView treeView, XDocument xDocument)
        {
            var treeNode = new TreeViewItem
            {
                Header = xDocument.Root.Name.LocalName,
                IsExpanded = true
            };
            treeView.Items.Add(treeNode);
            BuildNodes(treeNode, xDocument.Root);
        }

        private static void BuildNodes(TreeViewItem treeNode, XElement xElement)
        {
            foreach (var child in xElement.Nodes())
            {
                switch (child.NodeType)
                {
                    case XmlNodeType.Element:
                        var childElement = child as XElement;
                        var childTreeNode = new TreeViewItem
                        {
                            Header = childElement.Name.LocalName,
                            IsExpanded = true
                        };
                        treeNode.Items.Add(childTreeNode);
                        BuildNodes(childTreeNode, childElement);
                        break;

                    case XmlNodeType.Text:
                        var childText = child as XText;
                        treeNode.Items.Add(new TreeViewItem { Header = childText.Value ?? string.Empty });
                        break;
                }
            }
        }

        public static List<Process> GetAllRelatedProcessList(bool includeMain = false)
        {
            var processList = new List<Process>();

            var currentProcess = Process.GetCurrentProcess();
            if (includeMain) processList.Add(currentProcess);

            var query = "Select * From Win32_Process Where ParentProcessId = " + currentProcess.Id;

            using (var searcher = new ManagementObjectSearcher(query))
            {
                processList.AddRange(searcher.Get().
                                              OfType<ManagementObject>().
                                              Select(p => Process.GetProcessById(Convert.ToInt32(p.GetPropertyValue("ProcessId")))));
            }
            return processList;
        }

        public static void ShowMessageBox(string title, string message, params string[] messageArgs)
        {
            MessageBox.Show(Application.Current.MainWindow, string.Format(message, messageArgs), title);
        }
    }
}