using RimWorld;
using UnityEngine;
using Verse;

namespace Rimverse
{
    public class LineageExtension : DefModExtension
    {
        public Color? skinColor;
        public float skinColorJitter = 0.06f;
        public BodyTypeDef bodyTypeMale;
        public BodyTypeDef bodyTypeFemale;
        public bool zeroGAdapted;
        public float cabinFeverFactor = 1f;
        public float crushLimit = 1.3f;
        public bool fearOfSky;
        public bool lowGravityClumsy;
        public bool lowGravityStride;
        public bool dryClimatePenalty;
        public float waterSpeedFactor = 1f;
        public float helmetWorkFactor = 1f;
        public ThingDef apparel;
    }
}
