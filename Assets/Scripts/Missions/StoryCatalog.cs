using System.Collections.Generic;
using UnityEngine;
using SanMonica.Data;

namespace SanMonica.Missions
{
    /// <summary>
    /// "Saltwater Debt" - the original story of San Monica, told in five
    /// chapters. Dominic Vela comes home to bury his brother and finds the
    /// docks, the cartel and a corporation all holding a piece of the answer.
    /// </summary>
    public static class StoryCatalog
    {
        public const string ProtagonistName = "Dominic \"Dom\" Vela";

        public static List<MissionDefinition> BuildStory()
        {
            var list = new List<MissionDefinition>();

            // ---------------- Chapter 1: Homecoming ----------------
            list.Add(new MissionDefinition
            {
                Id = "s1_homecoming", Title = "Homecoming", Giver = "Ruben \"Rook\" Castellanos", Chapter = 1,
                Kind = MissionKind.Story, RewardCash = 800, RewardRespect = 5,
                Briefing = "Rook wants to see you the moment you are back in San Monica.",
                StartAnchor = MissionAnchor.Near(ShopType.Mechanic),
                IntroLines = new[]
                {
                    "ROOK: Eight years, Dom. You could have called.",
                    "DOM: I came for the funeral. That's all this is.",
                    "ROOK: Mateo didn't drown. Whatever they told you, he didn't drown."
                },
                OutroLines = new[] { "ROOK: Get some sleep. Tomorrow we go look at the water." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Meet Rook at the garage", Anchor = MissionAnchor.Near(ShopType.Mechanic), Radius = 6f },
                    new MissionObjective { Type = ObjectiveType.EnterVehicle, Description = "Take Rook's car", Anchor = MissionAnchor.FromPlayer(14f), VehicleId = "brawler" },
                    new MissionObjective { Type = ObjectiveType.GoToInVehicle, Description = "Drive to the Iron Bay docks", Anchor = MissionAnchor.Mark(LandmarkKind.Port, 60f), Radius = 22f,
                        Lines = new[] { "ROOK: Slow down at the gate. They know this car." } }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "s2_lastshift", Title = "Last Shift", Giver = "Ruben \"Rook\" Castellanos", Chapter = 1,
                Prerequisites = new[] { "s1_homecoming" }, RewardCash = 1500, RewardRespect = 8,
                Briefing = "Mateo's locker is still at the terminal. So are the men who emptied it.",
                StartAnchor = MissionAnchor.Mark(LandmarkKind.Port, 80f),
                IntroLines = new[]
                {
                    "ROOK: Locker forty-one. Anything in it, take it.",
                    "DOM: And if someone's watching the locker?",
                    "ROOK: Then we finally have someone to ask."
                },
                OutroLines = new[] { "ROOK: A Halcyon manifest. In a dockworker's locker. That's not nothing." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Reach locker 41", Anchor = MissionAnchor.Mark(LandmarkKind.Port, 90f), Radius = 8f },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Deal with the Iron Bay crew", Anchor = MissionAnchor.FromPlayer(18f),
                        EnemyCount = 4, PedArchetype = "ironbay", EnemyFaction = Faction.IronBaySyndicate },
                    new MissionObjective { Type = ObjectiveType.Collect, Description = "Take the manifest", Anchor = MissionAnchor.FromPlayer(10f), Count = 1 },
                    new MissionObjective { Type = ObjectiveType.Escape, Description = "Get clear of the docks", Anchor = MissionAnchor.Mark(LandmarkKind.Marigold, 120f), Radius = 40f, WantedLevelOnStart = 2 }
                },
                TurnsHostile = Faction.IronBaySyndicate
            });

            list.Add(new MissionDefinition
            {
                Id = "s3_signal", Title = "Signal Loss", Giver = "Ivy Marlowe", Chapter = 1,
                Prerequisites = new[] { "s2_lastshift" }, RewardCash = 2200, RewardRespect = 10,
                Briefing = "Ivy can read the manifest, but she needs a receiver from the university.",
                StartAnchor = MissionAnchor.Centre(DistrictType.University),
                IntroLines = new[]
                {
                    "IVY: The manifest is signed by a shipping ID that stopped existing in 2019.",
                    "DOM: So it's fake.",
                    "IVY: No. It's older than the company that used it. That's much worse."
                },
                OutroLines = new[] { "IVY: Give me two days. Don't get arrested in the meantime." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Meet Ivy at Kestrel University", Anchor = MissionAnchor.Centre(DistrictType.University), Radius = 7f },
                    new MissionObjective { Type = ObjectiveType.StealVehicle, Description = "Take the university van", Anchor = MissionAnchor.In(DistrictType.University, 90f), VehicleId = "courier" },
                    new MissionObjective { Type = ObjectiveType.DeliverVehicle, Description = "Deliver the van to Ivy's lock-up", Anchor = MissionAnchor.In(DistrictType.Marigold, 140f), Radius = 14f }
                }
            });

            // ---------------- Chapter 2: The Quarter ----------------
            list.Add(new MissionDefinition
            {
                Id = "s4_marigold", Title = "Marigold Rules", Giver = "Calle Nueve", Chapter = 2,
                Prerequisites = new[] { "s3_signal" }, RewardCash = 3000, RewardRespect = 12,
                Briefing = "The Quarter will talk to you, but only after you settle a debt for them.",
                StartAnchor = MissionAnchor.Centre(DistrictType.Marigold),
                IntroLines = new[]
                {
                    "TERE: You want names, you clean up a corner first.",
                    "DOM: Whose corner?",
                    "TERE: Serrano's. That's the point."
                },
                OutroLines = new[] { "TERE: Now you exist in the Quarter. Congratulations, that means Serrano knows you too." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Reach the corner on Marigold", Anchor = MissionAnchor.In(DistrictType.Marigold, 200f), Radius = 10f },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Clear the Serrano dealers", Anchor = MissionAnchor.FromPlayer(20f),
                        EnemyCount = 5, PedArchetype = "serrano", EnemyFaction = Faction.SerranoCartel },
                    new MissionObjective { Type = ObjectiveType.LoseWanted, Description = "Lose the police", WantedLevelOnStart = 2 }
                },
                TurnsHostile = Faction.SerranoCartel
            });

