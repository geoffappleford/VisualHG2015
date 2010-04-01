﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace HGLib
{
    // ------------------------------------------------------------------------
    // async query callback method
    // ------------------------------------------------------------------------
    public delegate void HandleFileStatusProc(string rootDirectory, Dictionary<string, char> fileStatusDictionary, SynchronizationContext context);

    // ------------------------------------------------------------------------
    // HG interface class - here we cover HG.exe commands in sync and async manner.
    // There are methods for file status queries, rename, add or remove files from
    // repo and update commands included. All these methods are defined as static to
    // become easy to use.
    // ------------------------------------------------------------------------
    public static class HG
    {
        #region invoke HG commands

        // ------------------------------------------------------------------------
        // invoke HG exe commands
        // ------------------------------------------------------------------------
        static Process InvokeCommand(string workingDirectory, string arguments)
        {
            Trace.WriteLine("InvokeCommand : " + arguments);

            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.FileName = "HG.exe";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.Start();
            return process;
        }

        // ------------------------------------------------------------------------
        // invoke HG exe commands and read results
        // ------------------------------------------------------------------------
        public static bool InvokeCommand(string workingDirectory, string arguments, out List<string> resultList)
        {
            resultList = new List<string>();
            
            Process process = InvokeCommand(workingDirectory, arguments);

            string str = "";
            while (!process.HasExited)
            {
                while ((str = process.StandardOutput.ReadLine()) != null)
                {
                    resultList.Add(str);
                }
                Thread.Sleep(0);
            }

            while ((str = process.StandardOutput.ReadLine()) != null)
            {
                resultList.Add(str);
            }

            return (resultList.Count > 0);
        }

        // ------------------------------------------------------------------------
        // build the argument sting from the given files and call invoke the hg command
        // ------------------------------------------------------------------------
        static void InvokeCommand(string command, string[] fileList, bool directoriesAllowed)
        {
            string workingDirectory = fileList[0].Substring(0, fileList[0].LastIndexOf('\\'));
            string rootDirectory = HG.FindRootDirectory(workingDirectory);

            string commandString = command;
            foreach (var file in fileList)
            {
                if(!directoriesAllowed && file.EndsWith("\\"))
                    continue;

                commandString += " \"" + file.Substring(rootDirectory.Length + 1) + "\" ";
            }

            List<string> list;
            HG.InvokeCommand(rootDirectory, commandString, out list);
        }

        // ------------------------------------------------------------------------
        // invoke the given command and get the new status of dependend files
        // to the dictionary
        // ------------------------------------------------------------------------
        static bool InvokeCommandGetStatus(string cmd, string[] fileList, out Dictionary<string, char> fileStatusDictionary)
        {
            fileStatusDictionary = null;
            try
            {
                HG.InvokeCommand(cmd, fileList, false);

                if (!QueryFileStatus(fileList, out fileStatusDictionary))
                    fileStatusDictionary = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("cmd- " + ex.Message);
                return false;

            }
            return (fileStatusDictionary != null);
        }

        #endregion invoke commands

        // ------------------------------------------------------------------------
        // find the HG root directory (archive directory) staring from the given dir
        // ------------------------------------------------------------------------
        static public string xxxFindRootDirectory(string directory)
        {
            try
            {
                while (!Directory.Exists(directory) && directory.Length > 0)
                {
                    directory = directory.Substring(0, directory.LastIndexOf('\\'));
                }

                List<string> list;
                if (HG.InvokeCommand(directory, "root", out list))
                {
                    return list[0];
                }
            }
            catch (Exception ex)
            {
                // is not a root dir or the directory does not exists
                Trace.WriteLine("HG.FindRootDirectory " + ex.Message);
            }
            return string.Empty;
        }

        // ------------------------------------------------------------------------
        /// get (lower case) root dir of the given file name
        // ------------------------------------------------------------------------
        public static string FindRootDirectory(string path)
        {
            path = path.ToLower();

            if (path.EndsWith("\\"))
                path = path.Substring(0, path.Length - 1);

            while (path.Length > 0)
            {
              if (File.Exists(path + "\\.hg\\thgstatus"))
                    return path;

                int index = path.LastIndexOf('\\');
                if (index >= 0)
                    path = path.Substring(0, index);
                else
                    break;
            }
            return string.Empty;
        }

        // ------------------------------------------------------------------------
        // get all states of the files inside the directory
        // ------------------------------------------------------------------------
        #region QueryRootStatus

        // ------------------------------------------------------------------------
        // run a async HG root status query and get the resulting status informations
        // to a fileStatusDictionary or callback method
        // ------------------------------------------------------------------------
        static public void QueryRootStatus(string rootDirectory, HandleFileStatusProc handleFileStatusProc)
        {
            SynchronizationContext context = WindowsFormsSynchronizationContext.Current;

            ThreadPool.QueueUserWorkItem(o =>
            {
                Dictionary<string, char> fileStatusDictionary;

                if (HG.QueryRootStatus(rootDirectory, out fileStatusDictionary))
                {
                    // notify the callee
                    if (handleFileStatusProc != null)
                        handleFileStatusProc(rootDirectory, fileStatusDictionary, context);
                }
            });
        }

        // ------------------------------------------------------------------------
        // update status dictionary with hg status cmd output
        // ------------------------------------------------------------------------
        public static bool UpdateStatusDictionary(List<string> lines, string rootDirectory, Dictionary<string, char> fileStatusDictionary)
        {
            char prevStatus = ' ';
            string prevFile = "";
            foreach (string str in lines)
            {
                char status = str[0];
                string file = rootDirectory + "\\" + str.Substring(2);

                if (status == ' ' && prevStatus == 'A')
                {
                    status = 'N'; // mark with internal VisualHG state 'N' for renamed file
                    file = prevFile;
                }

                fileStatusDictionary[file] = status;

                prevFile = file;
                prevStatus = status;
            }
            return true;
        }

        // ------------------------------------------------------------------------
        // run a HG root status query and get the resulting status informations to a fileStatusDictionary
        // ------------------------------------------------------------------------
        public static bool QueryRootStatus(string rootDirectory, out Dictionary<string, char> fileStatusDictionary)
        {
            Trace.WriteLine("Start QueryRootStatus");

            fileStatusDictionary = null;
            
            if (rootDirectory != string.Empty)
            {
                fileStatusDictionary = new Dictionary<string, char>();

                // Start a new process for the cmd
                Process process = HG.InvokeCommand(rootDirectory, "status -A");

                List<string> lines = new List<string>();

                string str = "";
                while (!process.HasExited)
                {
                    while ((str = process.StandardOutput.ReadLine()) != null)
                    {
                        lines.Add(str);
                    }
                    Thread.Sleep(0);
                }

                while ((str = process.StandardOutput.ReadLine()) != null)
                {
                    lines.Add(str);
                }

                UpdateStatusDictionary(lines, rootDirectory, fileStatusDictionary);
            }
            return (fileStatusDictionary != null);
        }

        #endregion QueryRootStatus

        // ------------------------------------------------------------------------
        // get all states of the requested files
        // ------------------------------------------------------------------------
        #region QueryFileStatusCmd

        // ------------------------------------------------------------------------
        // Run async query.
        // query the file status and call handleFileStatusProc with the resulting dictionary
        // ------------------------------------------------------------------------
        static public void QueryFileStatus(string[] fileList, HandleFileStatusProc handleFileStatusProc)
        {
            SynchronizationContext context = WindowsFormsSynchronizationContext.Current;

            ThreadPool.QueueUserWorkItem(o =>
            {
                Dictionary<string, char> fileStatusDictionary;
                // query the files status and get them to the fileStatusDictionary
                if (HG.QueryFileStatus(fileList, out fileStatusDictionary))
                {
                    // and notity the calee
                    if (handleFileStatusProc != null)
                        handleFileStatusProc("", fileStatusDictionary, context);
                }
            });
        }

        // ------------------------------------------------------------------------
        // query the files status and get them to the fileStatusDictionary
        // ------------------------------------------------------------------------
        static public bool QueryFileStatus(string[] fileList, out Dictionary<string, char> fileStatusDictionary)
        {
            fileStatusDictionary = new Dictionary<string, char>();
            try
            {
                if (fileList.Length > 0)
                {
                    string rootFile = fileList[0];
                    string directory = rootFile.Substring(0, rootFile.LastIndexOf('\\'));
                    string rootDirectory = HG.FindRootDirectory(directory);

                    // limit the number of files per call to avois inputbuffer overflows
                    string cmlLine = "";
                    int fileCounter = 0;
                    for (int iFile = 0; iFile < fileList.Length; ++iFile)
                    {
                        cmlLine += " \"" + fileList[iFile].Substring(rootDirectory.Length+1) + "\" ";
                        fileCounter++;

                        if (fileCounter >= 150 || (fileList.Length == iFile + 1))
                        {
                            List<string> resultList;
                            InvokeCommand(rootDirectory, "status -A " + cmlLine, out resultList);

                            UpdateStatusDictionary(resultList, rootDirectory, fileStatusDictionary);

                            // reset cmd line and filecounter for the next run
                            cmlLine = "";
                            fileCounter = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("HGProcess.StartHGProcess: " + ex.Message);
                return false;
            }

            return (fileStatusDictionary != null);
        }
        
        #endregion QueryFileStatusCmd


        // ------------------------------------------------------------------------
        // put file under source control
        // ------------------------------------------------------------------------
        #region AddFiles

        // ------------------------------------------------------------------------
        // async handle AddFiles event - update hg repository
        // ------------------------------------------------------------------------
        static public void AddFiles(string[] fileList, HandleFileStatusProc handleFileStatusProc)
        {
            SynchronizationContext context = WindowsFormsSynchronizationContext.Current;

            ThreadPool.QueueUserWorkItem(o =>
            {
                Dictionary<string, char> fileStatusDictionary;
                if (AddFiles(fileList, out fileStatusDictionary))
                {
                    // and notity the calee
                    if (handleFileStatusProc != null)
                        handleFileStatusProc("", fileStatusDictionary, context);
                }
            });
        }

        // ------------------------------------------------------------------------
        // handle file removed event - update hg repository
        // ------------------------------------------------------------------------
        static public bool AddFiles(string[] fileList, out Dictionary<string, char> fileStatusDictionary)
        {
            return InvokeCommandGetStatus("add", fileList, out fileStatusDictionary);
        }

        // ------------------------------------------------------------------------
        // async add file to the repositiry if they are not on the ignore list
        // ------------------------------------------------------------------------
        static public void AddFilesNotIgnored(string[] fileList, HandleFileStatusProc handleFileStatusProc)
        {
            SynchronizationContext context = WindowsFormsSynchronizationContext.Current;

            ThreadPool.QueueUserWorkItem(o =>
            {
                Dictionary<string, char> fileStatusDictionary;
                if (AddFilesNotIgnored(fileList, out fileStatusDictionary))
                {
                    // and notity the calee
                    if (handleFileStatusProc != null)
                        handleFileStatusProc("", fileStatusDictionary, context);
                }
            });
        }

        static public bool AddFilesNotIgnored(string[] fileList, out Dictionary<string, char> fileStatusDictionary)
        {
            List<string> addFilesList = new List<string>();

            Dictionary<string, char> statusDictionary;
            QueryFileStatus(fileList, out statusDictionary);
            foreach (var k in statusDictionary)
            {
                if (k.Value != 'I')
                {
                    addFilesList.Add(k.Key);
                }
            }

            return InvokeCommandGetStatus("add", addFilesList.ToArray(), out fileStatusDictionary);
        }

        #endregion AddFiles


        // ------------------------------------------------------------------------
        // propagate file renamed in the hg repository
        // ------------------------------------------------------------------------
        #region PropagateFileRenamed

        // ------------------------------------------------------------------------
        // async handle renamed file event - update hg repository
        // ------------------------------------------------------------------------
        static public void PropagateFileRenamed(string[] orgFileName, string[] newFileName, HandleFileStatusProc handleFileStatusProc)
        {
             SynchronizationContext context = WindowsFormsSynchronizationContext.Current;

            ThreadPool.QueueUserWorkItem(o =>
            {
                Dictionary<string, char> fileStatusDictionary;
                if (PropagateFileRenamed(orgFileName, newFileName, out fileStatusDictionary))
                {
                    // and notity the calee
                    if (handleFileStatusProc != null)
                        handleFileStatusProc("", fileStatusDictionary, context);
                }
            });
        }

        // ------------------------------------------------------------------------
        // handle renamed file event - update hg repository
        // ------------------------------------------------------------------------
        static public bool PropagateFileRenamed(string[] orgFileName, string[] newFileName, out Dictionary<string, char> fileStatusDictionary)
        {
            fileStatusDictionary = null;
            try
            {
                for (int pos = 0; pos < orgFileName.Length; ++pos)
                {
                    string workingDirectory = orgFileName[pos].Substring(0, orgFileName[pos].LastIndexOf('\\'));
                    string rootDirectory = HG.FindRootDirectory(workingDirectory);

                    string ofile = orgFileName[pos].Substring(rootDirectory.Length + 1);
                    string nfile = newFileName[pos].Substring(rootDirectory.Length + 1);
                    List<string> list;
                    HG.InvokeCommand(rootDirectory,
                        "rename  -A \"" + ofile + "\" \"" + nfile + "\"", out list);
                }

                if (!QueryFileStatus(newFileName, out fileStatusDictionary))
                    fileStatusDictionary = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("HG.PropagateFileRenamed exception- " + ex.Message);
                return false;

            }

            return (fileStatusDictionary != null);
        }

        #endregion PropagateFileRenamed

        // ------------------------------------------------------------------------
        // propagate file removed in the hg repository
        // ------------------------------------------------------------------------
        #region PropagateFileRemoved

        // ------------------------------------------------------------------------
        // async handle file removed event - update hg repository
        // ------------------------------------------------------------------------
        static public void PropagateFileRemoved(string[] fileList, HandleFileStatusProc handleFileStatusProc)
        {
            SynchronizationContext context = WindowsFormsSynchronizationContext.Current;

            ThreadPool.QueueUserWorkItem(o =>
            {
                Dictionary<string, char> fileStatusDictionary;
                if (PropagateFileRemoved(fileList, out fileStatusDictionary))
                {
                    // and notity the calee
                    if (handleFileStatusProc != null)
                        handleFileStatusProc("", fileStatusDictionary, context);
                }
            });
        }

        // ------------------------------------------------------------------------
        // handle file removed event - update hg repository
        // ------------------------------------------------------------------------
        static public bool PropagateFileRemoved(string[] fileList, out Dictionary<string, char> fileStatusDictionary)
        {
            return InvokeCommandGetStatus("remove", fileList, out fileStatusDictionary);
        }

        #endregion PropagateFileRemoved


        // ------------------------------------------------------------------------
        // revert uncommited changes
        // ------------------------------------------------------------------------
        public static bool Revert(string[] fileList, out Dictionary<string, char> fileStatusDictionary)
        {
            return InvokeCommandGetStatus("revert", fileList, out fileStatusDictionary);
        }
    }
}
