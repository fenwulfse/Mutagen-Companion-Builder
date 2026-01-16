using System;
using System.IO;
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

namespace Project_Gemini_Restored
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== CompanionGemini (Restored Jan 08 Build) ===");

            using var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);
            var mod = new Fallout4Mod(ModKey.FromFileName("CompanionGemini.esp"), Fallout4Release.Fallout4);

            // HELPER
            T? GetRecord<T>(string editorId) where T : class, IMajorRecordGetter {
                return env.LoadOrder.PriorityOrder.WinningOverrides<T>().FirstOrDefault(r => r.EditorID == editorId);
            }

            // 1. VANILLA RECORDS
            Console.WriteLine("Loading vanilla records...");
            var humanRace = GetRecord<IRaceGetter>("HumanRace");
            var femaleVoice = GetRecord<IVoiceTypeGetter>("FemaleEvenToned");
            var hair = GetRecord<IHeadPartGetter>("HairFemale03") ?? GetRecord<IHeadPartGetter>("HairFemale01");
            var eyes = env.LoadOrder.PriorityOrder.WinningOverrides<IHeadPartGetter>()
                .FirstOrDefault(h => h.EditorID != null && h.EditorID.Contains("Eyes") && h.EditorID.Contains("Human") && !h.EditorID.Contains("Blind"));
            var currentCompanionFaction = GetRecord<IFactionGetter>("CurrentCompanionFaction");
            var hasBeenCompanionFaction = GetRecord<IFactionGetter>("HasBeenCompanionFaction");
            var potentialCompanionFaction = GetRecord<IFactionGetter>("PotentialCompanionFaction");
            var voicesCompanionsFaction = GetRecord<IFactionGetter>("Voices_CompanionsFaction");
            var followersQuest = GetRecord<IQuestGetter>("Followers");
            var tutorialQuest = GetRecord<IQuestGetter>("CompanionTutorial");
            var avTemporaryAngerLevel = GetRecord<IActorValueInformationGetter>("CA_TemporaryAngerLevel");
            var avExperience = GetRecord<IActorValueInformationGetter>("Experience");
            var avHasItemForPlayer = GetRecord<IActorValueInformationGetter>("CA_HasItemForPlayer");
            var globalMQComplete = GetRecord<IGlobalGetter>("MQComplete");
            var globalCA_00 = GetRecord<IGlobalGetter>("CA_00");
            var globalCA_Infatuation = GetRecord<IGlobalGetter>("CA_T6_Infatuation");
            var kwCA_Event_Murder = GetRecord<IKeywordGetter>("CA_Event_Murder");
            var kwCA_CustomEvent_Dislikes = GetRecord<IKeywordGetter>("CA_CustomEvent_PiperDislikes");
            var kwCA_CustomEvent_Hates = GetRecord<IKeywordGetter>("CA_CustomEvent_PiperHates");
            var kwCA_CustomEvent_Likes = GetRecord<IKeywordGetter>("CA_CustomEvent_PiperLikes");
            var kwCA_CustomEvent_Loves = GetRecord<IKeywordGetter>("CA_CustomEvent_PiperLoves");
            var piperInfatuationPerk = GetRecord<IPerkGetter>("CompanionPiperPerk");
            var piperInfatuationMsg = GetRecord<IMessageGetter>("COMPiperMaxApprovalMessage");
            var kwPlayerCanStimpak = GetRecord<IKeywordGetter>("PlayerCanStimpak");
            var zeroSpecialClass = GetRecord<IClassGetter>("ZeroSPECIALclass");
            var combatStyle = GetRecord<ICombatStyleGetter>("csCompPiper");
            var speedMult = GetRecord<IActorValueInformationGetter>("SpeedMult");

            if (humanRace == null || femaleVoice == null || followersQuest == null) {
                Console.WriteLine("ERROR: Required vanilla records not found!");
                return;
            }

            // 2. CREATE NPC
            Console.WriteLine("Creating NPC...");
            var npc = new Npc(mod.GetNextFormKey(), Fallout4Release.Fallout4) {
                EditorID = "CompanionGemini",
                Name = new TranslatedString(Language.English, "Gemini"),
                Race = humanRace.FormKey.ToLink<IRaceGetter>(),
                Voice = new FormLinkNullable<IVoiceTypeGetter>(femaleVoice.FormKey),
                Flags = Npc.Flag.Unique | Npc.Flag.Essential | Npc.Flag.AutoCalcStats | Npc.Flag.Female,
                Factions = new ExtendedList<RankPlacement> {
                    new RankPlacement { Faction = currentCompanionFaction.FormKey.ToLink<IFactionGetter>(), Rank = -1 },
                    new RankPlacement { Faction = hasBeenCompanionFaction.FormKey.ToLink<IFactionGetter>(), Rank = -1 },
                    new RankPlacement { Faction = potentialCompanionFaction!.FormKey.ToLink<IFactionGetter>(), Rank = 0 },
                    new RankPlacement { Faction = voicesCompanionsFaction!.FormKey.ToLink<IFactionGetter>(), Rank = 0 }
                },
                Properties = new ExtendedList<ObjectProperty>()
            };
            if (hair != null) npc.HeadParts.Add(hair.FormKey.ToLink<IHeadPartGetter>());
            if (eyes != null) npc.HeadParts.Add(eyes.FormKey.ToLink<IHeadPartGetter>());
            if (speedMult != null) npc.Properties.Add(new ObjectProperty { ActorValue = speedMult.FormKey.ToLink<IActorValueInformationGetter>(), Value = 100.0f });
            if (zeroSpecialClass != null) npc.Class.SetTo(zeroSpecialClass);
            if (combatStyle != null) npc.CombatStyle.SetTo(combatStyle);
            mod.Npcs.Add(npc);

            // 3. CELL & PLACEMENT
            Console.WriteLine("Creating cell...");
            var homeLocation = new Location(mod.GetNextFormKey(), Fallout4Release.Fallout4) {
                EditorID = "GeminiHomeLocation",
                Name = new TranslatedString(Language.English, "Gemini's Home")
            };
            mod.Locations.Add(homeLocation);

            var cell = new Cell(mod.GetNextFormKey(), Fallout4Release.Fallout4) {
                EditorID = "GeminiStartCell",
                Name = new TranslatedString(Language.English, "Gemini's Office"),
                Flags = Cell.Flag.IsInteriorCell,
                Lighting = new CellLighting()
            };
            var placedNpc = new PlacedNpc(mod.GetNextFormKey(), Fallout4Release.Fallout4) {
                EditorID = "GeminiPlacedRef",
                MajorFlags = PlacedNpc.MajorFlag.InitiallyDisabled
            };
            placedNpc.Base.SetTo(npc.FormKey);
            cell.Temporary.Add(placedNpc);
            
            var floor = new PlacedObject(mod.GetNextFormKey(), Fallout4Release.Fallout4);
            floor.Base.SetTo(new FormKey(ModKey.FromNameAndExtension("Fallout4.esm"), 0x00067A40));
            cell.Temporary.Add(floor);

            var cellBlock = new CellBlock { BlockNumber = 0, GroupType = GroupTypeEnum.InteriorCellBlock };
            var cellSubBlock = new CellSubBlock { BlockNumber = 0, GroupType = GroupTypeEnum.InteriorCellSubBlock };
            cellSubBlock.Cells.Add(cell);
            cellBlock.SubBlocks.Add(cellSubBlock);
            mod.Cells.Records.Add(cellBlock);

            // 4. QUEST
            Console.WriteLine("Creating quest...");
            var questFormKey = mod.GetNextFormKey();
            var pscShortName = "QF_COMGemini_" + questFormKey.ID.ToString("X8");

            var aliasGemini = new QuestReferenceAlias {
                ID = 0, Name = "Gemini",
                Flags = QuestReferenceAlias.Flag.QuestObject | QuestReferenceAlias.Flag.AllowDead | QuestReferenceAlias.Flag.AllowDisabled
            };
            aliasGemini.UniqueActor.SetTo(npc.FormKey);
            var aliasCompanion = new QuestReferenceAlias { ID = 1, Name = "Companion", Flags = QuestReferenceAlias.Flag.Optional | QuestReferenceAlias.Flag.ExternalAliasLinked };
            var aliasDogmeat = new QuestReferenceAlias { ID = 2, Name = "Dogmeat", Flags = QuestReferenceAlias.Flag.Optional | QuestReferenceAlias.Flag.ExternalAliasLinked };

            // 5. DIALOGUE
            DialogTopic CreateTopic(string edid, string text, DialogTopic.CategoryEnum category = DialogTopic.CategoryEnum.Scene) {
                var topic = new DialogTopic(mod.GetNextFormKey(), Fallout4Release.Fallout4) {
                    EditorID = edid,
                    Quest = questFormKey.ToLink<IQuestGetter>(),
                    Category = category,
                    Subtype = category == DialogTopic.CategoryEnum.Scene ? DialogTopic.SubtypeEnum.Custom17 : DialogTopic.SubtypeEnum.Greeting,
                    SubtypeName = category == DialogTopic.CategoryEnum.Scene ? "SCEN" : "GREE",
                    Priority = category == DialogTopic.CategoryEnum.Scene ? 50 : 75
                };
                var info = new DialogResponses(mod.GetNextFormKey(), Fallout4Release.Fallout4);
                info.Responses.Add(new DialogResponse { Text = text });
                topic.Responses.Add(info);
                return topic;
            }
            var gemNpcPos = CreateTopic("COMGemini_NpcPos", "Okay, let's go.");
            var gemNpcNeg = CreateTopic("COMGemini_NpcNeg", "Maybe another time.");
            var gemNpcNeu = CreateTopic("COMGemini_NpcNeu", "Sure.");
            var gemNpcQue = CreateTopic("COMGemini_NpcQue", "What's on your mind?");
            var gemPlayerPos = CreateTopic("COMGemini_PlayerPos", "Come with me.");
            var gemPlayerNeg = CreateTopic("COMGemini_PlayerNeg", "Never mind.");
            var gemPlayerNeu = CreateTopic("COMGemini_PlayerNeu", "Wait here.");
            var gemPlayerQue = CreateTopic("COMGemini_PlayerQue", "Can we talk?");
            var gemDismissNpcPos = CreateTopic("COMGemini_DismissNpcPos", "I'll head back.");
            var gemDismissPlayerPos = CreateTopic("COMGemini_DismissPlayerPos", "It's time for us to part ways.");

            // 6. SCENES
            Scene CreateScene(string edid, int endStage) {
                var scene = new Scene(mod.GetNextFormKey(), Fallout4Release.Fallout4) {
                    EditorID = edid,
                    Flags = (Scene.Flag)36,
                    SetParentQuestStage = new SceneSetParentQuestStage { OnBegin = -1, OnEnd = (short)endStage }
                };
                scene.Quest.SetTo(questFormKey);
                scene.Actors.Add(new SceneActor { ID = 0, BehaviorFlags = SceneActor.BehaviorFlag.DeathEnd | SceneActor.BehaviorFlag.CombatEnd | SceneActor.BehaviorFlag.DialoguePause });
                scene.Phases.Add(new ScenePhase { Name = "Loop01" });
                return scene;
            }
            var recruitScene = CreateScene("COMGeminiPickup", 80);
            var dismissScene = CreateScene("COMGeminiDismiss", 90);

            void AddDialogAction(Scene scene, DialogTopic playerPos, DialogTopic playerNeg, DialogTopic playerNeu, DialogTopic playerQue, DialogTopic npcPos, DialogTopic npcNeg, DialogTopic npcNeu, DialogTopic npcQue) {
                var action = new SceneAction {
                    Type = new SceneActionTypicalType { Type = SceneAction.TypeEnum.PlayerDialogue },
                    Index = 1, AliasID = 0, StartPhase = 0, EndPhase = 0,
                    Flags = SceneAction.Flag.FaceTarget | SceneAction.Flag.HeadtrackPlayer | SceneAction.Flag.CameraSpeakerTarget
                };
                action.PlayerPositiveResponse.SetTo(playerPos);
                action.PlayerNegativeResponse.SetTo(playerNeg);
                action.PlayerNeutralResponse.SetTo(playerNeu);
                action.PlayerQuestionResponse.SetTo(playerQue);
                action.NpcPositiveResponse.SetTo(npcPos);
                action.NpcNegativeResponse.SetTo(npcNeg);
                action.NpcNeutralResponse.SetTo(npcNeu);
                action.NpcQuestionResponse.SetTo(npcQue);
                scene.Actions.Add(action);
            }
            AddDialogAction(recruitScene, gemPlayerPos, gemPlayerNeg, gemPlayerNeu, gemPlayerQue, gemNpcPos, gemNpcNeg, gemNpcNeu, gemNpcQue);
            AddDialogAction(dismissScene, gemDismissPlayerPos, gemPlayerNeg, gemPlayerNeu, gemPlayerQue, gemDismissNpcPos, gemNpcNeg, gemNpcNeu, gemNpcQue);

            // 7. GREETING
            var greetingTopic = new DialogTopic(mod.GetNextFormKey(), Fallout4Release.Fallout4) {
                EditorID = "COMGeminiGreeting",
                Quest = questFormKey.ToLink<IQuestGetter>(),
                Category = DialogTopic.CategoryEnum.Misc,
                Subtype = DialogTopic.SubtypeEnum.Greeting,
                SubtypeName = "GREE",
                Priority = 75
            };
            ConditionFloat FactionCheck(IFactionGetter faction, float value) => new ConditionFloat {
                CompareOperator = CompareOperator.EqualTo, ComparisonValue = value,
                Data = new FunctionConditionData { Function = Condition.Function.GetInFaction, ParameterOneRecord = faction.FormKey.ToLink<IFactionGetter>() }
            };
            var respNew = new DialogResponses(mod.GetNextFormKey(), Fallout4Release.Fallout4);
            respNew.Responses.Add(new DialogResponse { Text = "Hey. Need a hand?" });
            respNew.Conditions.Add(FactionCheck(hasBeenCompanionFaction, 0f));
            respNew.Flags = new DialogResponseFlags { Flags = (DialogResponses.Flag)4 };
            respNew.StartScene.SetTo(recruitScene);
            greetingTopic.Responses.Add(respNew);

            var respBack = new DialogResponses(mod.GetNextFormKey(), Fallout4Release.Fallout4);
            respBack.Responses.Add(new DialogResponse { Text = "Ready to head back out?" });
            respBack.Conditions.Add(FactionCheck(hasBeenCompanionFaction, 1f));
            respBack.Conditions.Add(FactionCheck(currentCompanionFaction, 0f));
            respBack.StartScene.SetTo(recruitScene);
            greetingTopic.Responses.Add(respBack);

            var respDismiss = new DialogResponses(mod.GetNextFormKey(), Fallout4Release.Fallout4);
            respDismiss.Responses.Add(new DialogResponse { Text = "Something on your mind?" });
            respDismiss.Conditions.Add(FactionCheck(currentCompanionFaction, 1f));
            respDismiss.Conditions.Add(FactionCheck(hasBeenCompanionFaction, 1f));
            respDismiss.StartScene.SetTo(dismissScene);
            respDismiss.StartScenePhase = "Loop01";
            greetingTopic.Responses.Add(respDismiss);

            // 8. QUEST STAGES
            var stage80 = new QuestStage { Index = 80, Unknown = 116 };
            stage80.LogEntries.Add(new QuestLogEntry { Flags = 0, Entry = new TranslatedString(Language.English, "Gemini recruited.") });
            var stage90 = new QuestStage { Index = 90, Unknown = 116 };
            stage90.LogEntries.Add(new QuestLogEntry { Flags = 0, Entry = new TranslatedString(Language.English, "Gemini dismissed.") });

            var vmad = new QuestAdapter {
                Version = 6, ObjectFormat = 2,
                Script = new ScriptEntry {
                    Name = "Fragments:Quests:" + pscShortName,
                    Properties = new ExtendedList<ScriptProperty> {
                        new ScriptObjectProperty { Name = "Alias_Gemini", Object = questFormKey.ToLink<IFallout4MajorRecordGetter>(), Alias = 0 },
                        new ScriptObjectProperty { Name = "Alias_Companion", Object = questFormKey.ToLink<IFallout4MajorRecordGetter>(), Alias = 1 },
                        new ScriptObjectProperty { Name = "Alias_Dogmeat", Object = questFormKey.ToLink<IFallout4MajorRecordGetter>(), Alias = 2 },
                        new ScriptObjectProperty { Name = "Followers", Object = followersQuest.FormKey.ToLink<IFallout4MajorRecordGetter>(), Alias = -1 }
                    }
                }
            };
            vmad.Fragments.Add(new QuestScriptFragment { Stage = 80, StageIndex = 0, Unknown2 = 1, ScriptName = "Fragments:Quests:" + pscShortName, FragmentName = "Fragment_Stage_0080_Item_00" });
            vmad.Fragments.Add(new QuestScriptFragment { Stage = 90, StageIndex = 0, Unknown2 = 1, ScriptName = "Fragments:Quests:" + pscShortName, FragmentName = "Fragment_Stage_0090_Item_00" });

            // 9. QUEST FINALIZE
            var quest = new Quest(questFormKey, Fallout4Release.Fallout4) {
                EditorID = "COMGemini",
                Name = new TranslatedString(Language.English, "Gemini"),
                Data = new QuestData { Flags = Quest.Flag.StartGameEnabled | Quest.Flag.AllowRepeatedStages | Quest.Flag.StartsEnabled | Quest.Flag.RunOnce | Quest.Flag.AddIdleTopicToHello, Priority = 70 },
                Aliases = new ExtendedList<AQuestAlias> { aliasGemini, aliasCompanion, aliasDogmeat },
                Stages = new ExtendedList<QuestStage> { stage80, stage90 },
                Scenes = new ExtendedList<Scene> { recruitScene, dismissScene },
                DialogTopics = new ExtendedList<DialogTopic> { greetingTopic, gemNpcPos, gemNpcNeg, gemNpcNeu, gemNpcQue, gemPlayerPos, gemPlayerNeg, gemPlayerNeu, gemPlayerQue, gemDismissNpcPos, gemDismissPlayerPos },
                VirtualMachineAdapter = vmad
            };
            mod.Quests.Add(quest);

            // 10. NPC SCRIPT
            var npcVmad = new VirtualMachineAdapter { Version = 6, ObjectFormat = 2 };
            var companionScript = new ScriptEntry { Name = "CompanionActorScript", Properties = new ExtendedList<ScriptProperty>() };
            void AddScriptProp(string name, IMajorRecordGetter? rec) {
                if (rec != null) companionScript.Properties.Add(new ScriptObjectProperty { Name = name, Object = rec.FormKey.ToLink<IFallout4MajorRecordGetter>(), Alias = -1 });
            }
            AddScriptProp("Tutorial", tutorialQuest);
            AddScriptProp("TemporaryAngerLevel", avTemporaryAngerLevel);
            AddScriptProp("MQComplete", globalMQComplete);
            AddScriptProp("HomeLocation", homeLocation);
            AddScriptProp("Experience", avExperience);
            AddScriptProp("HasItemForPlayer", avHasItemForPlayer);
            AddScriptProp("CA_Event_Murder", kwCA_Event_Murder);
            AddScriptProp("DismissScene", dismissScene);
            AddScriptProp("StartingThreshold", globalCA_00);
            AddScriptProp("InfatuationThreshold", globalCA_Infatuation);
            AddScriptProp("InfatuationPerk", piperInfatuationPerk);
            AddScriptProp("InfatuationPerkMessage", piperInfatuationMsg);
            AddScriptProp("DislikesEvent", kwCA_CustomEvent_Dislikes);
            AddScriptProp("HatesEvent", kwCA_CustomEvent_Hates);
            AddScriptProp("LikesEvent", kwCA_CustomEvent_Likes);
            AddScriptProp("LovesEvent", kwCA_CustomEvent_Loves);
            AddScriptProp("KeywordsToAddWhileCurrentCompanion", kwPlayerCanStimpak);
            npcVmad.Scripts.Add(companionScript);
            mod.Npcs.Records.First(n => n.EditorID == "CompanionGemini").VirtualMachineAdapter = npcVmad;

            // 11. SAVE
            mod.WriteToBinary("CompanionGemini.esp", new BinaryWriteParameters { MastersListOrdering = new MastersListOrderingByLoadOrder(env.LoadOrder) });
            Console.WriteLine("ESP Saved: CompanionGemini.esp");

            // PSC GENERATION
            string pscContent = ";BEGIN FRAGMENT CODE\n" +
                                "Scriptname Fragments:Quests:" + pscShortName + " Extends Quest Hidden Const\n\n" +
                                ";BEGIN FRAGMENT Fragment_Stage_0080_Item_00\n" +
                                "Function Fragment_Stage_0080_Item_00()\n" +
                                ";BEGIN CODE\n" +
                                "FollowersScript.GetScript().SetCompanion(Alias_Gemini.GetActorReference())\n" +
                                ";END CODE\n" +
                                "EndFunction\n" +
                                ";END FRAGMENT\n\n" +
                                ";BEGIN FRAGMENT Fragment_Stage_0090_Item_00\n" +
                                "Function Fragment_Stage_0090_Item_00()\n" +
                                ";BEGIN CODE\n" +
                                "FollowersScript.GetScript().DismissCompanion(Alias_Gemini.GetActorReference())\n" +
                                ";END CODE\n" +
                                "EndFunction\n" +
                                ";END FRAGMENT\n\n" +
                                "ReferenceAlias Property Alias_Gemini Auto Const Mandatory\n" +
                                "ReferenceAlias Property Alias_Companion Auto Const Mandatory\n" +
                                "ReferenceAlias Property Alias_Dogmeat Auto Const Mandatory\n" +
                                "FollowersScript Property Followers Auto Const Mandatory\n";

            File.WriteAllText(pscShortName + ".psc", pscContent);
            Console.WriteLine("PSC Saved: " + pscShortName + ".psc");
        }
    }
}