            list.Add(new MissionDefinition
            {
                Id = "s5_courier", Title = "Courier Work", Giver = "Tere Bastida", Chapter = 2,
                Prerequisites = new[] { "s4_marigold" }, RewardCash = 3600, RewardRespect = 10,
                Briefing = "Three drops across the city, one timer, no excuses.",
                StartAnchor = MissionAnchor.Centre(DistrictType.Marigold),
                IntroLines = new[] { "TERE: Three packages. Forty minutes of city traffic. Go." },
                OutroLines = new[] { "TERE: You drive like someone who used to do this for a living." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.EnterVehicle, Description = "Get a car", Anchor = MissionAnchor.FromPlayer(16f), VehicleId = "meridian" },
                    new MissionObjective { Type = ObjectiveType.Deliver, Description = "First drop - Sable Row", Anchor = MissionAnchor.In(DistrictType.Residential, 300f), Radius = 12f, TimeLimit = 150f },
                    new MissionObjective { Type = ObjectiveType.Deliver, Description = "Second drop - Palmetto Shore", Anchor = MissionAnchor.Mark(LandmarkKind.Beach, 200f), Radius = 12f, TimeLimit = 170f },
                    new MissionObjective { Type = ObjectiveType.Deliver, Description = "Last drop - Foundry Flats", Anchor = MissionAnchor.Mark(LandmarkKind.Foundry, 200f), Radius = 12f, TimeLimit = 190f }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "s6_tollbooth", Title = "The Tollbooth", Giver = "Ivy Marlowe", Chapter = 2,
                Prerequisites = new[] { "s5_courier" }, RewardCash = 5200, RewardRespect = 14,
                Briefing = "Every container that matters passes one scanner. Ivy wants it blinded for ten minutes.",
                StartAnchor = MissionAnchor.Mark(LandmarkKind.Port, 100f),
                IntroLines = new[]
                {
                    "IVY: The scanner logs to Halcyon, not to the port authority.",
                    "DOM: So we're stealing from a company that isn't supposed to be there.",
                    "IVY: We're stealing proof that it is."
                },
                OutroLines = new[] { "IVY: Ten minutes of blind port. Somebody moved something big through it tonight." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Get inside the port yard", Anchor = MissionAnchor.Mark(LandmarkKind.Port, 60f), Radius = 12f },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Silence the Vanguard patrol", Anchor = MissionAnchor.FromPlayer(24f),
                        EnemyCount = 4, PedArchetype = "security", EnemyFaction = Faction.VanguardSecurity },
                    new MissionObjective { Type = ObjectiveType.Collect, Description = "Pull the scanner drive", Anchor = MissionAnchor.FromPlayer(12f) },
                    new MissionObjective { Type = ObjectiveType.Survive, Description = "Hold the yard until Ivy finishes", SurviveSeconds = 70f, EnemyCount = 6, PedArchetype = "security", EnemyFaction = Faction.VanguardSecurity },
                    new MissionObjective { Type = ObjectiveType.Escape, Description = "Get out of the port", Anchor = MissionAnchor.Mark(LandmarkKind.Foundry, 200f), Radius = 45f, WantedLevelOnStart = 3 }
                },
                TurnsHostile = Faction.VanguardSecurity
            });

