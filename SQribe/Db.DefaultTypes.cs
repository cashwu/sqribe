// Copyright (c) Fynydd LLC.
// Licensed under the GNU GPLv3 License.

using System;
using System.IO;
using System.Threading;
using Fynydd.Halide;

namespace SQribe
{
    public interface IDefaultTypes
	{
        ISettings settings { get; }

        IOutput output { get; }

        IHelpers helpers { get; }

        void GenerateCreateScript(ref int counter, ref int total, long groupToken);

        void DropAll(long token);

        void Restore(long token);
    }

	public class DefaultTypes : IDefaultTypes
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

        public DefaultTypes(ISettings singletonSettings, IOutput singletonOutput, IHelpers singletonHelpers)
        {
            _settings = singletonSettings;
            _output = singletonOutput;
            _helpers = singletonHelpers;
        }

		/// <summary>
		/// Generate script to create default types.
		/// </summary>
        public void GenerateCreateScript(ref int counter, ref int total, long groupToken)
        {
            if (settings.SqlObjects.Contains(",dt,") && settings.Abort == false)
            {
                var objectName = "default type";
                var prefix = objectName.PluralizeNoun(2).ToUpperFirstCharacter();
                var startDate = DateTime.Now;
                var lastTimeUpdate = "";
                int currentCount = 0;
                int totalCount = 0;

                helpers.GenerateCreateScript (
                    objectName, 
                    settings.OutputPath + settings.DefaultTypesFilename, 
                    (script, token) => {

                            using (var reader = new DataReader(helpers.LoadScript("select-type-default-create-scripts.sql"), settings.DataSource, useRewind: true))
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
                                            while (reader.Read() && settings.Abort == false)
                                            {
                                                totalCount++;
                                            }
                                        }
                                    }

                                    if (reader.Rewind())
                                    {
                                        if (reader.HasRows)
                                        {
                                            while (reader.Read() && settings.Abort == false)
                                            {
                                                currentCount++;
                                                script += Constants.LineFeed;
                                                script += "-- SQRIBE/OBJ;" + settings.Hash + Constants.LineFeed;
                                                script += reader["CREATE_SCRIPT"].TrimEnd(new[] { '\n' }).TrimEnd(new[] { '\r' }) + Constants.LineFeed;
                                                script += "GO -- SQRIBE/GO;" + settings.Hash + Constants.LineFeed;

                                                helpers.ShowPercentageComplete(token, currentCount, totalCount, startDate, ref lastTimeUpdate, prefix + " ");
                                            }
                                        }
                                    }
                                }
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
                ",dt,", 
                settings.ScriptPath + settings.DefaultTypesFilename, 
                "default type", 
                "drops" + Path.DirectorySeparatorChar.ToString() + "drop-default-types.sql"
                );
        }

        public void Restore(long token)
        {
            helpers.RestoreObject(
                token, 
                ",dt,", 
                "default type", 
                settings.ScriptPath + settings.DefaultTypesFilename
                );
        }
    }
}
