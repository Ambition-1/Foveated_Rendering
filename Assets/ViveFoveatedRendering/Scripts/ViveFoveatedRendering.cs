//========= Copyright 2020, HTC Corporation. All rights reserved. ===========
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using System;
using ViveSR.anipal.Eye;



namespace HTC.UnityPlugin.FoveatedRendering
{
    public static class FoveatedRenderingExtensions
    {
        public static T Clamp<T>(this T input, T min, T max) where T : IComparable
        {
            if (min.CompareTo(input) > 0)
            { return min; }
            else if (max.CompareTo(input) < 0)
            { return max; }

            return input;
        }
    }

    [RequireComponent(typeof(Camera))]
    public class ViveFoveatedRendering : MonoBehaviour
    {
        Camera thisCamera = null;
        CommandBufferManager commandBufferMgr = new CommandBufferManager();

        bool foveatedRenderingInited = false;
        bool foveatedRenderingActivated = false;
        RenderMode renderMode = RenderMode.RENDER_MODE_MONO;

        [SerializeField]
        bool manualAdjustment = false;

        [SerializeField]
        ShadingRatePreset shadingRatePreset = ShadingRatePreset.SHADING_RATE_HIGHEST_PERFORMANCE;
        [SerializeField]
        ShadingPatternPreset patternPreset = ShadingPatternPreset.SHADING_PATTERN_NARROW;

        [SerializeField]
        Vector2 innerRegionRadii = new Vector2(0.25f, 0.25f);
        [SerializeField]
        Vector2 middleRegionRadii = new Vector2(0.33f, 0.33f);
        [SerializeField]
        Vector2 peripheralRegionRadii = new Vector2(1.0f, 1.0f);

        [SerializeField]
        ShadingRate innerShadingRate = ShadingRate.X1_PER_PIXEL;
        [SerializeField]
        ShadingRate middleShadingRate = ShadingRate.X1_PER_2X2_PIXELS;
        [SerializeField]
        ShadingRate peripheralShadingRate = ShadingRate.X1_PER_4X4_PIXELS;
        //此处为增加的变量
        private static EyeData_v2 eyeData = new EyeData_v2();
        private bool eye_callback_registered = false;
        private float pupilDiameterLeft, pupilDiameterRight;
        private Vector2 pupilPositionLeft, pupilPositionRight;
        private float eyeOpenLeft, eyeOpenRight;
        // ===== 新增：日志频率控制计数器 =====
        private int logFrameCounter = 0; // 帧计数器，用于控制日志输出间隔
        // ===== 新增结束 =====

        public void EnableFoveatedRendering(bool activate)
        {
            if (foveatedRenderingInited && activate != foveatedRenderingActivated)
            {
                foveatedRenderingActivated = activate;
                if (activate)
                {
                    commandBufferMgr.EnableCommands(thisCamera);
                }
                else
                {
                    commandBufferMgr.DisableCommands(thisCamera);
                }
            }
        }

        public void SetShadingRatePreset(ShadingRatePreset inputPreset)
        {
            if (foveatedRenderingInited)
            {
                shadingRatePreset = inputPreset.Clamp(ShadingRatePreset.SHADING_RATE_HIGHEST_PERFORMANCE, ShadingRatePreset.SHADING_RATE_MAX);
                ViveFoveatedRenderingAPI.SetFoveatedRenderingShadingRatePreset(shadingRatePreset);

                if (shadingRatePreset == ShadingRatePreset.SHADING_RATE_CUSTOM)
                {
                    SetShadingRate(TargetArea.INNER, innerShadingRate);
                    SetShadingRate(TargetArea.MIDDLE, middleShadingRate);
                    SetShadingRate(TargetArea.PERIPHERAL, peripheralShadingRate);
                }

                GL.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.UPDATE_GAZE);
            }
        }
        private static void EyeCallback(ref EyeData_v2 eye_data)
        {
            eyeData = eye_data; // 将 SDK 实时数据写入静态变量 eyeData
        }