            // ---------------- Chapter 3: Deep Water ----------------
            list.Add(new MissionDefinition
            {
                Id = "s7_deepwater", Title = "Deep Water", Giver = "Talia Reyes", Chapter = 3,
                Prerequisites = new[] { "s6_tollbooth" }, RewardCash = 6500, RewardRespect = 16,
                Briefing = "A former officer says your brother's file was closed by someone who never opened it.",
                StartAnchor = MissionAnchor.Mark(LandmarkKind.Marina, 60f),
                IntroLines = new[]
                {
                    "TALIA: I signed the report. I never wrote it.",
                    "DOM: Then who did?",
                    "TALIA: The signature block came back from Halcyon legal. Twelve minutes after the body."
                },
                OutroLines = new[] { "TALIA: If you go out on that water, Dom, don't come back the same way you left." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Meet Talia at the marina", Anchor = MissionAnchor.Mark(LandmarkKind.Marina, 40f), Radius = 7f },
                    new MissionObjective { Type = ObjectiveType.EnterVehicle, Description = "Take a boat", Anchor = MissionAnchor.Mark(LandmarkKind.Marina, 30f), VehicleId = "skiff" },
                    new MissionObjective { Type = ObjectiveType.GoToInVehicle, Description = "Reach the dive marker in the bay", Anchor = MissionAnchor.At(new Vector3(-3900f, 0f, -400f)), Radius = 30f },
                    new MissionObjective { Type = ObjectiveType.Collect, Description = "Dive and recover the case", Anchor = MissionAnchor.FromPlayer(25f) }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "s8_hillsjob", Title = "The Hills Job", Giver = "Ivy Marlowe", Chapter = 3,
                Prerequisites = new[] { "s7_deepwater" }, RewardCash = 9000, RewardRespect = 18,
                Briefing = "A Halcyon vice president keeps his files at home in Crestwood Hills.",
                StartAnchor = MissionAnchor.Mark(LandmarkKind.Crestwood, 120f),
                IntroLines = new[] { "IVY: In and out. If the alarm goes, Vanguard is four minutes away." },
                OutroLines = new[] { "IVY: Dom - your brother's name is in these files. Under 'assets'." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Reach the villa", Anchor = MissionAnchor.Mark(LandmarkKind.Crestwood, 120f), Radius = 10f },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Handle the house security", Anchor = MissionAnchor.FromPlayer(22f),
                        EnemyCount = 5, PedArchetype = "security", EnemyFaction = Faction.VanguardSecurity },
                    new MissionObjective { Type = ObjectiveType.Collect, Description = "Copy the home server", Anchor = MissionAnchor.FromPlayer(12f) },
                    new MissionObjective { Type = ObjectiveType.Escape, Description = "Get out of the Hills", Anchor = MissionAnchor.Centre(DistrictType.Downtown), Radius = 60f, WantedLevelOnStart = 3 }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "s9_nightshift", Title = "Night Shift", Giver = "Ruben \"Rook\" Castellanos", Chapter = 3,
                Prerequisites = new[] { "s8_hillsjob" }, RewardCash = 7400, RewardRespect = 14,
                Briefing = "Serrano put a price on the garage. Rook is not leaving it.",
                StartAnchor = MissionAnchor.Near(ShopType.Mechanic),
                IntroLines = new[] { "ROOK: They came at midnight. They'll come again at midnight." },
                OutroLines = new[] { "ROOK: Thirty years I fixed cars here. One night and it's a war." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Get to the garage", Anchor = MissionAnchor.Near(ShopType.Mechanic), Radius = 8f },
                    new MissionObjective { Type = ObjectiveType.Survive, Description = "Hold off the Serrano crews", SurviveSeconds = 110f, EnemyCount = 8, PedArchetype = "serrano", EnemyFaction = Faction.SerranoCartel },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Finish the last crew", Anchor = MissionAnchor.FromPlayer(26f), EnemyCount = 4, PedArchetype = "serrano", EnemyFaction = Faction.SerranoCartel }
                }
            });

            // ---------------- Chapter 4: Leverage ----------------
            list.Add(new MissionDefinition
            {
                Id = "s10_convoy", Title = "Convoy", Giver = "Tere Bastida", Chapter = 4,
                Prerequisites = new[] { "s9_nightshift" }, RewardCash = 12000, RewardRespect = 20,
                Briefing = "Serrano moves cash out of the Badlands every Thursday. This is Thursday.",
                StartAnchor = MissionAnchor.Centre(DistrictType.Badlands),
                IntroLines = new[] { "TERE: Two cars, one truck. Take the truck, burn the rest." },
                OutroLines = new[] { "TERE: That is the loudest thing anyone has done to Serrano in a decade." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoToInVehicle, Description = "Intercept the convoy on the desert route", Anchor = MissionAnchor.Centre(DistrictType.Badlands), Radius = 40f },
                    new MissionObjective { Type = ObjectiveType.DestroyVehicle, Description = "Destroy the escort cars", Count = 2, VehicleId = "cartel-runner", Anchor = MissionAnchor.FromPlayer(45f) },
                    new MissionObjective { Type = ObjectiveType.StealVehicle, Description = "Take the money truck", Anchor = MissionAnchor.FromPlayer(40f), VehicleId = "syndicate-van" },
                    new MissionObjective { Type = ObjectiveType.DeliverVehicle, Description = "Bring it to the Quarter", Anchor = MissionAnchor.Centre(DistrictType.Marigold), Radius = 16f, WantedLevelOnStart = 3 }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "s11_tower", Title = "Forty Floors", Giver = "Ivy Marlowe", Chapter = 4,
                Prerequisites = new[] { "s10_convoy" }, RewardCash = 18000, RewardRespect = 24,
                Briefing = "Halcyon keeps the original of everything on one floor of one tower downtown.",
                StartAnchor = MissionAnchor.Centre(DistrictType.Downtown),
                IntroLines = new[]
                {
                    "IVY: Forty floors up. One elevator. A lot of Vanguard.",
                    "DOM: And if we get it?",
                    "IVY: Then a company stops being a rumour and starts being a defendant."
                },
                OutroLines = new[] { "IVY: They logged the theft before we were out of the lobby. They were waiting." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Reach Halcyon Tower", Anchor = MissionAnchor.Centre(DistrictType.Downtown), Radius = 12f },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Clear the lobby detail", Anchor = MissionAnchor.FromPlayer(20f),
                        EnemyCount = 6, PedArchetype = "halcyon", EnemyFaction = Faction.HalcyonDynamics },
                    new MissionObjective { Type = ObjectiveType.Collect, Description = "Take the archive drive", Anchor = MissionAnchor.FromPlayer(14f) },
                    new MissionObjective { Type = ObjectiveType.Escape, Description = "Get out of downtown", Anchor = MissionAnchor.Mark(LandmarkKind.Beach, 200f), Radius = 55f, WantedLevelOnStart = 4 }
                },
                TurnsHostile = Faction.HalcyonDynamics
            });

