// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
namespace SolidityAST
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Newtonsoft.Json;

    public class SolidityCompiler
    {
        private static int solcTimeoutInMilliseconds = TimeSpan.FromSeconds(5).Seconds * 1000;

        public CompilerOutput Compile(string solcPath, string derivedFilePath)
        {
            if (!File.Exists(solcPath))
            {
                throw new SystemException($"Cannot find solidity compiler at {solcPath}");
            }

            derivedFilePath = derivedFilePath.Replace("\\", "/" /*, StringComparison.CurrentCulture*/);

            string jsonString = RunSolc(solcPath, derivedFilePath);

            Console.WriteLine("Raw solc json output");
            Console.WriteLine("How to read this voodoo: https://solidity.readthedocs.io/en/v0.6.6/using-the-compiler.html#output-description");
            Console.WriteLine();
            Console.WriteLine(jsonString);
            Console.WriteLine();

            List<string> errors = new List<string>();
            var settings = new JsonSerializerSettings
            {
                Error = (sender, errorArgs) =>
                {
                    errors.Add(errorArgs.ErrorContext.Error.Message);
                    errorArgs.ErrorContext.Handled = true;
                },
            };

            if (errors.Count != 0)
            {
                throw new SystemException($"Deserialization of Solidity compiler output failed with errors: {JsonConvert.SerializeObject(errors)}");
            }

            CompilerOutput compilerOutput = JsonConvert.DeserializeObject<CompilerOutput>(jsonString);

            return compilerOutput;
        }

        /// <summary>
        /// </summary>
        /// <param name="solcPath"></param>
        /// <param name="derivedFilePath">Path to the top-level solidty file</param>
        /// <returns></returns>
        private string RunSolc(string solcPath, string derivedFilePath)
        {
            string derivedFileName = Path.GetFileName(derivedFilePath);
            string containingDirectory = Path.GetDirectoryName(derivedFilePath);
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WorkingDirectory = containingDirectory;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = solcPath;
            p.StartInfo.Arguments = $"--standard-json --allow-paths --old-reporter {containingDirectory}";
            p.Start();

            string configString = "{ \"language\": \"Solidity\", \"sources\": { %SOLPLACEHOLDER%: { \"urls\": [ %URLPLACEHOLDER% ]}},"
                + "\"settings\": {\"evmVersion\": \"istanbul\", \"remappings\": [ \":openzeppelin-solidity/=../node_modules/openzeppelin-solidity/\" ], \"outputSelection\": {\"*\": {\"\": [ \"ast\" ]}}}}";
            configString = configString.Replace("%SOLPLACEHOLDER%", "\"" + derivedFileName + "\"" /*, StringComparison.CurrentCulture*/);
            configString = configString.Replace("%URLPLACEHOLDER%", "\"" + derivedFilePath + "\""/*, StringComparison.CurrentCulture*/);

            p.StandardInput.Write(configString);
            p.StandardInput.Close();
            string jsonString = p.StandardOutput.ReadToEnd();
            p.StandardOutput.Close();
            p.StandardError.Close();

            if (!p.WaitForExit(solcTimeoutInMilliseconds))
            {
                p.Kill();
                throw new SystemException("Killed Solidity compiler after 5s");
            }

            // Console.WriteLine(jsonString);
            return jsonString;
        }
    }
}
