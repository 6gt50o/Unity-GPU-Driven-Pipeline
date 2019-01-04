﻿using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace MPipeline
{
    public unsafe class RenderPipeline : MonoBehaviour
    {
        #region STATIC_AREA
        public enum CameraRenderingPath
        {
            Unlit, Forward, GPUDeferred
        }
        public static RenderPipeline current;
        public static PipelineCommandData data;
        public static Dictionary<CameraRenderingPath, DrawEvent> allDrawEvents = new Dictionary<CameraRenderingPath, DrawEvent>();
        #endregion
        public GameObject pipelinePrefab;
        private PipelineEvent[] allEvents;
        public PipelineResources resources;
        public SceneController sceneController;
        private void Awake()
        {
            if (current == this) return;
            if (current)
            {
                Debug.LogError("Render Pipeline should be Singleton!");
                DestroyImmediate(gameObject);
                return;
            }
            data.buffer = new CommandBuffer();
            DontDestroyOnLoad(this);
            current = this;
            data.frustumPlanes = new Vector4[6];
            allEvents = pipelinePrefab.GetComponentsInChildren<PipelineEvent>();
            foreach (var i in allEvents)
                i.InitEvent(resources);
            sceneController.Awake(this);
        }

        private void Update()
        {
            lock (SceneController.current.commandQueue)
            {
                SceneController.current.commandQueue.Run();
            }
            sceneController.Update();
        }

        private void OnDestroy()
        {
            if (current != this) return;
            current = null;
            foreach (var i in allEvents)
                i.DisposeEvent();
            allEvents = null;
            data.buffer.Dispose();
            sceneController.OnDestroy();
            PipelineSharedData.DisposeAll();
        }

        public void Render(CameraRenderingPath path, PipelineCamera pipelineCam, RenderTargetIdentifier dest, ref ScriptableRenderContext context, ref CullResults cullResults)
        {
            //Set Global Data
            Camera cam = pipelineCam.cam;
            data.context = context;
            data.cullResults = cullResults;
            PipelineFunctions.InitRenderTarget(ref pipelineCam.targets, cam, pipelineCam.temporalRT, data.buffer);
            data.resources = resources;
            PipelineFunctions.GetViewProjectMatrix(cam, out data.vp, out data.inverseVP);
            PipelineFunctions.GetCullingPlanes(ref data.inverseVP, data.frustumPlanes.Ptr(), cam.nearClipPlane, cam.farClipPlane, cam.transform.position, cam.transform.forward);
            DrawEvent evt;
            PipelineFunctions.ExecuteCommandBuffer(ref data);
            if (allDrawEvents.TryGetValue(path, out evt))
            {
                //Pre Calculate Events
                foreach (var i in evt.preRenderEvents)
                {
                    i.PreRenderFrame(pipelineCam, ref data);
                }
                //Run job system together
                JobHandle.ScheduleBatchedJobs();
                //Start Prepare Render Targets
                //Frame Update Events
                foreach (var i in evt.drawEvents)
                {
                    i.FrameUpdate(pipelineCam, ref data);
                }
            }
            data.buffer.Blit(pipelineCam.targets.renderTargetIdentifier, dest);
            PipelineFunctions.ExecuteCommandBuffer(ref data);
        }
    }
    public struct DrawEvent
    {
        public List<PipelineEvent> drawEvents;
        public List<PipelineEvent> preRenderEvents;
        public DrawEvent(int capacity)
        {
            drawEvents = new List<PipelineEvent>(capacity);
            preRenderEvents = new List<PipelineEvent>(capacity);
        }
    }
}