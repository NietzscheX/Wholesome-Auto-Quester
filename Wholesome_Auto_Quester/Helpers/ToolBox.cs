﻿using Newtonsoft.Json;
using robotManager.Helpful;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Wholesome_Auto_Quester.Bot;
using Wholesome_Auto_Quester.Database.Models;
using wManager;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using static wManager.Wow.Helpers.PathFinder;
using Math = System.Math;

namespace Wholesome_Auto_Quester.Helpers
{
    public static class ToolBox
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();
        public static readonly Random Rnd = new Random();

        private static HashSet<int> _completeQuests;

        private static Dictionary<int, bool[]> _objectiveCompletionDict = new Dictionary<int, bool[]>();

        public static readonly Dictionary<int, int> ZoneLevelDictionary = new Dictionary<int, int> {
            {14, 10}, //Kalimdor
            {15, 10}, //Azeroth
            {465, 1}, //AzuremystIsle
            {28, 1}, //DunMorogh
            {5, 1}, //Durotar
            {31, 1}, //Elwynn
            {463, 1}, //EversongWoods
            {42, 1}, //Teldrassil
            {21, 1}, //Tirisfal
            {481, 10}, //SilvermoonCity
            {11, 10}, //Barrens
            {477, 10}, //BloodmystIsle
            {43, 10}, //Darkshore
            {464, 10}, //Ghostlands
            {342, 10}, //Ironforge
            {36, 10}, //LochModan
            {10, 1}, //Mulgore
            {322, 10}, //Ogrimmar
            {22, 10}, //Silverpine
            {302, 10}, //Stormwind
            {472, 10}, //TheExodar
            {363, 10}, //ThunderBluff
            {383, 10}, //Undercity
            {40, 10}, //Westfall
            {37, 15}, //Redridge
            {82, 15}, //StonetalonMountains
            {44, 18}, //Ashenvale
            {35, 18}, //Duskwood
            {25, 20}, //Hilsbrad
            {41, 20}, //Wetlands
            {62, 25}, //ThousandNeedles
            {16, 30}, //Alterac
            {17, 30}, //Arathi
            {102, 30}, //Desolace
            {142, 30}, //Dustwallow
            {38, 30}, //Stranglethorn
            {18, 35}, //Badlands
            {39, 35}, //SwampOfSorrows
            {27, 40}, //Hinterlands
            {162, 40}, //Tanaris
            {122, 42}, //Feralas
            {182, 45}, //Aszhara
            {20, 45}, //BlastedLands
            {29, 45}, //SearingGorge
            {183, 48}, //Felwood
            {202, 48}, //UngoroCrater
            {30, 50}, //BurningSteppes
            {23, 51}, //WesternPlaguelands
            {24, 53}, //EasternPlaguelands
            {282, 53}, //Winterspring
            {242, 55}, //Moonglade
            {262, 55}, //Silithus
            {466, 58}, //Hellfire
            {467, 60}, //Zangarmarsh
            {479, 62}, //TerokkarForest
            {476, 65}, //BladesEdgeMountains
            {478, 65}, //Nagrand
            {480, 67}, //Netherstorm
            {474, 67}, //ShadowmoonValley
            {482, 65}, //ShattrathCity
            {487, 68}, //BoreanTundra
            {32, 68}, //DeadwindPass
            {492, 68}, //HowlingFjord
            {489, 71}, //Dragonblight
            {491, 73}, //GrizzlyHills
            {497, 75}, //ZulDrak
            {494, 76}, //SholazarBasin
            {511, 77}, //CrystalsongForest
            {542, 77}, //HrothgarsLanding
            {605, 77}, //IcecrownCitadel
            {505, 80} //Dalaran
        };

        public static long CurTime => Watch.ElapsedMilliseconds;

