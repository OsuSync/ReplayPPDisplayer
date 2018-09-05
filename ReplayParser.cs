using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OsuRTDataProvider.Listen;
using OsuRTDataProvider.Mods;

namespace ReplayPPDisplayer
{
    class ReplayParser
    {
        public OsuPlayMode Mode { get; private set; }
        public int Version { get; private set; }
        public string BeatmapMd5 { get; private set; }
        public string Player { get; private set; }
        public string ReplaypMd5 { get; private set; }

        public int Count300 { get; private set; }
        public int Count100 { get; private set; }
        public int Count50 { get; private set; }
        public int CountGeki { get; private set; }
        public int CountKatu { get; private set; }
        public int CountMiss { get; private set; }

        public int Score { get; private set; }
        public int MaxCombo { get; private set; }

        public bool Perfect { get; private set; }

        public ModsInfo Mods { get; set; }

        public ReplayParser(string osr)
        {
            Parse(osr);
        }

        private void Parse(string osr)
        {
            int retryCount = 5;
            while (retryCount != 0)
            {
                try
                {
                    using (var br = new BinaryReader(File.OpenRead(osr)))
                    {
                        Mode = (OsuPlayMode) br.ReadByte();
                        Version = br.ReadInt32();
                        int flag = br.ReadByte();
                        BeatmapMd5 = br.ReadString();
                        flag = br.ReadByte();
                        Player = br.ReadString();
                        flag = br.ReadByte();
                        ReplaypMd5 = br.ReadString();
                        Count300 = br.ReadInt16();
                        Count100 = br.ReadInt16();
                        Count50 = br.ReadInt16();
                        CountGeki = br.ReadInt16();
                        CountKatu = br.ReadInt16();
                        CountMiss = br.ReadInt16();

                        Score = br.ReadInt32();
                        MaxCombo = br.ReadInt16();

                        Perfect = br.ReadByte() == 1;

                        Mods = new ModsInfo()
                        {
                            Mod = (ModsInfo.Mods) br.ReadInt32()
                        };
                    }
                    break;
                }
                catch (IOException e)
                {
                    Thread.Sleep(100);
                    retryCount--;
                }
            }
        }
    }
}
