﻿namespace SourceBrowser.Site.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using SourceBrowser.Generator;
    using SourceBrowser.Site.Models;
    using SourceBrowser.Site.Utilities;
    using SourceBrowser.SolutionRetriever;

    internal static class BrowserRepository
    {
        private static readonly string StaticHtmlAbsolutePath = System.Web.Hosting.HostingEnvironment.MapPath("~/SB_Files/");

        /// <summary>
        /// Parses provided string and tries to retrieve:
        /// github user, repo, solution name, file within solution
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="githubUser">
        /// The github User.
        /// </param>
        /// <param name="githubRepo">
        /// The github Repo.
        /// </param>
        /// <param name="solutionName">
        /// The solution Name.
        /// </param>
        /// <param name="fileName">
        /// The file Name.
        /// </param>
        /// <returns>
        /// True if the folder info could be found.
        /// </returns>
        public static bool GetFolderInfo(string id, out string githubUser, out string githubRepo, out string solutionName, out string fileName)
        {
            githubUser = string.Empty;
            githubRepo = string.Empty;
            solutionName = string.Empty;
            fileName = string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            var pathParts = id.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length == 0)
            {
                return false;
            }

            if (pathParts.Length >= 1)
            {
                githubUser = pathParts[0];
            }

            if (pathParts.Length >= 2)
            {
                githubRepo = pathParts[1];
            }

            if (pathParts.Length >= 3)
            {
                solutionName = pathParts[2];
            }

            if (pathParts.Length >= 4)
            {
                fileName = string.Join("/", pathParts.Skip(3));
            }

            return true;
        }

        /// <summary>
        /// Returns a list of all Github users on file.
        /// </summary>
        /// <returns>
        /// All Github users.
        /// </returns>
        public static List<GithubUserStructure> GetAllGithubUsers()
        {
            // If, for some reason, we have no data at all, just return an empty list
            if (!Directory.Exists(StaticHtmlAbsolutePath))
            {
                return new List<GithubUserStructure>();
            }

            // Otherwise, find them all
            var directories = Directory.GetDirectories(StaticHtmlAbsolutePath);
            var users = new List<GithubUserStructure>(directories.Length);
            foreach (var directoryName in directories)
            {
                var userName = Path.GetFileName(directoryName);
                users.Add(GetUserStructure(userName));
            }
            return users;
        }

        /// <summary>
        /// Returns a structure containing information on user's github repositories available at Source Browser.
        /// If the structure does not exist, creates it.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        internal static GithubUserStructure GetUserStructure(string userName)
        {
            var userDataFile = Path.Combine(StaticHtmlAbsolutePath, userName, "user.data");
            if (!File.Exists(userDataFile))
            {
                var userData = SetUpUserStructure(userName);
                FileUtilities.SerializeData(userData, userDataFile);
                return userData;
            }
            try
            {
                return FileUtilities.DeserializeData<GithubUserStructure>(userDataFile);
            }
            catch
            {
                // There was some problem. Recreate the data.
                var userData = SetUpUserStructure(userName);
                FileUtilities.SerializeData(userData, userDataFile);
                return userData;
            }
            finally
            {
                // if user.data file is stale, asynchronously update it.
                if (!FileUtilities.FileIsFresh(userDataFile))
                {
                    Task.Run(() =>
                    {
                        var updatedData = SetUpUserStructure(userName);
                        FileUtilities.SerializeData(updatedData, userDataFile);
                    });
                }
            }
        }
    
        /// <summary>
        /// Creates a structure containing information on user's github repositories available at Source Browser.
        /// </summary>
        /// <param name="userName">The username.</param>
        /// <returns>The github structure.</returns>
        internal static GithubUserStructure SetUpUserStructure(string userName)
        {
            var repoPath = Path.Combine(StaticHtmlAbsolutePath, userName);

            List<GithubRepoStructure> repos;
            if (Directory.Exists(repoPath))
            {
                var directories = Directory.GetDirectories(repoPath);
                repos = new List<GithubRepoStructure>(directories.Length);
                foreach (var directory in directories)
                {
                    var repoName = Path.GetFileName(directory);
                    repos.Add(GetRepoStructure(userName, repoName));
                }
            }
            else
            {
                // If, for some reason the directory doesn't exist, just supply an empty list
                repos = new List<GithubRepoStructure>();
            }

            var userData = new GithubUserStructure()
            {
                Username = userName,
                Repos = repos,
                Path = repoPath
            };
            userData.UseLiveData();

            return userData;
        }

        /// <summary>
        /// Returns a structure containing information on a repository available at Source Browser.
        /// If the structure does not exist, creates it.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="repoName"></param>
        /// <returns></returns>
        internal static GithubRepoStructure GetRepoStructure(string userName, string repoName)
        {
            var repoDataFile = Path.Combine(StaticHtmlAbsolutePath, userName, repoName, "repo.data");
            if (!File.Exists(repoDataFile))
            {
                var repoData = SetUpRepoStructure(userName, repoName);
                FileUtilities.SerializeData(repoData, repoDataFile);
                return repoData;
            }
            try
            {
                return FileUtilities.DeserializeData<GithubRepoStructure>(repoDataFile);
            }
            catch
            {
                // There was some problem. Recreate the data.
                var repoData = SetUpRepoStructure(userName, repoName);
                FileUtilities.SerializeData(repoData, repoDataFile);
                return repoData;
            }
            finally
            {
                // if repo.data file is stale, asynchronously update it.
                if (!FileUtilities.FileIsFresh(repoDataFile))
                {
                    Task.Run(() =>
                    {
                        var updatedData = SetUpRepoStructure(userName, repoName);
                        FileUtilities.SerializeData(updatedData, repoDataFile);
                    });
                }
            }
        }

        /// <summary>
        /// Creates a structure containing information on a repository available at Source Browser.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="repoName"></param>
        /// <returns></returns>
        internal static GithubRepoStructure SetUpRepoStructure(string userName, string repoName)
        {
            var solutionPath = Path.Combine(StaticHtmlAbsolutePath, userName, repoName);

            List<string> solutionNames;
            if (Directory.Exists(solutionPath))
            {
                var directories = Directory.GetDirectories(solutionPath);
                solutionNames = new List<string>(directories.Length);
                solutionNames.AddRange(directories.Select(Path.GetFileName));
            }
            else
            {
                // If, for some reason the directory doesn't exist, just supply an empty list
                solutionNames = new List<string>();
            }

            var repoData = new GithubRepoStructure()
            {
                Name = repoName,
                Solutions = solutionNames,
                ParentUserName = userName
            };
            repoData.UseLiveData();

            return repoData;
        }

        internal static GithubSolutionStructure SetUpSolutionStructure(string userName, string repoName, string solutionName)
        {
            string solutionInfoPath;
            var solutionInfo = GetSolutionInfo(userName, repoName, solutionName, out solutionInfoPath);

            var viewModel = new GithubSolutionStructure()
            {
                Name = solutionName,
                RelativePath = CreatePath(userName, repoName, solutionName),
                RelativeRootPath = CreatePath(userName, repoName, solutionName),
                SolutionInfo = solutionInfo,
                ParentRepo = SetUpRepoStructure(userName, repoName)
            };

            return viewModel;
        }

        internal static GithubFileStructure SetUpFileStructure(DocumentInfo docInfo, string userName, string repoName, string solutionName, string pathRemainder)
        {
            string solutionInfoPath;
            var solutionInfo = GetSolutionInfo(userName, repoName, solutionName, out solutionInfoPath);

            var viewModel = new GithubFileStructure
            {
                FileName = Path.GetFileName(pathRemainder),
                Directory = GetRelativeDirectory(pathRemainder),
                RelativePath = CreatePath(userName, repoName, solutionName, GetRelativeDirectory(pathRemainder)), // Used to expand nodes leading to this file
                RelativeRootPath = CreatePath(userName, repoName, solutionName), // Points to the root of the treeview
                SourceCode = docInfo.HtmlContent,
                NumberOfLines = docInfo.NumberOfLines,
                SolutionInfo = solutionInfo
            };

            return viewModel;
        }


        internal static void FindPage(string path)
        {
            var fullPath = Path.Combine(StaticHtmlAbsolutePath, path);

            if (Directory.Exists(fullPath))
            {
                // It's a folder, we want to list the files.
                var files = FindFiles(fullPath);
                var folders = FindFolders(fullPath);
            }

            if (File.Exists(fullPath))
            {
                // It's a file, we want to list the file.
            }
        }

        public static bool IsFile(string path)
        {
            var fullPath = Path.Combine(StaticHtmlAbsolutePath, path);
            if (File.Exists(fullPath))
            {
                return true;
            }

            return false;
        }

        public static bool IsFolder(string path)
        {
            var fullPath = Path.Combine(StaticHtmlAbsolutePath, path);
            if (Directory.Exists(fullPath))
            {
                return true;
            }
            return false;
        }

        public static string GetRootDirectory(string path)
        {
            var splitPath = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var baseDirectory = splitPath.First();
            return baseDirectory;
        }

        public static string GetRelativeDirectory(string path)
        {
            var fileName = Path.GetFileName(path);
            if (fileName != null)
            {
                var baseDirectory = path.Substring(0, path.Length - fileName.Length);
                return baseDirectory;
            }
            return null;
        }

        public static DocumentInfo FindFile(string path)
        {
            var fullPath = Path.Combine(StaticHtmlAbsolutePath, path);
            DocumentInfo docInfo;
            using (var sr = new StreamReader(fullPath))
            {
                string rawJson = sr.ReadToEnd();
                docInfo = JsonConvert.DeserializeObject<DocumentInfo>(rawJson);
            }

            return docInfo;
        }

        public static List<string> FindFiles(string path)
        {
            var fullPath = Path.Combine(StaticHtmlAbsolutePath, path);
            var files = new List<string>();
            if (!Directory.Exists(fullPath))
            {
                return files;
            }

            var filePaths = Directory.GetFiles(fullPath);
            files = new List<string>(filePaths);
            files = files.Where(n => n != "solutionInfo.json").ToList();
            return files;
        }

        internal static List<string> FindFolders(string path)
        {
            var fullPath = Path.Combine(StaticHtmlAbsolutePath, path);
            var directories = new List<string>();
            if (!Directory.Exists(fullPath))
            {
                return directories;
            }

            var directoryPaths = Directory.GetDirectories(fullPath);
            directories = new List<string>(directoryPaths);
            return directories;
        }


        private static JObject GetSolutionInfo(string userName, string repoName, string solutionName, out string solutionInfoPath)
        {
            string absolutePath = Path.Combine(StaticHtmlAbsolutePath, userName, repoName, solutionName);
            solutionInfoPath = Path.Combine(absolutePath, "solutionInfo.json");

            if (!File.Exists(solutionInfoPath))
            {
                return null;
            }

            using (var sr = new StreamReader(solutionInfoPath))
            {
                var rawJson = sr.ReadToEnd();
                var json = JObject.Parse(rawJson);
                return json;
            }
        }

        private static string CreatePath(string part1, string part2 = null, string part3 = null, string part4 = null)
        {
            string[] pathParts;
            if (part4 != null)
            {
                pathParts = new[] { part1, part2, part3, part4 };
            }
            else if (part3 != null)
            {
                pathParts = new[] { part1, part2, part3 };
            }
            else if (part2 != null)
            {
                pathParts = new[] { part1, part2 };
            }
            else
            {
                pathParts = new[] { part1 };
            }

            return string.Join("/", pathParts);
        }
    }
}