        public static bool HostilesAreAround(WoWObject POI, float clearDistance = 25f)
        {
            //Logger.Log($"CHECK - Mounted = {ObjectManager.Me.IsMounted}, incomb = {ObjectManager.Me.InCombatFlagOnly}, dist={POI.GetDistance}");
            WoWUnit poiUnit = POI is WoWUnit ? (WoWUnit)POI : null;
            WoWUnit me = ObjectManager.Me;
            if (me.IsMounted && (me.InCombatFlagOnly || POI.GetDistance < 60 && poiUnit?.Reaction == Reaction.Hostile))
                MountTask.DismountMount(false, false);

            if (ObjectManager.Me.InCombatFlagOnly)
                return true;

            List<WoWObject> objectManager = ObjectManager.GetObjectWoW()
                .FindAll(o => o.Type == WoWObjectType.Unit);
            Dictionary<WoWUnit, float> hostileUnits = new Dictionary<WoWUnit, float>();
            foreach (WoWUnit unit in objectManager)
            {
                if (unit.IsAlive && unit.IsAttackable && unit.Reaction == Reaction.Hostile && unit.Guid != POI.Guid
                    && unit.Position.DistanceTo(POI.Position) < clearDistance)
                {
                    WAQPath pathFromPoi = GetWAQPath(unit.Position, POI.Position);
                    if (pathFromPoi.Distance < clearDistance)
                        hostileUnits.Add(unit, pathFromPoi.Distance);
                }
            }

            bool poiIsHostileUnit = poiUnit != null && poiUnit.Reaction == Reaction.Hostile;
            int maxCount = poiIsHostileUnit ? 2 : 3;
            if (hostileUnits.Where(u => u.Key.Level >= me.Level && POI.Position.DistanceTo(u.Key.Position) < 18).Count() >= maxCount
                || hostileUnits.Where(u => u.Key.Level >= me.Level - 2 && POI.Position.DistanceTo(u.Key.Position) < 18).Count() >= maxCount + 1)
            {
                if (Fight.InFight) Fight.StopFight();
                MoveHelper.StopAllMove();
                BlacklistHelper.AddNPC(POI.Guid, "Surrounded by hostiles");
                BlacklistHelper.AddZone(POI.Position, 20, "Surrounded by hostiles");
                WAQTasks.TaskInProgress.PutTaskOnTimeout(600, $"{POI.Name} is surrounded by hostiles");
                Main.RequestImmediateTaskReset = true;
                return true;
            }

            int addedDistCheck = poiIsHostileUnit ? 0 : 15; // We check further if it's not an enemy
            IOrderedEnumerable<KeyValuePair<WoWUnit, float>> hostilesInFront = hostileUnits
                .Where(u => u.Key.GetDistance < POI.GetDistance + addedDistCheck)
                .OrderBy(u => u.Key.GetDistance);
            if (hostilesInFront.Count() > 0)
            {
                if (Fight.InFight) Fight.StopFight();
                Logger.Log($"Fighting {hostileUnits.FirstOrDefault().Key.Name} to clear POI zone");
                Fight.StartFight(hostileUnits.FirstOrDefault().Key.Guid);
                return true;
            }
            return false;
        }

        public static T TakeHighest<T>(this IEnumerable<T> list, Func<T, int> takeValue, out int amount)
        {
            var highest = int.MinValue;
            T curHighestElement = default;

            foreach (T element in list)
            {
                int curValue = takeValue(element);
                if (curValue > highest)
                {
                    highest = curValue;
                    curHighestElement = element;
                }
            }

            amount = highest;
            return curHighestElement;
        }

        public static T TakeHighest<T>(this IEnumerable<T> list, Func<T, int> takeValue) =>
            list.TakeHighest(takeValue, out _);

        public static float PathLength(List<Vector3> path)
        {
            var length = 0f;
            for (var i = 0; i < path.Count - 1; i++) length += path[i].DistanceTo(path[i + 1]);

            return length;
        }

        public static bool InInteractDistance(this WoWUnit unit) => unit.GetDistance < unit.CombatReach + 4f;

        public static WoWUnit FindClosestUnitByEntry(int entry)
        {
            Vector3 myPos = ObjectManager.Me.PositionWithoutType;
            return ObjectManager.GetWoWUnitByEntry(entry)
                .TakeHighest(unit => (int)-unit.PositionWithoutType.DistanceTo(myPos));
        }

        public static WoWGameObject FindClosestGameObjectByEntry(int entry)
        {
            Vector3 myPos = ObjectManager.Me.PositionWithoutType;
            return ObjectManager.GetWoWGameObjectByEntry(entry)
                .TakeHighest(gameObject => (int)-gameObject.Position.DistanceTo(myPos));
        }

        public static string EscapeLuaString(this string str) => str.Replace("\\", "\\\\").Replace("'", "\\'");