        public ShadingRatePreset GetShadingRatePreset()
        {
            return shadingRatePreset;
        }

        public void SetPatternPreset(ShadingPatternPreset inputPreset)
        {
            if (foveatedRenderingInited)
            {
                patternPreset = inputPreset.Clamp(ShadingPatternPreset.SHADING_PATTERN_WIDE, ShadingPatternPreset.SHADING_PATTERN_MAX);
                ViveFoveatedRenderingAPI.SetFoveatedRenderingPatternPreset(patternPreset);

                if (patternPreset == ShadingPatternPreset.SHADING_PATTERN_CUSTOM)
                {
                    SetRegionRadii(TargetArea.INNER, innerRegionRadii);
                    SetRegionRadii(TargetArea.MIDDLE, middleRegionRadii);
                    SetRegionRadii(TargetArea.PERIPHERAL, peripheralRegionRadii);
                }

                GL.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.UPDATE_GAZE);
            }
        }

        public ShadingPatternPreset GetPatternPreset()
        {
            return patternPreset;
        }

        public void SetShadingRate(TargetArea targetArea, ShadingRate rate)
        {
            if (foveatedRenderingInited)
            {
                var clampedRate = rate.Clamp(ShadingRate.CULL, ShadingRate.X1_PER_4X4_PIXELS);
                switch (targetArea)
                {
                    case TargetArea.INNER:
                        innerShadingRate = clampedRate;
                        break;
                    case TargetArea.MIDDLE:
                        middleShadingRate = clampedRate;
                        break;
                    case TargetArea.PERIPHERAL:
                        peripheralShadingRate = clampedRate;
                        break;
                }

                ViveFoveatedRenderingAPI.SetShadingRate(targetArea, clampedRate);
                GL.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.UPDATE_GAZE);
            }
        }

        public ShadingRate GetShadingRate(TargetArea targetArea)
        {
            switch (targetArea)
            {
                case TargetArea.INNER:
                    return innerShadingRate;
                case TargetArea.MIDDLE:
                    return middleShadingRate;
                case TargetArea.PERIPHERAL:
                    return peripheralShadingRate;
            }

            return ShadingRate.CULL;
        }

        public void SetRegionRadii(TargetArea targetArea, Vector2 radii)
        {
            if (foveatedRenderingInited)
            {
                var clampedRadii = new Vector2(radii.x.Clamp(0.01f, 10.0f), radii.y.Clamp(0.01f, 10.0f));
                switch (targetArea)
                {
                    case TargetArea.INNER:
                        innerRegionRadii = clampedRadii;
                        break;
                    case TargetArea.MIDDLE:
                        middleRegionRadii = clampedRadii;
                        break;
                    case TargetArea.PERIPHERAL:
                        peripheralRegionRadii = clampedRadii;
                        break;
                }

                ViveFoveatedRenderingAPI.SetRegionRadii(targetArea, clampedRadii);
                GL.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.UPDATE_GAZE);
            }
        }

        public Vector2 GetRegionRadii(TargetArea targetArea)
        {
            switch (targetArea)
            {
                case TargetArea.INNER:
                    return innerRegionRadii;
                case TargetArea.MIDDLE:
                    return middleRegionRadii;
                case TargetArea.PERIPHERAL:
                    return peripheralRegionRadii;
            }

            return Vector2.zero;
        }

        void OnEnable()
        {
            ViveFoveatedRenderingAPI.InitializeNativeLogger(str => Debug.Log(str));

            thisCamera = GetComponent<Camera>();
            foveatedRenderingInited = ViveFoveatedRenderingAPI.InitializeFoveatedRendering(thisCamera.fieldOfView, thisCamera.aspect);
            if (foveatedRenderingInited)
            {
                var currentRenderingPath = thisCamera.actualRenderingPath;
                if (currentRenderingPath == RenderingPath.Forward)
                {
                    commandBufferMgr.AppendCommands("Enable Foveated Rendering", CameraEvent.BeforeForwardOpaque,
                        buf => buf.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.ENABLE_FOVEATED_RENDERING),
                        buf => buf.ClearRenderTarget(false, true, Color.black));

                    commandBufferMgr.AppendCommands("Disable Foveated Rendering", CameraEvent.AfterForwardAlpha,
                        buf => buf.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.DISABLE_FOVEATED_RENDERING));
                }
                else if (currentRenderingPath == RenderingPath.DeferredShading)
                {
                    commandBufferMgr.AppendCommands("Enable Foveated Rendering - GBuffer", CameraEvent.BeforeGBuffer,
                        buf => buf.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.ENABLE_FOVEATED_RENDERING),
                        buf => buf.ClearRenderTarget(false, true, Color.black));

                    commandBufferMgr.AppendCommands("Disable Foveated Rendering - GBuffer", CameraEvent.AfterGBuffer,
                        buf => buf.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.DISABLE_FOVEATED_RENDERING));

                    commandBufferMgr.AppendCommands("Enable Foveated Rendering - Alpha", CameraEvent.BeforeForwardAlpha,
                        buf => buf.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.ENABLE_FOVEATED_RENDERING));

                    commandBufferMgr.AppendCommands("Disable Foveated Rendering - Alpha", CameraEvent.AfterForwardAlpha,
                        buf => buf.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.DISABLE_FOVEATED_RENDERING));
                }

                EnableFoveatedRendering(true);
                bool isEyeTracked = ViveFoveatedGazeUpdater.AttachGazeUpdater(gameObject);

                if (manualAdjustment || (!ViveFoveatedInitParam.SetParamByHMD(this, isEyeTracked)))
                {
                    SetShadingRatePreset(shadingRatePreset);
                    SetPatternPreset(patternPreset);
                }
                
                //固定注视点渲染
               // ViveFoveatedRenderingAPI.SetNormalizedGazeDirection(new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, 0.0f, 1.0f));
                //GL.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.UPDATE_GAZE);
            }
        }
        
        private void Update()
            {
                if (SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING &&
                    SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.NOT_SUPPORT) return;

                if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback == true && eye_callback_registered == false)
                {
                    SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
                    eye_callback_registered = true;
                }
                else if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback == false && eye_callback_registered == true)
                {
                    SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
                    eye_callback_registered = false;
                }

                Vector3 GazeOriginCombinedLocal, GazeDirectionCombinedLocal;

                if (eye_callback_registered)
                {
                    if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal, eyeData)) { }
                    else if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.LEFT, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal, eyeData)) { }
                    else if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.RIGHT, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal, eyeData)) { }
                    else return;
                }
                else
                {
                    if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
                    else if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.LEFT, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
                    else if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.RIGHT, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
                    else return;
                }

                //以下为新增的部分
                //pupil diameter 瞳孔的直径
                pupilDiameterLeft = eyeData.verbose_data.left.pupil_diameter_mm;
                pupilDiameterRight = eyeData.verbose_data.right.pupil_diameter_mm;
                //pupil positions 瞳孔位置
                //pupil_position_in_sensor_area手册里写的是The normalized position of a pupil in [0,1]，给坐标归一化了
                pupilPositionLeft = eyeData.verbose_data.left.pupil_position_in_sensor_area;
                pupilPositionRight = eyeData.verbose_data.right.pupil_position_in_sensor_area;
                //pupilPositioncombined = eyeData.verbose_data.combined.convergence_point_in_sensor_area;
                //eye open 睁眼
                //eye_openness手册里写的是A value representing how open the eye is,也就是睁眼程度，从输出来看是在0-1之间，也归一化了
                eyeOpenLeft = eyeData.verbose_data.left.eye_openness;
                eyeOpenRight = eyeData.verbose_data.right.eye_openness;


                // ===== 新增：提取实时视线方向并更新注视点 =====
                // 从 eyeData 中获取左右眼归一化视线方向（需根据 SDK 字段名调整，此处为通用示例）
                Vector3 leftGazeDir = eyeData.verbose_data.left.gaze_direction_normalized; // 左眼视线方向
                Vector3 rightGazeDir = eyeData.verbose_data.right.gaze_direction_normalized; // 右眼视线方向

                // 新增：闭眼检测 - 低于阈值时使用固定注视点
                /*float eyeClosedThreshold = 0.1f; // 闭眼判断阈值（0-1范围，可根据实际需求调整）
                if (eyeOpenLeft < eyeClosedThreshold || eyeOpenRight < eyeClosedThreshold)
                {
                    leftGazeDir = new Vector3(0.0f, 0.0f, 1.0f);  // 固定左眼注视方向
                    rightGazeDir = new Vector3(0.0f, 0.0f, 1.0f); // 固定右眼注视方向
                }
                // 新增：修正Y轴方向（上下反转问题）
                leftGazeDir = new Vector3(leftGazeDir.x, -leftGazeDir.y, leftGazeDir.z);
                rightGazeDir = new Vector3(rightGazeDir.x, -rightGazeDir.y, rightGazeDir.z);

                // 确保方向向量归一化（避免长度异常导致渲染错误）
                leftGazeDir = leftGazeDir.normalized;
                rightGazeDir = rightGazeDir.normalized;

                logFrameCounter++;
                if (logFrameCounter >= 30)
                {
                    logFrameCounter = 0; // 重置计数器
                    Debug.Log("左眼瞳孔直径：" + pupilDiameterLeft + " 左眼位置坐标：" + pupilPositionLeft + "左眼睁眼程度" + eyeOpenLeft);
                    Debug.Log("右眼瞳孔直径：" + pupilDiameterRight + " 右眼位置坐标：" + pupilPositionRight + " 左眼睁眼程度" + eyeOpenRight);
                    Debug.Log("左眼归一化方向：" + leftGazeDir + " 右眼归一化方向：" + rightGazeDir);
                }*/
              
                // 动态更新注视点渲染（替换固定向量）
               // ViveFoveatedRenderingAPI.SetNormalizedGazeDirection(leftGazeDir, rightGazeDir);
               // GL.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.UPDATE_GAZE);
            //固定注视点渲染
             ViveFoveatedRenderingAPI.SetNormalizedGazeDirection(new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, 0.0f, 1.0f));
            GL.IssuePluginEvent(ViveFoveatedRenderingAPI.GetRenderEventFunc(), (int)EventID.UPDATE_GAZE);

        }


        void OnDisable()
        {
            EnableFoveatedRendering(false);
            commandBufferMgr.ClearCommands();

            ViveFoveatedRenderingAPI.ReleaseFoveatedRendering();
            ViveFoveatedRenderingAPI.ReleaseNativeLogger();

            foveatedRenderingInited = false;

            var gazeUpdater = GetComponent<ViveFoveatedGazeUpdater>();
            if (gazeUpdater != null)
            {
                gazeUpdater.enabled = false;
            }
            if (eye_callback_registered == true)
                {
                    SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
                    eye_callback_registered = false;
                }
        }

        void OnPreRender()
        {
            if (XRSettings.enabled)
            {
                switch (XRSettings.stereoRenderingMode)
                {
                    case XRSettings.StereoRenderingMode.MultiPass:
                        renderMode = thisCamera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left ?
                        RenderMode.RENDER_MODE_LEFT_EYE : RenderMode.RENDER_MODE_RIGHT_EYE;
                        break;
                    case XRSettings.StereoRenderingMode.SinglePass:
                        renderMode = RenderMode.RENDER_MODE_STEREO;
                        break;
                    default:
                        renderMode = RenderMode.RENDER_MODE_MONO;
                        break;
                }
            }
            else
            {
                renderMode = RenderMode.RENDER_MODE_MONO;
            }

            ViveFoveatedRenderingAPI.SetRenderMode(renderMode);
        }
    }
}

