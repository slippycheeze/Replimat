﻿using Verse;
using Verse.Sound;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using System.Text;

namespace Replimat
{
    public class Building_ReplimatTerminal : Building
    {
        public CompPowerTrader powerComp;

        public static int CollectDuration = GenTicks.SecondsToTicks(2f);

        public StorageSettings MealFilter;

        public FoodPreferability MaxPreferability = FoodPreferability.MealLavish;

        public ThingDef SelectedFood = ThingDefOf.MealNutrientPaste;

        public int ReplicatingTicks = 0;

        public bool hasReplimatTanks;

        public bool hasEnoughFeedstock;

        public List<Building_ReplimatFeedTank> GetTanks => Map.listerThings.ThingsOfDef(ReplimatDef.FeedTankDef).Select(x => x as Building_ReplimatFeedTank).Where(x => x.PowerComp.PowerNet == this.PowerComp.PowerNet).ToList();

        public bool CanDispenseNow
        {
            get
            {
                CheckFeedstockAvailability(1f);
                return this.powerComp.PowerOn && hasReplimatTanks && hasEnoughFeedstock;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.powerComp = base.GetComp<CompPowerTrader>();
            this.MealFilter = new StorageSettings();
            if (this.def.building.defaultStorageSettings != null)
            {
                this.MealFilter.CopyFrom(this.def.building.defaultStorageSettings);
            }
            CheckFeedstockAvailability(1f);
            ChooseMeal();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look<StorageSettings>(ref MealFilter, "MealFilter", this);
        }

        public void ChooseMeal()
        {
            SelectedFood = def.building.fixedStorageSettings.filter.AllowedThingDefs.Where(x => x.ingestible.preferability == MaxPreferability).RandomElement();
        }

        public void CheckFeedstockAvailability(float feedstockNeeded)
        {
            List<Building_ReplimatFeedTank> feedstockTanks = GetTanks;

            float totalAvailableFeedstock = feedstockTanks.Sum(x => x.storedFeedstock);

            hasReplimatTanks = feedstockTanks.Count() > 0;

            Log.Message("Replimat: " + totalAvailableFeedstock.ToString() + " feedstock available across " + feedstockTanks.Count().ToString() + " tanks");

            if (totalAvailableFeedstock >= feedstockNeeded)
            {
                hasEnoughFeedstock = true;
            }
            else
            {
                hasEnoughFeedstock = false;
            }
        }

        public void ConsumeFeedstock(float feedstockNeeded)
        {
            List<Building_ReplimatFeedTank> feedstockTanks = GetTanks;

            float totalAvailableFeedstock = feedstockTanks.Sum(x => x.storedFeedstock);

            if (feedstockTanks.Count() > 0)
            {
                feedstockTanks.Shuffle();

                if (totalAvailableFeedstock >= feedstockNeeded)
                {
                    float feedstockLeftToConsume = feedstockNeeded;

                    foreach (var currentTank in feedstockTanks)
                    {
                        if (feedstockLeftToConsume <= 0f)
                        {
                            break;
                        }
                        else
                        {
                            float num = Math.Min(feedstockLeftToConsume, currentTank.StoredFeedstock);

                            currentTank.DrawFeedstock(num);

                            feedstockLeftToConsume -= num;
                        }
                    }
                }

            }
            else
            {
                Log.Error("Replimat: Tried to draw feedstock from non-existent tanks!");
            }
        }




        public void Replicate()
        {
            if (!this.CanDispenseNow)
            {
                return;
            }

            ReplicatingTicks = GenTicks.SecondsToTicks(2f);
            CheckFeedstockAvailability(1f);
            this.def.building.soundDispense.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
        }

        public Thing TryDispenseFood()
        {
            if (!this.CanDispenseNow)
            {
                return null;
            }
            ChooseMeal();
            Thing dispensedMeal = ThingMaker.MakeThing(SelectedFood, null);
            float dispensedMealMass = dispensedMeal.def.BaseMass;
            Log.Message("Replimat: " + dispensedMeal.ToString() + " has mass of " + dispensedMealMass.ToString() + "kg (" + ReplimatUtility.convertMassToFeedstockVolume(dispensedMealMass) + "L feedstock required)");
            ConsumeFeedstock(ReplimatUtility.convertMassToFeedstockVolume(dispensedMealMass));
            return dispensedMeal;
        }

        public override void Draw()
        {
            base.Draw();

            if (ReplicatingTicks > 0)
            {
                float alpha;
                float quart = CollectDuration * 0.25f;
                if (ReplicatingTicks < quart)
                {
                    alpha = Mathf.InverseLerp(0, quart, ReplicatingTicks);
                }
                else if (ReplicatingTicks > quart * 3f)
                {
                    alpha = Mathf.InverseLerp(CollectDuration, quart * 3f, ReplicatingTicks);
                }
                else
                {
                    alpha = 1f;
                }

                Graphics.DrawMesh(GraphicsLoader.replimatTerminalGlow.MeshAt(base.Rotation), this.DrawPos + Altitudes.AltIncVect, Quaternion.identity,
                    FadedMaterialPool.FadedVersionOf(GraphicsLoader.replimatTerminalGlow.MatAt(base.Rotation, null), alpha), 0);
            }
        }

        public override void Tick()
        {
            base.Tick();

            powerComp.PowerOutput = -125f;

            if (ReplicatingTicks > 0)
            {
                ReplicatingTicks--;
                powerComp.PowerOutput = -1500f;
            }

        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (!this.hasReplimatTanks)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("Requires connection to Replimat Feedstock Tank");
            }

            if (this.hasReplimatTanks && !this.hasEnoughFeedstock)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("Insufficient Feedstock");
            }
            return stringBuilder.ToString();
        }


    }
}