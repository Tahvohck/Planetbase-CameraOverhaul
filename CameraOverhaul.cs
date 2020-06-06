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
        protected static float mAlternateRotationAcceleration = 0f;

        protected static Plane GroundPlane = new Plane(
            Vector3.up,
            new Vector3(TGenTotalSize, 0f, TGenTotalSize) * 0.5f);

        protected static int Modulesize = 0;
        protected static bool IsPlacingModule = false;
        private static float ZoomAxis;

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
        protected static readonly float FactorSpeedLateralFU = 6f;
        protected static readonly float FactorSpeedRotAndZoomFU = 10f;
        protected static readonly float FactorSpeedRotation = 120f;
        protected static readonly float MinRotationalElevation = 20f;
        protected static readonly float MaxRotationalElevation = 87f;
        protected static readonly float MaxDistFromMapCenter = Mathf.Min(375f, TGenTotalSize / 2.0f);

        // Fields that are private in CameraManager
        protected static Vector3 mAcceleration = Vector3.zero;  // TODO: Update assignment
        protected static float mCurrentHeight;                  // TODO: Update assignment
        protected static float mTargetHeight;                   // TODO: Might not need this
        protected static float mRotationAcceleration;           // TODO: Get from somewhere
        protected static float mVerticalRotationAcceleration;   // TODO: Get from somewhere
        protected static float mZoomAxis = 0f;                  // Only altered by Update and FixedUpdate
        protected static float mPreviousMouseX = 0f;            // Only altered by FixedUpdate
        protected static float mPreviousMouseY = 0f;            // Only altered by FixedUpdate
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
            // Only run if camera is NOT fixed and if gamestate is NOT null
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

                    #region mLocked else if absY > threshold
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
                        // TODO: Understand this whole segment
                        float speed = Mathf.Clamp(FactorSpeedZoom * timeStep, MinSpeedLinear, MaxSpeedLinear);
                        Vector3 movement = transform.forward * speed * -yAxisAccel;

                        Construction selected = Selection.getSelectedConstruction();
                        Vector3 planePoint = selected.getPosition();
                        planePoint.y = yAxisAccel < 0f ? 4f : selected.getRadius() + 10f;
                        Plane plane = new Plane(Vector3.up, planePoint);

                        // Ray goes forward if accel is negative, otherwise it goes backwards???
                        Ray ray = new Ray(
                            transform.position,
                            yAxisAccel < 0f ? transform.forward : -transform.forward);
                        float dist;
                        if (plane.Raycast(ray, out dist)) {
                            if (dist < movement.magnitude) {
                                movement *= dist / movement.magnitude;
                            }

                            transform.position += movement;
                        }
                    }
                    #endregion

                    // Rotate around world
                    if (Mathf.Abs(mAlternateRotationAcceleration) > thresholdRotation) {
                        Ray ray = new Ray(transform.position, transform.forward);
                        float dist;
                        if (GroundPlane.Raycast(ray, out dist)) {
                            transform.RotateAround(
                                transform.position + transform.forward * dist,
                                Vector3.up,
                                mAlternateRotationAcceleration * timeStep * FactorSpeedRotation);
                        }
                    }

                    // If we moved, set the correct height
                    if (!mLocked && (
                        zAxisAccelAbsolute > thresholdMovementXZ ||
                        xAxisAccelAbsolute > thresholdMovementXZ ||
                        yAxisAccelAbsolute > thresholdMovementY)) {
                        _Manager.placeOnFloor(mCurrentHeight);
                    }

                    // Calc map center and distance
                    Vector3 mapCenter = new Vector3(TGenTotalSize, 0f, TGenTotalSize) * 0.5f;
                    Vector3 mapCenterToCam = transform.position - mapCenter;
                    float distToMapCenter = mapCenterToCam.magnitude;

                    // limit camera bounds on map
                    if (distToMapCenter > MaxDistFromMapCenter) {
                        transform.position = mapCenter + mapCenterToCam.normalized * MaxDistFromMapCenter;
                    }
                }
            }

            // TODO: Interpolation, will need reflection
        }

        public void fixedUpdate(float timeStep, int frameIndex)
        {
            if (_Manager.getCinematic() is null) {
                float lateralMoveSpeed = timeStep * FactorSpeedLateralFU;
                float zoomAndRotationSpeed = timeStep * FactorSpeedRotAndZoomFU;

                GameState gameState = GameManager.getInstance().getGameState();

                // This only happens when placing a module and only if the current height is < 21
                if (mTargetHeight != mCurrentHeight) {
                    // TODO: Break 30f out into a constant?
                    mCurrentHeight += Mathf.Sign(mTargetHeight - mCurrentHeight) * timeStep * 30f;
                    if (Mathf.Abs(mCurrentHeight - mTargetHeight) < 0.5f) {
                        mCurrentHeight = mTargetHeight;
                    }
                }

                // Camera is unfixed and game isn't paused.
                if (!(gameState?.isCameraFixed() ?? true) && !TimeManager.getInstance().isPaused()) {
                    // Setup
                    KeyBindingManager bindingManager = KeyBindingManager.getInstance();
                    GameStateGame game = gameState as GameStateGame;
                    float axisCompositeZoom = bindingManager.getCompositeAxis(
                        ActionType.CameraZoomOut, ActionType.CameraZoomIn);
                    float axisCompositeLR = bindingManager.getCompositeAxis(
                        ActionType.CameraMoveLeft, ActionType.CameraMoveRight);
                    float axisCompositeFB = bindingManager.getCompositeAxis(
                        ActionType.CameraMoveBack, ActionType.CameraMoveForward);
                    bool ctrlButtonPressed =
                        Input.GetKey(KeyCode.LeftControl) &&
                        Input.GetKey(KeyCode.RightControl);

                    // Runs if we're placing a module
                    if (game.CurrentState() == GameStateHelper.Mode.PlacingModule) {
                        if (!IsPlacingModule) {
                            IsPlacingModule = true;
                            // TODO: Get this, maybe. See above in update()
                            // Modulesize = game.mCurrentModuleSize;
                        }

                        // Size adjustment code (JPF called this zoom code)
                        if (Mathf.Abs(mZoomAxis) > thresholdMovementXZ
                            || Math.Abs(axisCompositeZoom) > thresholdMovementXZ) {
                            // Adjust module size as needed IF the control keys are down.
                            if (ctrlButtonPressed) {
                                // This does not match JPF's code. In a way it's a bit more elegant, but in
                                // a different way it's a hack.
                                int sizeAdjust = (int)Mathf.Sign(axisCompositeZoom);
                                bool justUp = bindingManager.getBinding(ActionType.CameraZoomOut).justUp()
                                    && bindingManager.getBinding(ActionType.CameraZoomIn).justUp();
                                if (justUp && Modulesize >= 0 && Modulesize <= 4) {
                                    Modulesize += sizeAdjust;
                                }
                            }
                        }

                        // game.mCurrentModuleSize = Modulesize
                    } else {
                        // TODO: Maybe I don't need this. I should be able to trust the gamestate instead.
                        IsPlacingModule = false;
                    }

                    mAcceleration.x += axisCompositeLR * lateralMoveSpeed;
                    mAcceleration.z += axisCompositeFB * lateralMoveSpeed;

                    if (!ctrlButtonPressed) {
                        mAcceleration.y -= mZoomAxis * zoomAndRotationSpeed;
                        mAcceleration.y -= axisCompositeZoom * zoomAndRotationSpeed;
                    }

                    mAlternateRotationAcceleration -= axisCompositeLR * zoomAndRotationSpeed;

                    // Rotate with middle mouse button
                    // TODO: Can probably use a Vector2 here
                    if (Input.GetMouseButton(2)) {
                        float mouseDeltaX = Input.mousePosition.x - mPreviousMouseX;
                        float mouseDeltaY = Input.mousePosition.y - mPreviousMouseY;

                        if (Mathf.Abs(mouseDeltaX) != Mathf.Epsilon) {
                            mRotationAcceleration += zoomAndRotationSpeed * mouseDeltaX * 0.1f;
                        }
                        if (Mathf.Abs(mouseDeltaY) != Mathf.Epsilon) {
                            mVerticalRotationAcceleration += zoomAndRotationSpeed * mouseDeltaY * 0.1f;
                        }
                    }

                    // Move with mouse on screen borders
                    if (!Application.isEditor) {
                        float screenBorderWidth = Screen.height * 0.01f;
                        if (Input.mousePosition.x < screenBorderWidth) {
                            mAcceleration.x -= lateralMoveSpeed;
                        } else if (Input.mousePosition.x > Screen.width - screenBorderWidth) {
                            mAcceleration.x += lateralMoveSpeed;
                        } else if (Input.mousePosition.y < screenBorderWidth) {
                            mAcceleration.z -= lateralMoveSpeed;
                        } else if (Input.mousePosition.y > Screen.height - screenBorderWidth) {
                            mAcceleration.z += lateralMoveSpeed;
                        }
                    }

                    // Unlike JPF, I define a reusable function here because it reads better.
                    float clampSpeed = !Input.GetKey(KeyCode.LeftShift) ? 1f : 0.25f;
                    Clamp(ref mAcceleration.x, lateralMoveSpeed);
                    Clamp(ref mAcceleration.z, lateralMoveSpeed);
                    Clamp(ref mAcceleration.y, zoomAndRotationSpeed);
                    Clamp(ref mRotationAcceleration, zoomAndRotationSpeed);
                    Clamp(ref mVerticalRotationAcceleration, zoomAndRotationSpeed);
                    Clamp(ref mAlternateRotationAcceleration, zoomAndRotationSpeed);

                    // Said reusable function
                    void Clamp(ref float num, float mult)
                    {
                        num = Mathf.Clamp(num - num * mult, -clampSpeed, clampSpeed);
                    }
                } else {
                    mAcceleration = Vector3.zero;
                    mRotationAcceleration = 0f;
                    mVerticalRotationAcceleration = 0f;
                    mAlternateRotationAcceleration = 0f;
                }

                mPreviousMouseX = Input.mousePosition.x;
                mPreviousMouseY = Input.mousePosition.y;
            }
        }
    }


    [HarmonyPatch(typeof(CameraManager), "update")]
    public class PatchCameraUpdate
    {
        public static bool Prefix(float timeStep)
        {
            // We call this here instead of in CameraOverhaul.Init because this ensures that it's loaded
            // lazily; most importantly it ensures that the game is READY to instantiate it.
            if (CameraOverhaul.camera is null) {
                CameraOverhaul.camera = new CustomCameraManager();
            }
            CameraOverhaul.camera.update(timeStep);
            return false;
        }
    }


    [HarmonyPatch(typeof(CameraManager), "fixedUpdate")]
    public class PatchCameraFixedUpdate
    {
        public static bool Prefix(float timeStep, int frameIndex)
        {
            // We call this here instead of in CameraOverhaul.Init because this ensures that it's loaded
            // lazily; most importantly it ensures that the game is READY to instantiate it.
            if (CameraOverhaul.camera is null) {
                CameraOverhaul.camera = new CustomCameraManager();
            }
            CameraOverhaul.camera.fixedUpdate(timeStep, frameIndex);
            return false;
        }
    }
}