            list.Add(new MissionDefinition
            {
                Id = "s12_airfield", Title = "Ground Stop", Giver = "Talia Reyes", Chapter = 4,
                Prerequisites = new[] { "s11_tower" }, RewardCash = 21000, RewardRespect = 26,
                Briefing = "The vice president is flying out of Redwater tonight with the last physical copy.",
                StartAnchor = MissionAnchor.Mark(LandmarkKind.Airport, 200f),
                IntroLines = new[] { "TALIA: If that jet leaves the ground, none of this ever happened." },
                OutroLines = new[] { "TALIA: He talked. He also gave me a name I did not want." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoToInVehicle, Description = "Get to Redwater International", Anchor = MissionAnchor.Mark(LandmarkKind.Airport, 300f), Radius = 40f, TimeLimit = 240f },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Clear the apron", Anchor = MissionAnchor.FromPlayer(30f),
                        EnemyCount = 7, PedArchetype = "halcyon", EnemyFaction = Faction.HalcyonDynamics },
                    new MissionObjective { Type = ObjectiveType.DestroyVehicle, Description = "Stop the jet", Count = 1, VehicleId = "plane-jet", Anchor = MissionAnchor.FromPlayer(60f) },
                    new MissionObjective { Type = ObjectiveType.LoseWanted, Description = "Disappear", WantedLevelOnStart = 4 }
                }
            });

            // ---------------- Chapter 5: Saltwater Debt ----------------
            list.Add(new MissionDefinition
            {
                Id = "s13_ledger", Title = "The Ledger", Giver = "Ivy Marlowe", Chapter = 5,
                Prerequisites = new[] { "s12_airfield" }, RewardCash = 26000, RewardRespect = 28,
                Briefing = "The archive names every San Monica official on the Iron Bay payroll. Ivy wants it public.",
                StartAnchor = MissionAnchor.Centre(DistrictType.Marigold),
                IntroLines = new[] { "IVY: Once I publish, the whole city becomes a suspect. Including us." },
                OutroLines = new[] { "IVY: It's out. Whatever happens now happens in daylight." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Meet Ivy at the broadcast mast", Anchor = MissionAnchor.Mark(LandmarkKind.Foundry, 160f), Radius = 10f },
                    new MissionObjective { Type = ObjectiveType.Survive, Description = "Protect the uplink", SurviveSeconds = 130f, EnemyCount = 9, PedArchetype = "halcyon", EnemyFaction = Faction.HalcyonDynamics },
                    new MissionObjective { Type = ObjectiveType.Escape, Description = "Break contact", Anchor = MissionAnchor.Mark(LandmarkKind.Farmland, 400f), Radius = 70f, WantedLevelOnStart = 4 }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "s14_ironbay", Title = "Iron Bay", Giver = "Ruben \"Rook\" Castellanos", Chapter = 5,
                Prerequisites = new[] { "s13_ledger" }, RewardCash = 34000, RewardRespect = 32,
                Briefing = "The Syndicate is loading the last shipment. Rook says it is the one Mateo saw.",
                StartAnchor = MissionAnchor.Mark(LandmarkKind.Port, 120f),
                IntroLines = new[] { "ROOK: Same berth. Same crane. Eight years later." },
                OutroLines = new[] { "ROOK: He saw this. That's all he did. He just saw it." },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Get to berth nine", Anchor = MissionAnchor.Mark(LandmarkKind.Port, 110f), Radius = 14f },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Break the Syndicate line", Anchor = MissionAnchor.FromPlayer(28f),
                        EnemyCount = 10, PedArchetype = "ironbay", EnemyFaction = Faction.IronBaySyndicate },
                    new MissionObjective { Type = ObjectiveType.DestroyVehicle, Description = "Destroy the transport", Count = 1, VehicleId = "hauler", Anchor = MissionAnchor.FromPlayer(50f) },
                    new MissionObjective { Type = ObjectiveType.Escape, Description = "Leave the docks", Anchor = MissionAnchor.Mark(LandmarkKind.Marina, 200f), Radius = 50f, WantedLevelOnStart = 5 }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "s15_saltwater", Title = "Saltwater Debt", Giver = "Dominic Vela", Chapter = 5,
                Prerequisites = new[] { "s14_ironbay" }, RewardCash = 60000, RewardRespect = 50,
                Briefing = "One name is left. He is on a yacht in Halcyon Bay, and he is expecting you.",
                StartAnchor = MissionAnchor.Mark(LandmarkKind.Marina, 60f),
                IntroLines = new[]
                {
                    "DOM: You had eight years to tell me.",
                    "ARDEN VOSS: You had eight years to ask. Neither of us used them well."
                },
                OutroLines = new[]
                {
                    "DOM: It was never worth this.",
                    "ROOK: It never is, Dom. Come home."
                },
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.EnterVehicle, Description = "Take a fast boat", Anchor = MissionAnchor.Mark(LandmarkKind.Marina, 30f), VehicleId = "dartboat" },
                    new MissionObjective { Type = ObjectiveType.GoToInVehicle, Description = "Reach the yacht", Anchor = MissionAnchor.At(new Vector3(-4600f, 0f, 300f)), Radius = 40f },
                    new MissionObjective { Type = ObjectiveType.KillAll, Description = "Clear Voss's detail", Anchor = MissionAnchor.FromPlayer(35f),
                        EnemyCount = 8, PedArchetype = "halcyon", EnemyFaction = Faction.HalcyonDynamics },
                    new MissionObjective { Type = ObjectiveType.KillTarget, Description = "Arden Voss", Anchor = MissionAnchor.FromPlayer(25f), PedArchetype = "halcyon", EnemyFaction = Faction.HalcyonDynamics },
                    new MissionObjective { Type = ObjectiveType.Escape, Description = "Get back to shore", Anchor = MissionAnchor.Mark(LandmarkKind.Beach, 150f), Radius = 60f, WantedLevelOnStart = 4 }
                }
            });

            return list;
        }

        /// <summary>Repeatable side work available across the whole map.</summary>
        public static List<MissionDefinition> BuildSideMissions()
        {
            var list = new List<MissionDefinition>();

            list.Add(new MissionDefinition
            {
                Id = "side_taxi", Title = "Fare Run", Giver = "Meridian Cab Co.", Kind = MissionKind.Delivery,
                RewardCash = 900, RepeatableAfterCompletion = true,
                Briefing = "Pick up a fare and get them across town before they lose patience.",
                StartAnchor = MissionAnchor.FromPlayer(200f),
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoToInVehicle, Description = "Collect the fare", Anchor = MissionAnchor.FromPlayer(320f), Radius = 12f, TimeLimit = 150f },
                    new MissionObjective { Type = ObjectiveType.GoToInVehicle, Description = "Drop the fare off", Anchor = MissionAnchor.FromPlayer(700f), Radius = 14f, TimeLimit = 260f }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "side_race", Title = "Street Race", Giver = "Redline crowd", Kind = MissionKind.Race,
                RewardCash = 2500, RepeatableAfterCompletion = true,
                Briefing = "Four checkpoints, one clock, no rules.",
                StartAnchor = MissionAnchor.FromPlayer(160f),
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.Race, Description = "Checkpoint 1", Anchor = MissionAnchor.FromPlayer(420f), Radius = 18f, TimeLimit = 95f },
                    new MissionObjective { Type = ObjectiveType.Race, Description = "Checkpoint 2", Anchor = MissionAnchor.FromPlayer(700f), Radius = 18f, TimeLimit = 105f },
                    new MissionObjective { Type = ObjectiveType.Race, Description = "Checkpoint 3", Anchor = MissionAnchor.FromPlayer(950f), Radius = 18f, TimeLimit = 115f },
                    new MissionObjective { Type = ObjectiveType.Race, Description = "Finish line", Anchor = MissionAnchor.FromPlayer(1200f), Radius = 20f, TimeLimit = 125f }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "side_repo", Title = "Repossession", Giver = "Rook's Garage", Kind = MissionKind.Side,
                RewardCash = 1800, RepeatableAfterCompletion = true,
                Briefing = "Somebody stopped paying. Bring the car back in one piece.",
                StartAnchor = MissionAnchor.FromPlayer(250f),
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.StealVehicle, Description = "Find and take the car", Anchor = MissionAnchor.FromPlayer(500f), VehicleId = "harborline" },
                    new MissionObjective { Type = ObjectiveType.DeliverVehicle, Description = "Return it to the garage", Anchor = MissionAnchor.Near(ShopType.Mechanic), Radius = 12f }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "side_bounty", Title = "Open Contract", Giver = "Anonymous", Kind = MissionKind.Assassination,
                RewardCash = 4200, RepeatableAfterCompletion = true,
                Briefing = "A name, a location and no questions.",
                StartAnchor = MissionAnchor.FromPlayer(300f),
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Find the target", Anchor = MissionAnchor.FromPlayer(600f), Radius = 26f },
                    new MissionObjective { Type = ObjectiveType.KillTarget, Description = "Eliminate the target", Anchor = MissionAnchor.FromPlayer(20f), PedArchetype = "serrano", EnemyFaction = Faction.SerranoCartel },
                    new MissionObjective { Type = ObjectiveType.LoseWanted, Description = "Walk away clean", WantedLevelOnStart = 2 }
                }
            });

            list.Add(new MissionDefinition
            {
                Id = "side_protect", Title = "Escort", Giver = "Tere Bastida", Kind = MissionKind.Protection,
                RewardCash = 3400, RepeatableAfterCompletion = true,
                Briefing = "Get somebody across the Quarter alive.",
                StartAnchor = MissionAnchor.Centre(DistrictType.Marigold),
                Objectives = new List<MissionObjective>
                {
                    new MissionObjective { Type = ObjectiveType.GoTo, Description = "Meet the client", Anchor = MissionAnchor.Centre(DistrictType.Marigold), Radius = 10f },
                    new MissionObjective { Type = ObjectiveType.Protect, Description = "Keep the client alive", SurviveSeconds = 90f, EnemyCount = 5, PedArchetype = "serrano", EnemyFaction = Faction.SerranoCartel, FailIfTargetDies = true }
                }
            });

            return list;
        }
    }
}
