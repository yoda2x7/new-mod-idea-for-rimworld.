using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalSpawner
{
    public class Building_UniversalSpawnerBench : Building
    {
        private string selectedDefName;

        private ThingDef SelectedDef
        {
            get
            {
                if (selectedDefName == null)
                {
                    return null;
                }

                return DefDatabase<ThingDef>.GetNamedSilentFail(selectedDefName);
            }
            set
            {
                selectedDefName = value?.defName;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref selectedDefName, "selectedDefName");
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "Select item",
                defaultDesc = "Choose which item this bench will spawn (costs 1 wood).",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Select", true),
                action = () => Find.WindowStack.Add(new Dialog_SelectThingDef(def => SelectedDef = def))
            };

            yield return new Command_Action
            {
                defaultLabel = "Spawn item",
                defaultDesc = "Consume 1 wood and spawn the selected item.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", true),
                action = TrySpawnSelected
            };
        }

        private void TrySpawnSelected()
        {
            if (SelectedDef == null)
            {
                Messages.Message("Select an item first.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!TryConsumeWood())
            {
                Messages.Message("Need 1 wood to spawn an item.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Thing spawned = MakeThingForDef(SelectedDef);
            if (spawned == null)
            {
                Messages.Message("Unable to spawn that item.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            GenPlace.TryPlaceThing(spawned, Position, Map, ThingPlaceMode.Near);
        }

        private bool TryConsumeWood()
        {
            Thing wood = GenClosest.ClosestThingReachable(Position, Map, ThingRequest.ForDef(ThingDefOf.WoodLog),
                PathEndMode.ClosestTouch, TraverseParms.For(TraverseMode.PassDoors), 20f);

            if (wood == null)
            {
                return false;
            }

            wood.SplitOff(1).Destroy(DestroyMode.Vanish);
            return true;
        }

        private Thing MakeThingForDef(ThingDef def)
        {
            try
            {
                ThingDef stuff = ChooseStuff(def);
                return ThingMaker.MakeThing(def, stuff);
            }
            catch (Exception exception)
            {
                Log.Warning($"[UniversalSpawner] Failed to create thing {def?.defName}: {exception}");
                return null;
            }
        }

        private ThingDef ChooseStuff(ThingDef def)
        {
            if (def == null || !def.MadeFromStuff)
            {
                return null;
            }

            List<ThingDef> candidates = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(candidate => candidate.IsStuff && def.stuffCategories != null && candidate.stuffProps != null)
                .Where(candidate => def.stuffCategories.Any(category => candidate.stuffProps.categories.Contains(category)))
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            ThingDef preferred = candidates.FirstOrDefault(candidate => candidate.defName == "Steel")
                ?? candidates.FirstOrDefault(candidate => candidate.defName == "WoodLog")
                ?? candidates.First();

            return preferred;
        }
    }

    public class Dialog_SelectThingDef : Window
    {
        private readonly Action<ThingDef> onChosen;
        private Vector2 scrollPosition;
        private string searchText = string.Empty;

        public Dialog_SelectThingDef(Action<ThingDef> onChosen)
        {
            this.onChosen = onChosen;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(620f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 40f), "Select item to spawn");

            Rect searchRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, 30f);
            searchText = Widgets.TextField(searchRect, searchText);

            Rect outRect = new Rect(inRect.x, inRect.y + 85f, inRect.width, inRect.height - 130f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, GetListHeight(outRect.width - 16f));

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (ThingDef def in GetSpawnableDefs())
            {
                Rect rowRect = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(rowRect, def.LabelCap))
                {
                    onChosen?.Invoke(def);
                    Close();
                }
                y += 32f;
            }

            Widgets.EndScrollView();
        }

        private float GetListHeight(float width)
        {
            int count = GetSpawnableDefs().Count();
            return count * 32f;
        }

        private IEnumerable<ThingDef> GetSpawnableDefs()
        {
            IEnumerable<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.thingClass != null)
                .Where(def => def.category == ThingCategory.Item || def.category == ThingCategory.Building)
                .Where(def => !def.destroyOnDrop)
                .Where(def => def.thingSetMakerTags == null || !def.thingSetMakerTags.Contains("NotSpawnable"));

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                defs = defs.Where(def => def.LabelCap.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || def.defName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return defs.OrderBy(def => def.LabelCap);
        }
    }
}
