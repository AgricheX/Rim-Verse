using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    [StaticConstructorOnStartup]
    public class ITab_RimverseShip : ITab
    {
        private static readonly Vector2 WinSize = new Vector2(420f, 480f);
        private static readonly Texture2D HullBar = SolidColorMaterials.NewSolidColorTexture(new Color(0.6f, 0.6f, 0.65f));
        private static readonly Texture2D AirBar = SolidColorMaterials.NewSolidColorTexture(new Color(0.4f, 0.7f, 1f));
        private static readonly Texture2D HeatBar = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.45f, 0.2f));

        public ITab_RimverseShip()
        {
            size = WinSize;
            labelKey = "Rimverse_TabShip";
        }

        private RimverseShip SelShip => SelThing?.TryGetComp<CompShipPart>()?.Ship;

        public override bool IsVisible => SelShip != null;

        protected override void FillTab()
        {
            RimverseShip ship = SelShip;
            if (ship == null) return;
            Rect rect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(12f);
            Listing_Standard l = new Listing_Standard();
            l.Begin(rect);
            Text.Font = GameFont.Medium;
            l.Label(ship.Label);
            Text.Font = GameFont.Small;
            if (l.ButtonText("Rimverse_Rename".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RenameRimverseShip(ship.state));
            }
            l.Label("Rimverse_ShipClass".Translate() + ": " + ("Rimverse_Class_" + ship.shipClass).Translate());
            l.Label("Rimverse_ShipCells".Translate(ship.cells.Count, ship.cellLimit) + (ship.overLimit ? " " + (string)"Rimverse_OverLimitShort".Translate() : ""));
            l.GapLine();
            Bar(l, "Rimverse_MeterIntegrity".Translate(), ship.Integrity, ship.hullCurrent.ToString("0") + " / " + (ship.hullMax + ship.state.lostHullPoints).ToString("0"), HullBar);
            Bar(l, "Rimverse_MeterAir".Translate(), ship.AirFraction, ship.state.airStock.ToString("0") + " / " + ship.airCapacity.ToString("0") + " APH", AirBar);
            Bar(l, "Rimverse_MeterHeat".Translate(), ship.HeatFraction, ship.state.heat.ToString("0") + " / " + ship.heatCapacity.ToString("0") + " UC", HeatBar);
            PowerNet net = ship.bridge.PowerComp?.PowerNet;
            string power = net == null ? (string)"Rimverse_NoPowerNet".Translate() : (net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick).ToString("0") + " W";
            l.Label("Rimverse_MeterPower".Translate() + ": " + power);
            l.GapLine();
            l.Label("Rimverse_ShipMass".Translate() + ": " + ship.mass.ToString("0.#") + " t");
            l.Label("Rimverse_ShipThrust".Translate() + ": " + ship.thrust.ToString("0"));
            l.Label("Rimverse_AirFlow".Translate(ship.airGeneration.ToString("0.#"), ShipMeters.CrewConsumptionPerHour(ship).ToString("0.#")));
            l.Label("Rimverse_HeatFlow".Translate(ship.heatGeneration.ToString("0.#"), ship.heatRemoval.ToString("0.#")));
            l.Label("Rimverse_ShipFuel".Translate(ShipLaunchUtility.FuelAvailable(ship).ToString("0"), ship.fuelCapacity.ToString("0")));
            l.Label("Rimverse_ShipCrew".Translate(ship.crewCount));
            ShipCrewUtility.PilotAndCopilot(ship, out Pawn pilot, out Pawn copilot);
            l.Label("Rimverse_ShipPilot".Translate(pilot != null ? pilot.LabelShort + " (" + ShipCrewUtility.PilotLevel(pilot).ToString("0.#") + ")" : "-", copilot != null ? copilot.LabelShort : "-"));
            l.Label("Rimverse_ShipDodge".Translate(ShipCrewUtility.Dodge(ship, pilot).ToStringPercent()));
            if (ship.multipleBridges)
            {
                GUI.color = ColorLibrary.RedReadable;
                l.Label("Rimverse_ProblemMultipleBridges".Translate());
                GUI.color = Color.white;
            }
            l.End();
        }

        private static void Bar(Listing_Standard l, string label, float fraction, string value, Texture2D tex)
        {
            Rect r = l.GetRect(24f);
            Rect labelRect = r.LeftPart(0.3f);
            Rect barRect = r.RightPart(0.7f);
            Widgets.Label(labelRect, label);
            Widgets.FillableBar(barRect, Mathf.Clamp01(fraction), tex);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, fraction.ToStringPercent() + "  " + value);
            Text.Anchor = TextAnchor.UpperLeft;
            l.Gap(4f);
        }
    }

    public class Dialog_RenameRimverseShip : Window
    {
        private readonly ShipState state;
        private string curName;

        public Dialog_RenameRimverseShip(ShipState state)
        {
            this.state = state;
            curName = state.name;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(320f, 160f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Rimverse_Rename".Translate());
            curName = Widgets.TextField(new Rect(0f, 36f, inRect.width, 32f), curName);
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 36f, inRect.width, 36f), "OK".Translate()) && !curName.NullOrEmpty())
            {
                state.name = curName.Trim();
                Close();
            }
        }
    }
}
