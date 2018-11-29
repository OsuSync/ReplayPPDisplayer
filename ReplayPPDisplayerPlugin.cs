using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OsuRTDataProvider;
using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;
using OsuRTDataProvider.Mods;
using RealTimePPDisplayer;
using RealTimePPDisplayer.Beatmap;
using RealTimePPDisplayer.Calculator;
using RealTimePPDisplayer.Displayer;
using Sync.Plugins;

namespace ReplayPPDisplayer
{
    [SyncPluginDependency("8eb9e8e0-7bca-4a96-93f7-6408e76898a9", Version = "^1.5.0", Require = true)]
    public class ReplayPPDisplayerPlugin : Plugin
    {
        public const string VERSION = "0.0.1";
        public const string PLUGIN_NAME = "ReplayPPDisplayer";
        public const string PLGUIN_AUTHOR = "KedamaOvO";

        private PerformanceCalculatorBase _stdPpCalculator;
        private PerformanceCalculatorBase _taikoPpCalculator;
        private PerformanceCalculatorBase _maniaPpCalculator;
        private PerformanceCalculatorBase _ctbPpCalculator;

        private DisplayerBase _displayer = new DisplayerBase();

        private OsuListenerManager.OsuStatus _status;
        private Beatmap _beatmap;
        private FileSystemWatcher _watcher;

        public ReplayPPDisplayerPlugin() : base(PLUGIN_NAME, PLGUIN_AUTHOR)
        {
        }

        public override void OnEnable()
        {
            if (getHoster().EnumPluings().FirstOrDefault(p => p.Name == "OsuRTDataProvider") is OsuRTDataProviderPlugin ortdp &&
                getHoster().EnumPluings().FirstOrDefault(p => p.Name == "RealTimePPDisplayer") is RealTimePPDisplayerPlugin rtppd)
            {
                ortdp.ListenerManager.OnBeatmapChanged += (beatmap) => _beatmap = beatmap;
                ortdp.ListenerManager.OnStatusChanged += (last, cur) => _status = cur;
                ortdp.ListenerManager.OnStatusChanged += (last, cur) =>
                {
                    if (cur != OsuListenerManager.OsuStatus.Rank)
                    {
                        using (var mmf = MemoryMappedFile.CreateOrOpen("replay-pp", 2048))
                        {
                            var buf = new byte[] {0};
                            mmf.CreateViewStream().Write(buf,0,1);
                        }
                    }

                    if (cur != OsuListenerManager.OsuStatus.NoFoundProcess ||
                        cur != OsuListenerManager.OsuStatus.Unkonwn)
                    {
                        if (_watcher == null)
                        {
                            string replayDirectory = Path.GetDirectoryName(Process.GetProcessesByName("osu!").FirstOrDefault()?.MainModule.FileName);
                            replayDirectory = Path.Combine(replayDirectory, "Replays");

                            _watcher = new FileSystemWatcher(replayDirectory);
                            _watcher.NotifyFilter = NotifyFilters.LastWrite;
                            _watcher.EnableRaisingEvents = true;
                            _watcher.Changed += (s, e) =>
                            {
                                _watcher.EnableRaisingEvents = false;
                                Task.Run(() =>
                                {
                                    Thread.Sleep(500);
                                    _watcher.EnableRaisingEvents = true;
                                });

                                if (_status == OsuListenerManager.OsuStatus.Rank)
                                {
                                    var replay = new ReplayParser(e.FullPath);
                                    var cal = GetCalculator(replay.Mode);

                                    cal.Beatmap = new BeatmapReader(_beatmap,replay.Mode);
                                    cal.Count300 = replay.Count300;
                                    cal.Count100 = replay.Count100;
                                    cal.Count50 = replay.Count50;
                                    cal.CountGeki = replay.CountGeki;
                                    cal.CountKatu = replay.CountKatu;
                                    cal.CountMiss = replay.CountMiss;

                                    cal.Score = replay.Score;
                                    cal.MaxCombo = replay.MaxCombo;

                                    cal.Mods = replay.Mods;
                                    cal.Time = int.MaxValue;

                                    var ppTuple = cal.GetPerformance();
                                    using (var mmf = MemoryMappedFile.CreateOrOpen("replay-pp", 2048))
                                    {
                                        using (var sw = new StreamWriter(mmf.CreateViewStream()))
                                        {
                                            var ppStr = _displayer.FormatPp(ppTuple);
                                            sw.Write(ppStr);
                                        }
                                    }

                                    var beatmap = cal.Beatmap.OrtdpBeatmap;
                                    var mods = cal.Mods;
                                    string songs = $"{beatmap.Artist} - {beatmap.Title}[{beatmap.Difficulty}]";
                                    string acc = $"{cal.Accuracy:F2}%";
                                    string modsStr = $"{(mods != ModsInfo.Mods.None ? "+" + mods.ShortName : "")}";
                                    string pp = $"{cal.GetPerformance().RealTimePP:F2}pp";
                                    string msg = $"[Replay]{songs} {modsStr} | {acc} => {pp} ({replay.Mode})";
                                    Sync.Tools.IO.CurrentIO.WriteColor(msg, ConsoleColor.Blue);
                                }
                            };
                        }
                    }
                };
            }
        }

        private PerformanceCalculatorBase GetCalculator(OsuPlayMode mode)
        {
            PerformanceCalculatorBase calculator;
            switch (mode)
            {
                case OsuPlayMode.Osu:
                    _stdPpCalculator = _stdPpCalculator ?? new StdPerformanceCalculator();
                    calculator = _stdPpCalculator;
                    break;
                case OsuPlayMode.Taiko:
                    _taikoPpCalculator = _taikoPpCalculator ?? new TaikoPerformanceCalculator();
                    calculator = _taikoPpCalculator;
                    break;
                case OsuPlayMode.Mania:
                    _maniaPpCalculator = _maniaPpCalculator ?? new ManiaPerformanceCalculator();
                    calculator = _maniaPpCalculator;
                    break;
                case OsuPlayMode.CatchTheBeat:
                    _ctbPpCalculator = _ctbPpCalculator ?? new CatchTheBeatPerformanceCalculator();
                    calculator = _ctbPpCalculator;
                    break;
                default:
                    Sync.Tools.IO.CurrentIO.WriteColor($"[ReplayPPDisplay]Unknown Mode! Mode:0x{(int)mode:X8}", ConsoleColor.Red);
                    calculator = null;
                    break;
            }

            calculator?.ClearCache();

            return calculator;
        }
    }
}