        public static float GetRealDistance(this WoWObject wObject) =>
            wObject.Type switch
            {
                WoWObjectType.Unit => ((WoWUnit)wObject).GetDistance,
                WoWObjectType.GameObject => ((WoWGameObject)wObject).GetDistance,
                WoWObjectType.Player => ((WoWPlayer)wObject).GetDistance,
                _ => 0f
            };

        public static bool IsNpcFrameActive() =>
            Lua.LuaDoString<bool>(
                "return GetClickFrame('GossipFrame'):IsVisible() == 1 or GetClickFrame('QuestFrame'):IsVisible() == 1;");

        public static bool GossipTurnInQuest(string questName)
        {
            // Select quest
            var exitCodeOpen = Lua.LuaDoString<int>($@"
            if GetClickFrame('QuestFrameAcceptButton'):IsVisible() == 1
                or GetClickFrame('QuestFrameCompleteButton'):IsVisible() == 1
                or GetClickFrame('QuestFrameCompleteQuestButton'):IsVisible() == 1 then return 0; end
            if GetClickFrame('QuestFrame'):IsVisible() == 1 then
            	for i=1, 32 do
            		local button = GetClickFrame('QuestTitleButton' .. i);
            		if button:IsVisible() ~= 1 then break; end
            		local text = button:GetText();
            		text = strsub(text, 11, strlen(text)-2);
            		if text == '{questName.EscapeLuaString()}' then
                        button:Click();
                        return 0;
                    end
            	end
            elseif GetClickFrame('GossipFrame'):IsVisible() == 1 then
            	local activeQuests = {{ GetGossipActiveQuests() }};
            	for j=1, GetNumGossipActiveQuests(), 1 do
            		local i = j*4-3;
            		if activeQuests[i] == '{questName.EscapeLuaString()}' then
            			if activeQuests[i+3] ~= 1 then return 3; end
            			SelectGossipActiveQuest(j);
            			return 0;
            		end
            	end
            else
            	return 1;
            end
            return 2;");
            switch (exitCodeOpen)
            {
                case 1:
                    Logger.LogError($"No Gossip window was open to hand in {questName}");
                    return false;
                case 2:
                    Logger.LogError($"The quest {questName} has not been found to hand in.");
                    return false;
                case 3:
                    Logger.LogError($"The quest {questName} has been found but is not completed yet.");
                    return false;
            }

            Thread.Sleep(200);

            var requiresItems = Lua.LuaDoString<bool>("return GetNumQuestItems() > 0;");
            if (requiresItems)
            {
                Lua.LuaDoString("CompleteQuest();");
                Thread.Sleep(200);
            }

            // Get reward
            var hasQuestReward = Lua.LuaDoString<bool>("return GetNumQuestChoices() > 0;");
            if (hasQuestReward)
            {
                // Ugly workaround to trigger the selection event
                Logger.LogDebug("Letting InventoryManager select quest reward.");
                Quest.CompleteQuest();
            }

            Thread.Sleep(200);

            // Finish it
            Lua.LuaDoString(
                $"if GetClickFrame('QuestFrame'):IsVisible() then GetQuestReward({(hasQuestReward ? "1" : "nil")}); end");
            Thread.Sleep(200);
            Lua.LuaDoString(@"
            local closeButton = GetClickFrame('QuestFrameCloseButton');
            if closeButton:IsVisible() then
            	closeButton:Click();
            end");

            Logger.Log($"Turned in quest {questName}.");

            return true;
        }

        public static bool GossipPickUpQuest(string questName, int questId)
        {
            // Select quest
            var exitCodeOpen = Lua.LuaDoString<int>($@"
            if GetClickFrame('QuestFrameAcceptButton'):IsVisible() == 1 or GetClickFrame('QuestFrameCompleteButton'):IsVisible() == 1 then return 0; end
            if GetClickFrame('QuestFrame'):IsVisible() == 1 then
            	for i=1, 32 do
            		local button = GetClickFrame('QuestTitleButton' .. i);
            		if button:IsVisible() ~= 1 then break; end
            		local text = button:GetText();
            		text = strsub(text, 11, strlen(text)-2);
            		if text == '{questName.EscapeLuaString()}' then
                        button:Click();
                        return 0;
                    end
            	end
            elseif GetClickFrame('GossipFrame'):IsVisible() == 1 then
            	local availableQuests = {{ GetGossipAvailableQuests() }};
            	for j=1, GetNumGossipAvailableQuests(), 1 do
            		local i = j*5-4;
            		if availableQuests[i] == '{questName.EscapeLuaString()}' then
            			SelectGossipAvailableQuest(j);
            			return 0;
            		end
            	end
                local autoCompleteQuests = {{ GetGossipActiveQuests() }}
            	for j=1, GetNumGossipActiveQuests(), 1 do
            		local i = j*4-3;
            		if autoCompleteQuests[i] == '{questName.EscapeLuaString()}' then
            			SelectGossipActiveQuest(j);
            			return 3;
            		end
            	end
            else
            	return 1;
            end
            return 2;");
            switch (exitCodeOpen)
            {
                case 1:
                    Logger.LogError($"No Gossip or Quest window was open to pick up {questName}");
                    return false;
                case 2:
                    Logger.LogError($"The quest {questName} has not been found to pick up.");
                    return false;
                case 3:
                    Logger.Log($"The quest {questName} is an autocomplete.");
                    Thread.Sleep(200);
                    Quest.CompleteQuest();
                    WAQTasks.MarQuestAsCompleted(questId);
                    return true;
            }

            Thread.Sleep(200);

            if (Lua.LuaDoString<bool>("return GetClickFrame('QuestFrameCompleteButton'):IsVisible() == 1;"))
            {
                Logger.LogError($"The quest {questName} seems to be a trade quest.");
                Lua.LuaDoString(@"
                local closeButton = GetClickFrame('QuestFrameCloseButton');
                if closeButton:IsVisible() then
                	closeButton:Click();
                end");
                return false;
            }

            // Finish it
            Lua.LuaDoString("if GetClickFrame('QuestFrame'):IsVisible() then AcceptQuest(); end");
            Thread.Sleep(200);
            Lua.LuaDoString(@"
            local closeButton = GetClickFrame('QuestFrameCloseButton');
            if closeButton:IsVisible() then
            	closeButton:Click();
            end");

            Logger.Log($"Picked up quest {questName}.");

            return true;
        }

        internal static int GetIndexOfClosestPoint(List<Vector3> path)
        {
            if (path == null || path.Count <= 0) return 0;
            Vector3 myPos = ObjectManager.Me.PositionWithoutType;

            var curIndex = 0;
            var curDistance = float.MaxValue;

            for (var i = 0; i < path.Count; i++)
            {
                float distance = myPos.DistanceTo(path[i]);
                if (distance < curDistance)
                {
                    curDistance = distance;
                    curIndex = i;
                }
            }

            return curIndex;
        }

        internal static float PointDistanceToLine(Vector3 start, Vector3 end, Vector3 point)
        {
            float vLenSquared = (start.X - end.X) * (start.X - end.X) +
                                (start.Y - end.Y) * (start.Y - end.Y) +
                                (start.Z - end.Z) * (start.Z - end.Z);
            if (vLenSquared == 0f) return point.DistanceTo(start);

            Vector3 ref1 = point - start;
            Vector3 ref2 = end - start;
            float clippedSegment = Math.Max(0, Math.Min(1, Vector3.Dot(ref ref1, ref ref2) / vLenSquared));

            Vector3 projection = start + (end - start) * clippedSegment;
            return point.DistanceTo(projection);
        }

        public static bool MoveToHotSpotAbortCondition(WAQTask task) =>
            WAQTasks.WoWObjectInProgress != null
            || !ObjectManager.Me.IsMounted && ObjectManager.Me.InCombatFlagOnly;

        public static void UpdateCompletedQuests()
        {
            List<int> completedQuests = new List<int>();
            completedQuests.AddRange(Quest.FinishedQuestSet);
            completedQuests.AddRange(WholesomeAQSettings.CurrentSetting.ListCompletedQuests);
            _completeQuests = completedQuests.Distinct().ToHashSet();
            bool shouldSave = false;
            foreach (int questId in _completeQuests)
            {
                if (!WholesomeAQSettings.CurrentSetting.ListCompletedQuests.Contains(questId))
                {
                    WholesomeAQSettings.CurrentSetting.ListCompletedQuests.Add(questId);
                    Logger.Log($"Saved quest {questId} as completed");
                    shouldSave = true;
                }
            }
            if (shouldSave)
                WholesomeAQSettings.CurrentSetting.Save();
        }

        public static bool IsQuestCompleted(int questId) => WholesomeAQSettings.CurrentSetting.ListCompletedQuests.Contains(questId);
        public static int GetServerNbCompletedQuests() => Quest.FinishedQuestSet.Count;

        public static bool ShouldQuestBeFinished(this ModelQuestTemplate quest) => quest.Status == QuestStatus.InProgress
                                                                           || quest.Status == QuestStatus.ToTurnIn;

        public static Vector3 Position(this ModelCreature npc) => npc.GetSpawnPosition;

        public static bool WoWDBFileIsPresent() => File.Exists(Others.GetCurrentDirectory + @"\Data\WoWDb335");

        public static bool JSONFileIsPresent() => File.Exists(Others.GetCurrentDirectory + @"\Data\WAQquests.json");

        public static bool CompiledJSONFileIsPresent() =>
            File.Exists(@"F:\WoW\Dev\Wholesome-Auto-Quester\Wholesome_Auto_Quester\Compiled\WAQquests.zip");

        public static bool ZippedJSONIsPresent() => File.Exists(Others.GetCurrentDirectory + @"\Data\WAQquests.zip");

        public static List<ModelQuestTemplate> GetAllQuestsFromJSON()
        {
            try
            {
                if (!JSONFileIsPresent())
                {
                    Logger.LogError("The JSON file is not present.");
                    return null;
                }

                using (StreamReader file = File.OpenText(Others.GetCurrentDirectory + @"\Data\WAQquests.json"))
                {
                    var serializer = new JsonSerializer();
                    return (List<ModelQuestTemplate>)serializer.Deserialize(file, typeof(List<ModelQuestTemplate>));
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
                return null;
            }
        }

        public static void ZipJSONFile()
        {
            try
            {
                if (!JSONFileIsPresent())
                {
                    Logger.LogError("The JSON file is not present in Data");
                    return;
                }

                if (ZippedJSONIsPresent())
                    File.Delete(Others.GetCurrentDirectory + @"\Data\WAQquests.zip");

                using (ZipArchive zip = ZipFile.Open(Others.GetCurrentDirectory + @"\Data\WAQquests.zip",
                    ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = zip.CreateEntry("WAQquests.json");
                    entry.LastWriteTime = DateTimeOffset.Now;

                    using (FileStream stream = File.OpenRead(Others.GetCurrentDirectory + @"\Data\WAQquests.json"))
                    using (Stream entryStream = entry.Open())
                    {
                        stream.CopyTo(entryStream);
                    }
                }

                /*
                // Copy to Compiled folder
                var compiledzip = @"F:\WoW\Dev\Wholesome-Auto-Quester\Wholesome_Auto_Quester\Compiled\WAQquests.zip";
                if (File.Exists(compiledzip))
                    File.Delete(compiledzip);
                File.Copy(Others.GetCurrentDirectory + @"\Data\WAQquests.zip", compiledzip);
                */
            }
            catch (Exception e)
            {
                Logger.LogError("ZipJSONFile > " + e.Message);
            }
        }

        public static void WriteJSONFromDBResult(List<ModelQuestTemplate> resultFromDB)
        {
            try
            {
                if (File.Exists(Others.GetCurrentDirectory + @"\Data\WAQquests.json"))
                    File.Delete(Others.GetCurrentDirectory + @"\Data\WAQquests.json");

                /*
                Logger.Log("Serialize");
                string jsonString = JsonConvert.SerializeObject(resultFromDB, Formatting.Indented);
                Logger.Log("Write");
                File.WriteAllText(Others.GetCurrentDirectory + @"\Data\WAQquests.json", jsonString);
                */
                using (StreamWriter file = File.CreateText(Others.GetCurrentDirectory + @"\Data\WAQquests.json"))
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(file, resultFromDB);
                }
            }
            catch (Exception e)
            {
                Logger.LogError("WriteJSONFromDBResult > " + e.Message);
            }
        }

        public static List<string> GetAvailableQuestGossips()
        {
            var result = new List<string>();
            var numGossips = Lua.LuaDoString<int>(@"return GetNumGossipAvailableQuests()");
            var nameIndex = 1;
            for (var i = 1; i <= numGossips; i++)
            {
                result.Add(Lua.LuaDoString<string>(@"
                    local gossips = { GetGossipAvailableQuests() };
                    return gossips[" + nameIndex + "];"));
                nameIndex += 4;
            }

            return result;
        }

        public static List<string> GetActiveQuestGossips()
        {
            var result = new List<string>();
            var numGossips = Lua.LuaDoString<int>(@"return GetNumGossipActiveQuests()");
            var nameIndex = 1;
            for (var i = 1; i <= numGossips; i++)
            {
                result.Add(Lua.LuaDoString<string>(@"
                    local gossips = { GetGossipActiveQuests() };
                    return gossips[" + nameIndex + "];"));
                nameIndex += 4;
            }

            return result;
        }

        public static List<string> GetAllGossips()
        {
            var result = new List<string>();
            var numGossips = Lua.LuaDoString<int>(@"return GetNumGossipOptions()");
            var nameIndex = 1;
            for (var i = 1; i <= numGossips; i++)
            {
                result.Add(Lua.LuaDoString<string>(@"
                    local gossips = { GetGossipOptions() };
                    return gossips[" + nameIndex + "];"));
                nameIndex += 3;
            }

            return result;
        }

        public static bool ShouldStateBeInterrupted(WAQTask task, WoWObject gameObject, WoWObjectType expectedType)
        {
            if (gameObject != null)
            {
                if (gameObject.Type != expectedType)
                {
                    Logger.LogError($"Expected {expectedType} for PickUp Quest but got {gameObject.Type} instead.");
                    return true;
                }
                if (wManagerSetting.IsBlackListedZone(gameObject.Position)
                    || wManagerSetting.IsBlackListed(gameObject.Guid))
                {
                    MoveHelper.StopAllMove();
                    Main.RequestImmediateTaskReset = true;
                    return true;
                }
            }
            if (wManagerSetting.IsBlackListedZone(task.Location))
            {
                MoveHelper.StopAllMove();
                Main.RequestImmediateTaskReset = true;
                return true;
            }
            return false;
        }

        public static void UpdateObjectiveCompletionDict(int[] questIds)
            => _objectiveCompletionDict = GetObjectiveCompletionDict(questIds);

        private static Dictionary<int, bool[]> GetObjectiveCompletionDict(int[] questIds)
        {
            var resultDict = new Dictionary<int, bool[]>();
            if (questIds.Length <= 0) return resultDict;
            string[] questIdStrings = questIds.Select(id => id.ToString()).ToArray();
            var inputTable = new StringBuilder("{",
                2 + questIdStrings.Aggregate(0, (last, str) => last + str.Length) + questIdStrings.Length - 1);
            for (var i = 0; i < questIdStrings.Length; i++)
            {
                inputTable.Append(questIdStrings[i]);
                if (i < questIdStrings.Length - 1) inputTable.Append(",");
            }

            inputTable.Append("}");

            bool[] outputTable = Lua.LuaDoString<bool[]>($@"
            local inputTable = {inputTable};
            local outputTable = {{}};
            
            for _, entry in pairs(inputTable) do
                local qId = 0;
                local i = 1
                while GetQuestLogTitle(i) do
            		local questTitle, level, questTag, suggestedGroup, isHeader, isCollapsed, isComplete, isDaily, questID = GetQuestLogTitle(i)
            		if ( not isHeader ) and questID == entry then
            			qId = i;
            		end
            		i = i + 1
                end
            	
            	for j=1, 6 do
            		if not qId then
            			table.insert(outputTable, false);
            		else
            			local description, objectiveType, isCompleted = GetQuestLogLeaderBoard(j,qId);
            			if not (description == nil) then  
            				table.insert(outputTable, isCompleted == 1);
            			else
            				table.insert(outputTable, false);
            			end
            		end
            	end
            end
            return unpack(outputTable)");

            if (outputTable.Length != questIds.Length * 6)
            {
                Logging.Write(
                    $"Expected {questIds.Length * 6} entries in GetObjectiveCompletionArray but got {outputTable.Length} instead.");
                return resultDict;
            }

            for (var i = 0; i < questIds.Length; i++)
            {
                var completionArray = new bool[6];
                for (var j = 0; j < completionArray.Length; j++)
                    completionArray[j] = outputTable[i * completionArray.Length + j];

                resultDict.Add(questIds[i], completionArray);
            }

            return resultDict;
        }

        public static bool IsObjectiveCompleted(int objectiveId, int questId)
        {
            if (objectiveId == -1)
                return false;

            if (objectiveId < 1 || objectiveId > 6)
            {
                Logging.WriteError($"Tried to call GetObjectiveCompletion with objectiveId: {objectiveId}");
                return false;
            }

            if (_objectiveCompletionDict.TryGetValue(questId, out bool[] completionArray))
                return completionArray[objectiveId - 1];

            Logging.WriteDebug($"Did not have quest {questId} in completion dictionary.");
            return false;
        }

        public static bool DangerousEnemiesAtLocation(Vector3 location)
        {
            uint myLevel = ObjectManager.Me.Level;
            var unitCounter = 0;
            foreach (WoWUnit unit in ObjectManager.GetWoWUnitHostile())
            {
                float distance = unit.PositionWithoutType.DistanceTo(location);
                if (distance > 40 || distance > unit.AggroDistance + 3) continue;
                uint unitLevel = unit.Level;
                if (unitLevel > myLevel + 2 || unitLevel > myLevel && unit.IsElite) return true;
                if (unitLevel > myLevel - 2) unitCounter++;
                if (unitCounter > 3) break;
            }

            return unitCounter > 3;
        }

        // public static string GetTaskId(WAQTask task) => task.TaskId;

        // public static string GetTaskIdLegacy(WAQTask task) {
        //     string taskType = ((int) task.TaskType).ToString();
        //     string questEntry = task.Quest.Id.ToString();
        //     string objIndex = task.ObjectiveIndex.ToString();
        //     string uniqueId = $"{task.Npc?.Guid}{task.GatherObject?.Guid}";
        //
        //     return $"{taskType}{questEntry}{objIndex}{uniqueId}";
        // }

        // public static string GetTaskId(TaskType taskType, int questEntry, int objIndex, int uniqueId = 0) {
        //     string uid = uniqueId == 0 ? "" : uniqueId.ToString();
        //     return $"{(int) taskType}{questEntry}{objIndex}{uid}";
        // }

        public static string GetWoWVersion() => Lua.LuaDoString<string>("v, b, d, t = GetBuildInfo(); return v");

        public static Factions GetFaction() =>
            (PlayerFactions)ObjectManager.Me.Faction switch
            {
                PlayerFactions.Human => Factions.Human,
                PlayerFactions.Orc => Factions.Orc,
                PlayerFactions.Dwarf => Factions.Dwarf,
                PlayerFactions.NightElf => Factions.NightElf,
                PlayerFactions.Undead => Factions.Undead,
                PlayerFactions.Tauren => Factions.Tauren,
                PlayerFactions.Gnome => Factions.Gnome,
                PlayerFactions.Troll => Factions.Troll,
                PlayerFactions.Goblin => Factions.Goblin,
                PlayerFactions.BloodElf => Factions.BloodElf,
                PlayerFactions.Draenei => Factions.Draenei,
                PlayerFactions.Worgen => Factions.Worgen,
                _ => Factions.Unknown
            };

        public static Classes GetClass() =>
            ObjectManager.Me.WowClass switch
            {
                WoWClass.Warrior => Classes.Warrior,
                WoWClass.Paladin => Classes.Paladin,
                WoWClass.Hunter => Classes.Hunter,
                WoWClass.Rogue => Classes.Rogue,
                WoWClass.Priest => Classes.Priest,
                WoWClass.DeathKnight => Classes.DeathKnight,
                WoWClass.Shaman => Classes.Shaman,
                WoWClass.Mage => Classes.Mage,
                WoWClass.Warlock => Classes.Warlock,
                WoWClass.Druid => Classes.Druid,
                _ => Classes.Unknown
            };

        // Calculate real walking distance, returns 0 is path is broken
        public static WAQPath GetWAQPath(Vector3 from, Vector3 to)
        {
            float distance = 0f;
            bool isReachable;
            List<Vector3> path = FindPath(from, to, skipIfPartiel: false, resultSuccess: out isReachable);
            if (isReachable)
                for (var i = 0; i < path.Count - 1; ++i) distance += path[i].DistanceTo(path[i + 1]);
            return new WAQPath(path, distance);
        }

        public static bool IsHorde()
        {
            return ObjectManager.Me.Faction == (uint)PlayerFactions.Orc || ObjectManager.Me.Faction == (uint)PlayerFactions.Tauren
                || ObjectManager.Me.Faction == (uint)PlayerFactions.Undead || ObjectManager.Me.Faction == (uint)PlayerFactions.BloodElf
                || ObjectManager.Me.Faction == (uint)PlayerFactions.Troll;
        }

        public static void InitializeWAQSettings()
        {
            BlacklistHelper.AddQuestToBlackList(1202); // Theramore docks
            //WholesomeAQSettings.AddQuestToBlackList(1526); // Call of Fire. Requires active item from PREVIOUS quest
            BlacklistHelper.AddQuestToBlackList(863); // Ignition, bugged platform
            BlacklistHelper.AddQuestToBlackList(6383); // Ashenvale hunt, bugged 
            BlacklistHelper.AddQuestToBlackList(891); // The Guns of NorthWatch, too many mobs
            BlacklistHelper.AddQuestToBlackList(9612); // A hearty thanks, requires heal on mob
            if (IsHorde()) BlacklistHelper.AddQuestToBlackList(4740); // Bugged, should only be alliance

            if (!wManagerSetting.CurrentSetting.DoNotSellList.Contains("WAQStart") || !wManagerSetting.CurrentSetting.DoNotSellList.Contains("WAQEnd"))
            {
                wManagerSetting.CurrentSetting.DoNotSellList.Remove("WAQStart");
                wManagerSetting.CurrentSetting.DoNotSellList.Remove("WAQEnd");
                wManagerSetting.CurrentSetting.DoNotSellList.Add("WAQStart");
                wManagerSetting.CurrentSetting.DoNotSellList.Add("WAQEnd");
                wManagerSetting.CurrentSetting.Save();
            }
        }

        public static Dictionary<int, int> QuestModifiedLevel = new Dictionary<int, int>()
        {
            { 354, 3 }, // Roaming mobs, hard to find in a hostile zone
            { 843, 3 }, // Bael'Dun excavation, too many mobs
            { 6548, 3 }, // Avenge my village, too many mobs
            { 6629, 3 }, // Avenge my village follow up, too many mobs
            { 216, 2 }, // Between a rock and a Thistlefur, too many mobs
        };

        public static void PickupQuestFromBagItem(string itemName)
        {
            ItemsManager.UseItemByNameOrId(itemName);
            Thread.Sleep(500);
            Lua.LuaDoString("if GetClickFrame('QuestFrame'):IsVisible() then AcceptQuest(); end");
            Thread.Sleep(500);
            Lua.LuaDoString(@"
                        local closeButton = GetClickFrame('QuestFrameCloseButton');
                        if closeButton:IsVisible() then
            	            closeButton:Click();
                        end");
        }

        public static WAQPath AdjustPathToTask(WAQTask task)
        {
            Random rand = new Random();
            Vector3 location = task.Location;
            for (int i = 0; i < 10; i++)
            {
                Vector3 newdest = new Vector3(
                    location.X + rand.NextDouble() * 4 - 2,
                    location.Y + rand.NextDouble() * 4 - 2,
                    location.Z + rand.NextDouble() * 4 - 2);
                WAQPath newPath = GetWAQPath(ObjectManager.Me.Position, newdest);
                Logger.Log($"Trying to adjust path for {task.TaskName} {i}");
                if (newPath.IsReachable)
                {
                    Logger.Log($"FOUND");
                    task.Location = newdest;
                    return newPath;
                }
            }
            Logger.Log($"FAILED");
            return new WAQPath(new List<Vector3>(), 0);
        }

        public static WAQPath AdjustPathToObject(WoWObject wObject)
        {
            Random rand = new Random();
            Vector3 location = wObject.Position;
            for (int i = 0; i < 10; i++)
            {
                Vector3 newdest = new Vector3(
                    location.X + rand.NextDouble() * 4 - 2,
                    location.Y + rand.NextDouble() * 4 - 2,
                    location.Z + rand.NextDouble() * 4 - 2);
                WAQPath newPath = GetWAQPath(ObjectManager.Me.Position, newdest);
                Logger.Log($"Trying to adjust path for {wObject.Name} {i}");
                if (newPath.IsReachable)
                {
                    Logger.Log($"FOUND");
                    return newPath;
                }
            }
            Logger.Log($"FAILED");
            return new WAQPath(new List<Vector3>(), 0);
        }

        public static bool PlayerInBloodElfStartingZone()
        {
            string zone = Lua.LuaDoString<string>("return GetRealZoneText();");
            return zone == "Eversong Woods" || zone == "Ghostlands" || zone == "Silvermoon City";
        }

        public static bool PlayerInDraneiStartingZone()
        {
            string zone = Lua.LuaDoString<string>("return GetRealZoneText();");
            return zone == "Azuremyst Isle" || zone == "Bloodmyst Isle" || zone == "The Exodar";
        }
    }
}