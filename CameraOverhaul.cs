using Planetbase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Tahvohck_Mods.JPFariasUpdates
{
    class CameraOverhaul
    {
        private readonly static Harmony harmInst = new Harmony(typeof(CameraOverhaul).FullName);

        public static void Init()
        {
            harmInst.PatchAll();
        }
    }


    public abstract class CustomCameraManager : CameraManager
    {
        // These are not const so other mods can change them if they want
        public static float MIN_HEIGHT = 12f;
        public static float MAX_HEIGHT = 120f;

        // Protected instead of private - Can be accessed by subclasses
        protected static float VerticalRotationAcceleration = 0f;
        protected static float PreviousMouseY = 0f;

        protected static float AlternateRotationAcceleration = 0f;

        protected static Plane GroundPlane = new Plane(
            Vector3.up,
            new Vector3(TGenTotalSize, 0f, TGenTotalSize) * 0.5f);

        protected static int Modulesize = 0;
        protected static bool IsPlacingModule = false;
        private float ZoomAxis;

        // Taken directly from the source code but then made into a public property.
        protected const float _TGenTotalSize = 2000f;
        public static float TGenTotalSize => _TGenTotalSize;

        // Constants
        protected static readonly float thresholdMovementY = 0.01f;
        protected static readonly float thresholdMovementXZ = 0.001f;
        protected static readonly float thresholdRotation = 0.01f;
        protected static readonly float MinSpeedLinear = 0.01f;
        protected static readonly float MaxSpeedLinear = 100f;
        protected static readonly float FactorSpeedZoom = 60f;
        protected static readonly float FactorSpeedLateral = 80f;
        protected static readonly float FactorSpeedRotation = 120f;

        // Fields that are private in CameraManager
        protected static Vector3 mAcceleration = Vector3.zero;  // TODO: Update assignment
        protected static float mCurrentHeight;                  // TODO: Update assignment
        protected static float mTargetHeight;                   // TODO: Might not need this
        protected static float mRotationAcceleration;           // TODO: Get from somewhere
        protected static float mVerticalRotationAcceleration;   // TODO: Get from somewhere

        public new void update(float timeStep)
        {

        public new void fixedUpdate(float timeStep, int frameIndex)
        {

        }
    }
}
