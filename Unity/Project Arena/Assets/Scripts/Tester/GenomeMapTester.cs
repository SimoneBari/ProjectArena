using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AI;
using BehaviorDesigner.Runtime;
using Logging;
using Managers;
using Managers.Map;
using Managers.Mode;
using Maps.Genomes;
using Maps.MapGenerator;
using Newtonsoft.Json;
using UnityEngine;

namespace Tester
{
    /// <summary>
    /// Deals with instantiating an experiment to test a specific map generated from a given genome.
    /// </summary>
    public class GenomeMapTester : MonoBehaviour
    {
        private const int DEFAULT_GAME_LENGTH = 240;

        private const int BOT1_ID = 1;
        private const int BOT2_ID = 2;

        [SerializeField] private GameObject botPrefab;
        [SerializeField] private string genomeName;
        [SerializeField] private string bot1ParamsFilenamePrefix;
        [SerializeField] private string bot2ParamsFilenamePrefix;
        [SerializeField] private SLMapManager mapManager;
        [SerializeField] private GenomeV2MapGenerator genomeMapGenerator;
        [SerializeField] private SpawnPointManager spawnPointManager;
        [SerializeField] private int numExperiments = 1;
        [SerializeField] private string experimentName = "experiment";

        private readonly List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
        private GameResultsAnalyzer analyzer;
        private string botsPath;

        private int experimentNumber;
        private string importPath;

        private GenomeTesterGameManager manager;
        private string genomesPath;

        private int gameLength = DEFAULT_GAME_LENGTH;
        
        private void Awake()
        {
#if !UNITY_EDITOR
                Time.captureFramerate = 24;
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
#endif
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith("-bot1file=")) bot1ParamsFilenamePrefix = arg.Substring(10);
                if (arg.StartsWith("-bot2file=")) bot2ParamsFilenamePrefix = arg.Substring(10);
                if (arg.StartsWith("-numExperiments=")) numExperiments = int.Parse(arg.Substring(16));
                if (arg.StartsWith("-experimentName=")) experimentName = arg.Substring(16);
                if (arg.StartsWith("-genomeName=")) genomeName = arg.Substring(12);
                if (arg.StartsWith("-gameLength=")) gameLength = int.Parse(arg.Substring(12));
            }

            importPath = Application.persistentDataPath + "/Import/";
            genomesPath = importPath + "Genomes/";
            botsPath = importPath + "Bots/";

            if (!Directory.Exists(importPath)) Directory.CreateDirectory(importPath);
            if (!Directory.Exists(genomesPath)) Directory.CreateDirectory(genomesPath);
            if (!Directory.Exists(botsPath)) Directory.CreateDirectory(botsPath);

            analyzer = new GameResultsAnalyzer(BOT1_ID, BOT2_ID);
            analyzer.Setup();

            StartNewExperimentGameEvent.Instance.AddListener(NewExperimentStarted);
            ExperimentEndedGameEvent.Instance.AddListener(ExperimentEnded);
            
            StartNewExperiment();

            // Register start and end experiment events, so that I can finalize the analyzer and
            // reset it
        }
        
        private void StartNewExperiment()
        {
            analyzer.Reset();

            var bot1Params = ReadFromFile(botsPath + bot1ParamsFilenamePrefix + "params.json",
                BotCharacteristics.Default);
            var bot2Params = ReadFromFile(botsPath + bot2ParamsFilenamePrefix + "params.json",
                BotCharacteristics.Default);

            var defaultGuns = new[] {true, true, true, true};
            var activeGunsBot1 = ReadFromFile(botsPath + bot1ParamsFilenamePrefix + "guns.json", defaultGuns);
            var activeGunsBot2 = ReadFromFile(botsPath + bot2ParamsFilenamePrefix + "guns.json", defaultGuns);

            var genome = ReadFromFile(genomesPath + genomeName, GraphGenomeV2.Default);
            if (!genome.IsGenomeValid())
            {
                Debug.LogError("Genome read from file is invalid!");
                genome = GraphGenomeV2.Default;
            }
            genomeMapGenerator.SetGenome(genome);

            manager = gameObject.AddComponent<GenomeTesterGameManager>();
            manager.SetParameters(
                botPrefab,
                BOT1_ID,
                bot1Params,
                activeGunsBot1,
                BOT2_ID,
                bot2Params,
                activeGunsBot2,
                mapManager,
                spawnPointManager,
                gameLength,
                respawnDuration: 1.0f
            );
        }

        private void ExperimentEnded()
        {
            Debug.Log("Experiment num " + +experimentNumber + " ended!");

            manager.StopGame();
            Destroy(manager);

            // TODO provide correct length 
            results.Add(analyzer.CompileResults(DEFAULT_GAME_LENGTH));

            experimentNumber++;

            if (experimentNumber >= numExperiments)
            {
                ExportResults(JsonConvert.SerializeObject(results), experimentName);
                Application.Quit();
            }
            else
            {
                mapManager.ResetMap();
                spawnPointManager.Reset();

                StartCoroutine(WaitAndStart());
            }
        }

        private static void ExportResults(string compileResults, string experimentName)
        {
            var exportPath = Application.persistentDataPath + "/Export/" + experimentName;
            var filePath = exportPath + "." + "json";
            try
            {
                using var writer = new StreamWriter(filePath);
                writer.Write(compileResults);
            }
            catch (Exception e)
            {
                Debug.LogError("Couldn't export results!, error " + e.Message);
            }
        }

        private IEnumerator WaitAndStart()
        {
            yield return new WaitForSeconds(1);
            StartNewExperiment();
        }

        private void NewExperimentStarted()
        {
            Debug.Log("Experiment num " + experimentNumber + " started!");
        }

        private static T ReadFromFile<T>(string filePath, T defaultValue)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            catch (Exception)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(defaultValue, Formatting.Indented);
                    using var writer = new StreamWriter(filePath);
                    writer.Write(json);
                    writer.Close();
                }
                catch (Exception)
                {
                    // Ignored, could not generate default file.
                }
            }

            return defaultValue;
        }

        private static Tuple<BotCharacteristics, bool[]> LoadBotCharacteristics(string botsPath, string botFilename)
        {
            var paramsFile = botsPath + botFilename + "params.json";
            var botParams = BotCharacteristics.Default;
            try
            {
                using var reader = new StreamReader(paramsFile);
                botParams = JsonConvert.DeserializeObject<BotCharacteristics>(reader.ReadToEnd());
            }
            catch (Exception)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(botParams, Formatting.Indented);
                    using var writer = new StreamWriter(paramsFile);
                    writer.Write(json);
                    writer.Close();
                }
                catch (Exception)
                {
                    // Ignored, could not generate default file.
                }
            }

            var gunsFile = botsPath + botFilename + "guns.json";
            var guns = new[] {true, true, true, true};
            try
            {
                using var reader = new StreamReader(gunsFile);
                guns = JsonConvert.DeserializeObject<bool[]>(reader.ReadToEnd());
            }
            catch (Exception)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(guns);
                    using var writer = new StreamWriter(gunsFile);
                    writer.Write(json);
                    writer.Close();
                }
                catch (Exception)
                {
                    // Ignored, could not generate default file.
                }
            }

            return new Tuple<BotCharacteristics, bool[]>(botParams, guns);
        }
    }
}