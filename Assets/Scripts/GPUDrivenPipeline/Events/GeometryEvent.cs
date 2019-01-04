﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public unsafe class GeometryEvent : PipelineEvent
    {
        HizDepth hizDepth;
        Material linearMat;
        public enum OcclusionCullingMode
        {
            None, SingleCheck, DoubleCheck
        }
        public OcclusionCullingMode occCullingMod = OcclusionCullingMode.None;
        protected override void Init(PipelineResources resources)
        {
            hizDepth = new HizDepth();
            hizDepth.InitHiZ(resources);
            linearMat = new Material(resources.linearDepthShader);
            Application.targetFrameRate = int.MaxValue;
        }

        protected override void Dispose()
        {
            hizDepth.DisposeHiZ();
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthIdentifier);
            buffer.ClearRenderTarget(true, true, Color.black);
            PipelineBaseBuffer baseBuffer;
            bool isClusterEnabled = SceneController.GetBaseBuffer(out baseBuffer);
            HizOcclusionData hizData = IPerCameraData.GetProperty(cam, () => new HizOcclusionData());
            RenderClusterOptions options = new RenderClusterOptions
            {
                command = buffer,
                frustumPlanes = data.frustumPlanes,
                isOrtho = cam.cam.orthographic,
                cullingShader = data.resources.gpuFrustumCulling,
                terrainCompute = data.resources.terrainCompute,
                isClusterEnabled = isClusterEnabled,
                isTerrainEnabled = true
            };
            HizOptions hizOptions;
            switch (occCullingMod)
            {
                case OcclusionCullingMode.None:
                    SceneController.current.DrawCluster(ref options, ref cam.targets);
                    break;
                case OcclusionCullingMode.SingleCheck:
                    hizOptions = new HizOptions
                    {
                        currentCameraUpVec = cam.cam.transform.up,
                        hizData = hizData,
                        hizDepth = hizDepth,
                        linearLODMaterial = linearMat,
                        currentDepthTex = cam.targets.depthTexture
                    };
                    SceneController.current.DrawClusterOccSingleCheck(ref options, ref hizOptions, ref cam.targets);
                    break;
                case OcclusionCullingMode.DoubleCheck:
                    hizOptions = new HizOptions
                    {
                        currentCameraUpVec = cam.cam.transform.up,
                        hizData = hizData,
                        hizDepth = hizDepth,
                        linearLODMaterial = linearMat,
                        currentDepthTex = cam.targets.depthTexture
                    };
                    SceneController.current.DrawClusterOccDoubleCheck(ref options, ref hizOptions, ref cam.targets);
                    break;
            }
            data.ExecuteCommandBuffer();
        }
    }
    public class HizOcclusionData : IPerCameraData
    {
        public Vector3 lastFrameCameraUp = Vector3.up;
        public RenderTexture historyDepth;
        public HizOcclusionData()
        {
            historyDepth = new RenderTexture(512, 256, 0, RenderTextureFormat.RHalf);
            historyDepth.useMipMap = true;
            historyDepth.autoGenerateMips = false;
            historyDepth.filterMode = FilterMode.Point;
            historyDepth.wrapMode = TextureWrapMode.Clamp;
        }
        public override void DisposeProperty()
        {
            historyDepth.Release();
            Object.Destroy(historyDepth);
        }
    }
}