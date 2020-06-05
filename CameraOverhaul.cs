using Planetbase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Tahvohck_Mods.JPFariasUpdates
{
    public class CameraOverhaul
    {
        private readonly static Harmony harmInst = new Harmony(typeof(CameraOverhaul).FullName);
        public static CustomCameraManager camera;

        public static void Init()
        {
            try {
                harmInst.PatchAll();
            } catch (Exception e) {
                Debug.Log(e.Message);
            }
        }
    }


    public class CustomCameraManager
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
        private static float ZoomAxis;

        private static int Diffcheck = 0;

        private static CameraManager _Manager;

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
        protected static readonly float MinRotationalElevation = 20f;
        protected static readonly float MaxRotationalElevation = 87f;

        // Fields that are private in CameraManager
        protected static Vector3 mAcceleration = Vector3.zero;  // TODO: Update assignment
        protected static float mCurrentHeight;                  // TODO: Update assignment
        protected static float mTargetHeight;                   // TODO: Might not need this
        protected static float mRotationAcceleration;           // TODO: Get from somewhere
        protected static float mVerticalRotationAcceleration;   // TODO: Get from somewhere
        protected static bool mLocked;                          // TODO: Get from somewhere

        public CustomCameraManager()
        {
            _Manager = CameraManager.getInstance();
        }

        public void update(float timeStep)
        {
            if (ZoomAxis == 0f) { ZoomAxis = Input.GetAxis("Zoom"); }

            GameState gameState = GameManager.getInstance().getGameState();
            // Substitue true if gamestate is null via null fallback
            if (!(gameState?.isCameraFixed() ?? true)) {
                if (_Manager.isCinematic()) {
                    _Manager.updateCinematic(timeStep);
                } else {
                    GameStateGame game = gameState as GameStateGame;
                    if (game.CurrentState() == GameStateHelper.Mode.PlacingModule && IsPlacingModule) {
                        /* TODO: Determine if this is needed. The game source doesn't include this.
                         * It's used later on to actually work with the module size and I don't
                         * think that that does much in here. It looks like the same code gets run in
                         * GameStateGame.updateUI() as well, so this might have been imported from an older
                         * version of the source code without that method.
                         * */
                        // game.mCurrentModuleSize = mModuleSize
                    }

                    Transform transform = _Manager.getCamera().transform;

                    float xAxisAccel = mAcceleration.x;
                    float yAxisAccel = mAcceleration.y;
                    float zAxisAccel = mAcceleration.z;
                    float xAxisAccelAbsolute = Mathf.Abs(mAcceleration.x);
                    float yAxisAccelAbsolute = Mathf.Abs(mAcceleration.y);
                    float zAxisAccelAbsolute = Mathf.Abs(mAcceleration.z);

                    if (!mLocked) {


                        // Zoom control
                        if (yAxisAccelAbsolute > thresholdMovementY) {
                            float speed = Mathf.Clamp(
                                FactorSpeedZoom * timeStep,
                                MinSpeedLinear, MaxSpeedLinear);
                            float newHeight = Mathf.Clamp(
                                mCurrentHeight + yAxisAccel * speed,
                                MIN_HEIGHT, MAX_HEIGHT);

                            // Constant is an upper bound on the camera elevation angle
                            // TODO: Quaternions instead?
                            if (transform.eulerAngles.x < 86f) {
                                zAxisAccel += (mCurrentHeight - newHeight) / speed;
                                zAxisAccelAbsolute = Mathf.Abs(zAxisAccel);
                            }

                            mCurrentHeight = newHeight;
                            mTargetHeight = mCurrentHeight;
                        }

                        // Forward movement
                        if (zAxisAccelAbsolute > thresholdMovementXZ) {
                            transform.position +=
                                transform.forward.normalized * zAxisAccel * timeStep * FactorSpeedLateral;
                        }

                        // Sideways movement
                        if (xAxisAccelAbsolute > thresholdMovementXZ) {
                            transform.position +=
                                transform.right.normalized * xAxisAccel * timeStep * FactorSpeedLateral;
                        }

                        // Rotation
                        if (mRotationAcceleration > thresholdRotation ||
                            mVerticalRotationAcceleration > thresholdRotation) {
                            // This is different from how JPF did it, as I'm trying to not alter the euler
                            // angles directly per Unity docs, doing it instead via a vector.
                            // Example for clamps: Euler.x is 50f
                            // min clamp: 20 - 50 => -30
                            // max clamp: 87 - 50 =>  37
                            float minClamp = MinRotationalElevation - transform.eulerAngles.x;
                            float maxClamp = MaxRotationalElevation - transform.eulerAngles.x;
                            float xDelta = -(mVerticalRotationAcceleration * timeStep * FactorSpeedRotation);
                            float yDelta = mRotationAcceleration * timeStep * FactorSpeedRotation;

                            // Constrain elevation adjustment
                            xDelta = Mathf.Clamp(xDelta, minClamp, maxClamp);

                            transform.eulerAngles += new Vector3(xDelta, yDelta);
                        }
                    } else if (yAxisAccelAbsolute > thresholdMovementY ) {
                        //float speed = Mathf.Clamp(FactorSpeedZoom * timeStep, MinSpeedLinear, MaxSpeedLinear);
                        //Vector3 movement = transform.forward;
                        //
                    }
                }
            }
        }

        public void fixedUpdate(float timeStep, int frameIndex)
        {

        }
    }


    [HarmonyPatch(typeof(CameraManager), "update")]
    public class PatchCamera
    {
        public static bool Prefix(float timeStep)
        {
            // We call this here instead of in CameraOverhaul.Init because this ensures that it's loaded
            // lazily; most importantly it ensures that the game is READY to instantiate it.
            if (CameraOverhaul.camera is null) {
                CameraOverhaul.camera = new CustomCameraManager();
            }
            CameraOverhaul.camera.update(timeStep);
            return true;
        }
    }
}
