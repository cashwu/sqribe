// Copyright (c) Fynydd LLC.
// Licensed under the GNU GPLv3 License.

using System;
using System.IO;
using System.Threading;
using Fynydd.Halide;

namespace SQribe
{
    public interface IFullTextCatalogs
	{
        ISettings settings { get; }

        IOutput output { get; }

        IHelpers helpers { get; }

        void GenerateCreateScript(ref int counter, ref int total, long groupToken);

        void DropAll(long token);

        void Restore(long token);
    }


	public class FullTextCatalogs : IFullTextCatalogs
	{
        #region Public properties

        public ISettings settings
        {
            get
            {
                return _settings;
            }
        }

        public IOutput output
        {
            get
            {
                return _output;
            }
        }

        public IHelpers helpers
        {
            get
            {
                return _helpers;
            }
        }

        #endregion

        #region Private properties

        private readonly ISettings _settings;
        private readonly IOutput _output;
        private readonly IHelpers _helpers;

        #endregion

        public FullTextCatalogs(ISettings singletonSettings, IOutput singletonOutput, IHelpers singletonHelpers)
        {
            _settings = singletonSettings;
            _output = singletonOutput;
            _helpers = singletonHelpers;
        }

		/// <summary>
		/// Generate script to create fulltext catalogs and indexes functions.
		/// </summary>
        public void GenerateCreateScript(ref int counter, ref int total, long groupToken)
        {
            if (settings.SqlObjects.Contains(",ftc,") && settings.Abort == false)
            {
                var objectName = "fulltext catalog";
                var prefix = objectName.PluralizeNoun(2).ToUpperFirstCharacter();
                var startDate = DateTime.Now;
                var lastTimeUpdate = "";
                int currentCount = 0;
                int totalCount = 0;

                helpers.GenerateCreateScript (
                    objectName, 
                    settings.OutputPath + settings.FulltextCatalogsFilename, 
                    (script, token) => {
                        
                        using (var reader = new DataReader(helpers.LoadScript("generate-fulltext-catalogs.sql"), settings.DataSource, useRewind: true))
                        {
                            if (settings.Abort == false)
                            {
                                var cts = new CancellationTokenSource();
                                var task = reader.ExecuteAsync(cts.Token);

                                while (task.IsCompleted == false)
                                {
                                    if (settings.Abort)
                                    {
                                        cts.Cancel();
                                    }

                                    System.Threading.Thread.Sleep(Constants.SleepNumber);
                                }

                                if (reader.IsReady)
                                {
                                    if (reader.HasRows)
                                    {
                                        do
                                        {
                                            while (reader.Read() && settings.Abort == false)
                                            {
                                                totalCount++;
                                            }

                                            System.Threading.Thread.Sleep(Constants.SleepNumber);

                                        } while (reader.NextResult() && settings.Abort == false);
                                    }
                                }
                            }

                            if (settings.Abort == false)
                            {
                                if (reader.Rewind())
                                {
                                    if (reader.HasRows)
                                    {
                                        do
                                        {
                                            while (reader.Read() && settings.Abort == false)
                                            {
                                                if (reader[0].StartsWith("CREATE FULLTEXT CATALOG"))
                                                {
                                                    currentCount++;

                                                    script += "-- SQRIBE/OBJ;" + settings.Hash + Constants.LineFeed;
                                                }

                                                script += reader[0] + Constants.LineFeed;

                                                helpers.ShowPercentageComplete(token, currentCount, totalCount, startDate, ref lastTimeUpdate, prefix + " ");
                                            }

                                            System.Threading.Thread.Sleep(Constants.SleepNumber);

                                        } while (reader.NextResult() && settings.Abort == false);
                                    }
                                }
                            }
                        }

                        if (currentCount > 0)
                        {
                            script = script.StandardizeLineEndings().Replace("GO" + Constants.LineFeed, "GO -- SQRIBE/GO;" + settings.Hash + Constants.LineFeed);
                        }

                        return Tuple.Create(script, currentCount);
                    },
                    ref counter, ref total, groupToken
                );
            }
        }

        public void DropAll(long token)
        {
            helpers.DropObject(
                token, 
                ",ftc,", 
                settings.ScriptPath + settings.FulltextCatalogsFilename, 
                "fulltext catalog", 
                "drops" + Path.DirectorySeparatorChar.ToString() + "drop-fulltext-catalogs.sql"
                );
        }

        public void Restore(long token)
        {
            helpers.RestoreObject(
                token, 
                ",ftc,", 
                "fulltext catalog", 
                settings.ScriptPath + settings.FulltextCatalogsFilename
                );
        }
    }
}
