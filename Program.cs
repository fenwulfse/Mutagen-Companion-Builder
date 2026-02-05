using System;
using System.Linq;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Strings;
using Noggog;

namespace CompanionGemini
{
    // ==============================================================================
    // GUARDRAIL SYSTEM (v2.0) - STOP & NOTIFY ARCHITECTURE
    // ==============================================================================
    public static class Guardrail
    {
        private static void Throw(string message)
        {
            Console.WriteLine("\n!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine("!!! GUARDRAIL VIOLATION DETECTED !!!");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine($"MESSAGE: {message}");
            Console.WriteLine("ACTION: Execution has been HALTED. We must discuss this conflict before proceeding.");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!\n");
            throw new Exception($"GUARDRAIL HALT: {message}");
        }

        public static void AssertGreeting(IDialogTopicGetter topic)
        {
            if (topic.Priority != 50) Throw($"Greeting Topic '{topic.EditorID}' must have Priority 50. Found: {topic.Priority}. (Piper Canon Lock)");
            if (topic.Subtype != DialogTopic.SubtypeEnum.Greeting) Throw($"Greeting Topic '{topic.EditorID}' must have Subtype 'Greeting'. Found: {topic.Subtype}.");
            if (topic.Category != DialogTopic.CategoryEnum.Misc) Throw($"Greeting Topic '{topic.EditorID}' must be in Category 'Misc' to stay in the Miscellaneous Tab.");
            if (topic.Branch != null && !topic.Branch.IsNull) Throw($"Greeting Topic '{topic.EditorID}' must NOT have a Branch. Branches move greetings to the Dialogue Tab.");
        }

        public static void AssertQuest(IQuestGetter quest)
        {
            if (quest.Data == null) Throw($"Quest '{quest.EditorID}' has no DATA record.");
            if (quest.Data.Priority != 70) Throw($"Quest '{quest.EditorID}' must have Priority 70. Found: {quest.Data.Priority}.");
            if (quest.Name?.ToString() != "Gemini") Throw($"Quest Name must be 'Gemini'. Found: {quest.Name}.");

            if (!quest.Data.Flags.HasFlag(Quest.Flag.StartGameEnabled)) Throw("StartGameEnabled must be Checked.");
            if (!quest.Data.Flags.HasFlag(Quest.Flag.RunOnce)) Throw("RunOnce must be Checked.");
            
            bool hasLock = false;
            foreach (var cond in quest.DialogConditions)
            {
                if (cond is IConditionFloatGetter cf && cf.Data is IFunctionConditionDataGetter fcd)
                {
                    if (fcd.Function == Condition.Function.GetIsAliasRef && fcd.ParameterOneNumber == 0)
                    {
                        hasLock = true;
                        break;
                    }
                }
            }
            if (!hasLock) Throw($"Quest '{quest.EditorID}' must have 'GetIsAliasRef(0) == 1' in DialogConditions to lock dialogue to Gemini.");
        }

        public static void AssertStages(IQuestGetter quest)
        {
            if (quest.Stages.Count < 53) Throw($"Quest '{quest.EditorID}' is missing stages. Expected 53 (Piper Canon), found {quest.Stages.Count}.");
            foreach (var stage in quest.Stages)
            {
                if (stage.LogEntries.Count != 1) Throw($"Stage {stage.Index} must have exactly ONE Log Entry (Index 0). Found: {stage.LogEntries.Count}.");
                if (stage.LogEntries[0].Note == null || string.IsNullOrEmpty(stage.LogEntries[0].Note)) Throw($"Stage {stage.Index} Designer Note (NAM0) is missing.");
            }
        }

        public static void Validate(Fallout4Mod mod)
        {
            Console.WriteLine("--- RUNNING GUARDRAIL VALIDATION ---");
            foreach (var quest in mod.Quests)
            {
                AssertQuest(quest);
                AssertStages(quest);
                foreach (var topic in quest.DialogTopics)
                {
                    if (topic.Subtype == DialogTopic.SubtypeEnum.Greeting || (topic.EditorID?.Contains("Greeting") ?? false))
                        AssertGreeting(topic);
                }
            }
            Console.WriteLine("--- GUARDRAIL PASSED ---");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== CompanionGemini Q - Full Recovery Build (Identity & Engine Fix) ===");
            
            using var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);
            var mod = new Fallout4Mod(ModKey.FromFileName("CompanionGemini.esp"), Fallout4Release.Fallout4);

            T? GetRecord<T>(string editorId) where T : class, IMajorRecordGetter {
                return env.LoadOrder.PriorityOrder.WinningOverrides<T>().FirstOrDefault(r => r.EditorID == editorId);
            }

            var fo4 = ModKey.FromNameAndExtension("Fallout4.esm");
            for (int i = 0; i < 200; i++) mod.GetNextFormKey();

            // 1. NPC (Identity restoration)
            var npc = mod.Npcs.AddNew("CompanionGemini");
            npc.Name = new TranslatedString(Language.English, "Gemini");
            npc.ShortName = new TranslatedString(Language.English, "Gemini");
            npc.Race.SetTo(GetRecord<IRaceGetter>("HumanRace")?.FormKey ?? throw new Exception("Race missing"));
            npc.Voice.SetTo(new FormKey(fo4, 0x01928A)); // NPCFPiper
            npc.Flags = Npc.Flag.Unique | Npc.Flag.Essential | Npc.Flag.AutoCalcStats | Npc.Flag.Female;
            
            var currentCompanionFaction = new FormKey(fo4, 0x023C01);
            var hasBeenCompanionFaction = new FormKey(fo4, 0x075D56);
            var potentialCompanionFaction = new FormKey(fo4, 0x0A1B85);

            npc.Factions.Add(new RankPlacement { Faction = currentCompanionFaction.ToLink<IFactionGetter>(), Rank = -1 });
            npc.Factions.Add(new RankPlacement { Faction = hasBeenCompanionFaction.ToLink<IFactionGetter>(), Rank = -1 });
            npc.Factions.Add(new RankPlacement { Faction = potentialCompanionFaction.ToLink<IFactionGetter>(), Rank = 0 });

            // ENGINE FIX: Bad Speed Mult (Initialize collection if needed)
            var speedMultAV = new FormKey(fo4, 0x000002DE); // SpeedMult
            npc.Properties.Add(new ObjectProperty { ActorValue = speedMultAV.ToLink<IActorValueInformationGetter>(), Value = 100.0f });

            // 2. MAIN QUEST (Structure restoration)
            var quest = mod.Quests.AddNew("COMGemini");
            quest.Name = new TranslatedString(Language.English, "Gemini");
            quest.Data = new QuestData { Flags = Quest.Flag.StartGameEnabled | Quest.Flag.RunOnce | Quest.Flag.AddIdleTopicToHello | Quest.Flag.AllowRepeatedStages, Priority = 70 };
            
            quest.DialogConditions.Add(new ConditionFloat { 
                CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1, 
                Data = new FunctionConditionData { Function = Condition.Function.GetIsAliasRef, ParameterOneNumber = 0 } 
            });

            // 3. ALIASES
            quest.Aliases.Add(new QuestReferenceAlias { 
                ID = 0, Name = "Gemini", 
                UniqueActor = new FormLinkNullable<INpcGetter>(npc.FormKey), 
                Flags = QuestReferenceAlias.Flag.Essential | QuestReferenceAlias.Flag.QuestObject | QuestReferenceAlias.Flag.StoresText 
            });
            quest.Aliases.Add(new QuestReferenceAlias { ID = 1, Name = "Companion", Flags = QuestReferenceAlias.Flag.Optional | QuestReferenceAlias.Flag.AllowDisabled | QuestReferenceAlias.Flag.AllowReserved });
            quest.Aliases.Add(new QuestReferenceAlias { ID = 2, Name = "dogmeat", Flags = QuestReferenceAlias.Flag.Optional | QuestReferenceAlias.Flag.AllowDisabled | QuestReferenceAlias.Flag.AllowReserved });

            // 4. DIALOGUE HELPER
            var neutralEmotion = new FormKey(fo4, 0x0D755D).ToLink<IKeywordGetter>();
            DialogTopic CreateTopic(string edid, string prompt, string text, DialogTopic.CategoryEnum cat = DialogTopic.CategoryEnum.Misc) {
                // Topic lives in mod.DialogTopics
                var t = mod.DialogTopics.AddNew(edid);
                t.Quest.SetTo(quest.FormKey);
                t.Category = cat;
                t.Priority = 50;
                if (cat == DialogTopic.CategoryEnum.Scene) { t.Subtype = DialogTopic.SubtypeEnum.Custom17; t.SubtypeName = "SCEN"; }
                var r = new DialogResponses(mod.GetNextFormKey(), Fallout4Release.Fallout4);
                r.Responses.Add(new DialogResponse { Text = new TranslatedString(Language.English, text), ResponseNumber = 1, Unknown = 1, Emotion = neutralEmotion });
                if (!string.IsNullOrEmpty(prompt)) r.Prompt = new TranslatedString(Language.English, prompt);
                t.Responses.Add(r);
                quest.DialogTopics.Add(t);
                return t;
            }

            // 5. SCENES (Advanced Logic Replicas)
            void QuickScene(string edid, int phases, int endStage) {
                var s = new Scene(mod.GetNextFormKey(), Fallout4Release.Fallout4) { EditorID = edid, Flags = (Scene.Flag)36 };
                s.Quest.SetTo(quest.FormKey);
                s.Actors.Add(new SceneActor { ID = 0, BehaviorFlags = (SceneActor.BehaviorFlag)10, Flags = (SceneActor.Flag)4 });
                for (int i = 0; i < phases; i++) {
                    var p = new ScenePhase { Name = "" };
                    if (i == phases - 1 && endStage > 0) p.PhaseSetParentQuestStage = new SceneSetParentQuestStage { OnBegin = -1, OnEnd = (short)endStage };
                    s.Phases.Add(p);
                }
                quest.Scenes.Add(s);
            }

            QuickScene("COMGeminiPickupScene", 6, 80);
            QuickScene("COMGeminiDismissScene", 5, 90);
            QuickScene("COMGemini_01_NeutralToFriendship", 8, 406);
            QuickScene("COMGemini_02_FriendshipToAdmiration", 6, 420);
            QuickScene("COMGemini_02a_AdmirationToConfidant", 8, 497);
            QuickScene("COMGemini_03_AdmirationToInfatuation", 14, 525);
            QuickScene("COMGemini_04_NeutralToDisdain", 3, 220);
            QuickScene("COMGemini_05_DisdainToHatred", 10, 120);
            QuickScene("COMGemini_06_RepeatInfatuationToAdmiration", 4, 450);
            QuickScene("COMGemini_07_RepeatAdmirationToNeutral", 4, 330);
            QuickScene("COMGemini_10_RepeatAdmirationToInfatuation", 6, 550);
            QuickScene("COMGeminiMurderScene", 5, 620);

            // Use the helper
            CreateTopic("COMGeminiRecoveryPlaceholder", "", "Syncing...");

            // 6. STAGES (Full 53 Replica)
            var notes = new Dictionary<int, string> {
                { 80, "Pickup" }, { 90, "Dismiss" }, { 100, "Hatred" }, { 110, "H-FG" }, { 120, "H-Done" }, { 130, "H-Bail" }, { 140, "H-Rep" }, { 150, "H-Rep-FG" }, { 160, "H-Rep-Done" },
                { 200, "Disdain" }, { 210, "D-FG" }, { 220, "D-Done" }, { 230, "D-Rep" }, { 240, "D-Rep-FG" }, { 250, "D-Rep-Done" }, { 300, "Neutral" },
                { 310, "N-Rep" }, { 320, "N-Rep-FG" }, { 330, "N-Rep-Done" }, { 340, "N-Rep2" }, { 350, "N-Rep2-FG" }, { 360, "N-Rep2-Done" },
                { 400, "Admiration" }, { 405, "Friendship" }, { 406, "F-FG" }, { 407, "F-Done" }, { 410, "A-FG" }, { 420, "A-Done" },
                { 430, "A-Rep" }, { 440, "A-Rep-FG" }, { 450, "A-Rep-Done" }, { 460, "A-Rep2" }, { 470, "A-Rep2-FG" }, { 480, "A-Rep2-Done" },
                { 495, "Confidant" }, { 496, "C-FG" }, { 497, "C-Done" }, { 500, "Infatuation" }, { 510, "I-FG" }, { 515, "I-Done-Declined" }, { 520, "I-Done-Failed" }, { 522, "I-Done-Perm" },
                { 525, "I-Done-Success" }, { 530, "I-Rep" }, { 540, "I-Rep-FG" }, { 550, "I-Rep-Done" }, { 560, "I-Rep-No" }, { 600, "Murder" }, { 610, "M-FG" }, { 620, "M-Done" }, { 630, "M-Quit" },
                { 1000, "Endgame" }, { 1010, "Endgame-Done" }
            };
            foreach(var kv in notes) {
                var s = new QuestStage { Index = (ushort)kv.Key, Flags = 0 };
                s.LogEntries.Add(new QuestLogEntry { Note = kv.Value, Flags = 0, Conditions = new ExtendedList<Condition>() });
                quest.Stages.Add(s);
            }

            // 7. GREETINGS (Miscellaneous Tab Fix)
            var greetings = mod.DialogTopics.AddNew("COMGeminiGreetings");
            greetings.Quest.SetTo(quest.FormKey);
            greetings.Category = DialogTopic.CategoryEnum.Misc;
            greetings.Subtype = DialogTopic.SubtypeEnum.Greeting;
            greetings.SubtypeName = "GREE";
            greetings.Priority = 50;
            quest.DialogTopics.Add(greetings);

            // 8. VMAD
            quest.VirtualMachineAdapter = new QuestAdapter { Version = 6, ObjectFormat = 2, Script = new ScriptEntry { Name = "Fragments:Quests:QF_COMGemini_" + quest.FormKey.ID.ToString("X8") } };

            // 9. VALIDATE & SAVE
            try {
                Guardrail.Validate(mod);
                mod.WriteToBinary("CompanionGemini.esp", new BinaryWriteParameters { MastersListOrdering = new MastersListOrderingByLoadOrder(env.LoadOrder) });
                Console.WriteLine("SUCCESS: Recovery Build Complete.");
            } catch (Exception ex) {
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
