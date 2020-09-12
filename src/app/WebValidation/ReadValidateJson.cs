﻿using CSE.WebValidate.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace CSE.WebValidate
{
    public partial class WebV : IDisposable
    {
        /// <summary>
        /// Load the requests from json files
        /// </summary>
        /// <param name="fileList">list of files to load</param>
        /// <returns>sorted List or Requests</returns>
        private List<Request> LoadValidateRequests(List<string> fileList)
        {
            List<Request> list;
            List<Request> fullList = new List<Request>();

            // read each json file
            foreach (string inputFile in fileList)
            {
                list = ReadJson(inputFile);

                // add contents to full list
                if (list != null && list.Count > 0)
                {
                    fullList.AddRange(list);
                }
            }

            // return null if can't read and validate the json files
            if (fullList == null || fullList.Count == 0 || !ValidateJson(fullList))
            {
                return null;
            }

            // return sorted list
            return fullList;
        }

        /// <summary>
        /// Load performance targets from json
        /// </summary>
        /// <returns>Dictionary of PerfTarget</returns>
        private Dictionary<string, PerfTarget> LoadPerfTargets()
        {
            const string perfFileName = "perfTargets.txt";

            string content = ReadTestFile(perfFileName);

            if (!string.IsNullOrWhiteSpace(content))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, PerfTarget>>(content);
            }

            // return empty dictionary - perf targets are not required
            return new Dictionary<string, PerfTarget>();
        }

        /// <summary>
        /// Reads a file from local or --base-url
        /// </summary>
        /// <param name="file">file name</param>
        /// <returns>file contents</returns>
        public string ReadTestFile(string file)
        {
            string content = string.Empty;

            if (string.IsNullOrWhiteSpace(file))
            {
                throw new ArgumentNullException(nameof(file));
            }


            if (string.IsNullOrEmpty(config.BaseUrl))
            {
                // check for file exists
                if (string.IsNullOrEmpty(file) || !File.Exists(file))
                {
                    Console.WriteLine($"File Not Found: {file}");
                    return null;
                }

                // read the file
                content = File.ReadAllText(file);

                // check for empty file
                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine($"Unable to read file {file}");
                    return null;
                }
            }
            else
            {
                string path = config.BaseUrl + file;

                using HttpClient client = new HttpClient();

                try
                {
                    content = client.GetStringAsync(new Uri(path)).Result;

                    // check for empty file
                    if (string.IsNullOrEmpty(content))
                    {
                        Console.WriteLine($"Unable to read file {path}");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return null;
                }
            }

            return content;
        }

        /// <summary>
        /// Read a json test file
        /// </summary>
        /// <param name="file">file path</param>
        /// <returns>List of Request</returns>
        public List<Request> ReadJson(string file)
        {
            string json = ReadTestFile(file);

            if (string.IsNullOrWhiteSpace(json))
            {
                Console.WriteLine($"Invalid json in file: {file}");
                return null;
            }

            return LoadJson(json);
        }

        /// <summary>
        /// Load the json string into a List of Requests
        /// </summary>
        /// <param name="json">json string</param>
        /// <returns>List of Request or null</returns>
        private List<Request> LoadJson(string json)
        {
            try
            {
                List<Request> list = null;
                InputJson data = null;
                List<Request> l2 = new List<Request>();

                try
                {
                    // try to parse the json
                    data = JsonConvert.DeserializeObject<InputJson>(json);
                }
                catch
                {
                    // try to read the array of Requests style document
                    // this is being deprecated in v1.4
                    list = JsonConvert.DeserializeObject<List<Request>>(json);
                }

                // replace placedholders with environment variables
                if (data != null && data.Requests.Count > 0)
                {
                    if (data.Variables != null && data.Variables.Count > 0)
                    {
                        foreach (string v in data.Variables)
                        {
                            json = json.Replace("${" + v + "}", System.Environment.GetEnvironmentVariable(v), StringComparison.Ordinal);
                        }

                        // reload from json
                        data = JsonConvert.DeserializeObject<InputJson>(json);
                    }

                    list = data.Requests;
                }

                if (list != null && list.Count > 0)
                {
                    foreach (Request r in list)
                    {
                        // Add the default perf targets if exists
                        if (r.PerfTarget != null && r.PerfTarget.Quartiles == null)
                        {
                            if (Targets.ContainsKey(r.PerfTarget.Category))
                            {
                                r.PerfTarget.Quartiles = Targets[r.PerfTarget.Category].Quartiles;
                            }
                        }

                        l2.Add(r);
                    }

                    // success
                    return l2;
                }

                Console.WriteLine("Invalid JSON file");
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            // couldn't read the list
            return null;
        }

        /// <summary>
        /// Validate all of the requests
        /// </summary>
        /// <param name="requests">list of Request</param>
        /// <returns></returns>
        private static bool ValidateJson(List<Request> requests)
        {
            // validate each request
            foreach (Request r in requests)
            {
                ValidationResult result = Parameters.Validator.Validate(r);
                if (result.Failed)
                {
                    Console.WriteLine($"Error: Invalid json\n\t{JsonConvert.SerializeObject(r, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })}\n\t{string.Join("\n", result.ValidationErrors)}");
                    return false;
                }
            }

            // validated
            return true;
        }
    }
}