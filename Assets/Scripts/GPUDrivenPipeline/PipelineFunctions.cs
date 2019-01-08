﻿using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Text;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using MPipeline;

public unsafe static class PipelineFunctions
{
    public static void GetCullingPlanes(ref Matrix4x4 invVp, Vector4* cullingPlanes)
    {
        Vector3 nearLeftButtom = invVp.MultiplyPoint(new Vector3(-1, -1, 1));
        Vector3 nearLeftTop = invVp.MultiplyPoint(new Vector3(-1, 1, 1));
        Vector3 nearRightButtom = invVp.MultiplyPoint(new Vector3(1, -1, 1));
        Vector3 nearRightTop = invVp.MultiplyPoint(new Vector3(1, 1, 1));
        Vector3 farLeftButtom = invVp.MultiplyPoint(new Vector3(-1, -1, 0));
        Vector3 farRightTop = invVp.MultiplyPoint(new Vector3(1, 1, 0));
        Vector3 farRightButtom = invVp.MultiplyPoint(new Vector3(1, -1, 0));
        Plane plane;
        //Far
        plane = new Plane(farLeftButtom, farRightButtom, farRightTop);
        cullingPlanes[0] = plane.normal;
        cullingPlanes[0].w = plane.distance;
        //Up
        plane = new Plane(nearLeftTop, farRightTop, nearRightTop);
        cullingPlanes[1] = plane.normal;
        cullingPlanes[1].w = plane.distance;
        //Down
        plane = new Plane(nearRightButtom, farLeftButtom, nearLeftButtom);
        cullingPlanes[2] = plane.normal;
        cullingPlanes[2].w = plane.distance;
        //Left
        plane = new Plane(farLeftButtom, nearLeftTop, nearLeftButtom);
        cullingPlanes[3] = plane.normal;
        cullingPlanes[3].w = plane.distance;
        //Right
        plane = new Plane(farRightTop, nearRightButtom, nearRightTop);
        cullingPlanes[4] = plane.normal;
        cullingPlanes[4].w = plane.distance;
        //Near
        plane = new Plane(nearRightTop, nearRightButtom, nearLeftButtom);
        cullingPlanes[5] = plane.normal;
        cullingPlanes[5].w = plane.distance;

    }

    public static void GetCullingPlanes(ref Matrix4x4 invVp, Vector4* cullingPlanes, float nearClip, float farClip, Vector3 cameraPosition, Vector3 cameraForward)
    {
        Vector3 nearLeftButtom = invVp.MultiplyPoint(new Vector3(-1, -1, 1));
        Vector3 nearLeftTop = invVp.MultiplyPoint(new Vector3(-1, 1, 1));
        Vector3 nearRightButtom = invVp.MultiplyPoint(new Vector3(1, -1, 1));
        Vector3 nearRightTop = invVp.MultiplyPoint(new Vector3(1, 1, 1));
        Vector3 farLeftButtom = invVp.MultiplyPoint(new Vector3(-1, -1, 0.5f));
        Vector3 farRightTop = invVp.MultiplyPoint(new Vector3(1, 1, 0.5f));
        Plane plane;
        //Far
        plane = new Plane(cameraForward, cameraPosition + cameraForward * farClip);
        cullingPlanes[0] = cameraForward;
        cullingPlanes[0].w = plane.distance;
        //Up
        plane = new Plane(nearLeftTop, farRightTop, nearRightTop);
        cullingPlanes[1] = plane.normal;
        cullingPlanes[1].w = plane.distance;
        //Down
        plane = new Plane(nearRightButtom, farLeftButtom, nearLeftButtom);
        cullingPlanes[2] = plane.normal;
        cullingPlanes[2].w = plane.distance;
        //Left
        plane = new Plane(farLeftButtom, nearLeftTop, nearLeftButtom);
        cullingPlanes[3] = plane.normal;
        cullingPlanes[3].w = plane.distance;
        //Right
        plane = new Plane(farRightTop, nearRightButtom, nearRightTop);
        cullingPlanes[4] = plane.normal;
        cullingPlanes[4].w = plane.distance;
        //Near
        plane = new Plane(-cameraForward, cameraPosition + cameraForward * nearClip);
        cullingPlanes[5] = -cameraForward;
        cullingPlanes[5].w = plane.distance;

    }
    public static void GetCullingPlanesForSRP(Vector4* planes, int count = 6)
    {
        for(int i = 0; i < count; ++i)
        {
            planes[i] = -planes[i];
        }
    }

    public static void GetCullingPlanes(float nearClip, float farClip, float halfFovInRag, float4* cullingPlanes, ref Matrix4x4 localToWorld)
    {
        float4 GetPlane(float3 a, float3 b, float3 c)
        {
            float3 normal = math.normalize(math.cross(b - a, c - a));
            return new float4(normal, -math.dot(normal, a));
        }
        float3 right = (Vector3)localToWorld.GetColumn(0);
        float3 up = (Vector3)localToWorld.GetColumn(1);
        float3 forward = (Vector3)localToWorld.GetColumn(2);
        float3 position = (Vector3)localToWorld.GetColumn(3);
        float3 farPos = position + forward * farClip;
        cullingPlanes[0] = new float4(forward, -math.dot(forward, farPos));   //Far Plane
        cullingPlanes[5] = new float4(-forward, -math.dot(-forward, position + forward * nearClip)); //Near Plane
        float screenSize = Mathf.Tan(halfFovInRag * Mathf.Deg2Rad * 0.5f) * farClip;
        float3 upDir = screenSize * up;
        float3 rightDir = screenSize * right;
        float3 leftDown = farPos - upDir - rightDir;
        float3 leftUp = farPos + upDir - rightDir;
        float3 rightDown = farPos + rightDir - upDir;
        float3 rightUp = farPos + rightDir + upDir;
        cullingPlanes[1] = GetPlane(position, leftDown, leftUp);
        cullingPlanes[2] = GetPlane(position, leftUp, rightUp);
        cullingPlanes[3] = GetPlane(position, rightUp, rightDown);
        cullingPlanes[4] = GetPlane(position, rightDown, leftDown);
    }
    //TODO: Streaming Loading
    /// <summary>
    /// Initialize pipeline buffers
    /// </summary>
    /// <param name="baseBuffer"></param> pipeline base buffer
    public static void InitBaseBuffer(PipelineBaseBuffer baseBuffer, ClusterMatResources materialResources, string name, int maximumLength)
    {
        baseBuffer.clusterBuffer = new ComputeBuffer(maximumLength, sizeof(CullBox));
        baseBuffer.resultBuffer = new ComputeBuffer(maximumLength, PipelineBaseBuffer.UINTSIZE);
        baseBuffer.instanceCountBuffer = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        NativeArray<uint> instanceCountBufferValue = new NativeArray<uint>(5, Allocator.Temp);
        instanceCountBufferValue[0] = PipelineBaseBuffer.CLUSTERVERTEXCOUNT;
        baseBuffer.instanceCountBuffer.SetData(instanceCountBufferValue);
        instanceCountBufferValue.Dispose();
        baseBuffer.verticesBuffer = new ComputeBuffer(maximumLength * PipelineBaseBuffer.CLUSTERCLIPCOUNT, sizeof(Point));
        baseBuffer.clusterCount = 0;
        baseBuffer.dispatchBuffer = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        NativeArray<uint> occludedCountList = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
        occludedCountList[1] = 1;
        occludedCountList[2] = 1;
        baseBuffer.dispatchBuffer.SetData(occludedCountList);
        baseBuffer.reCheckCount = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        baseBuffer.reCheckResult = new ComputeBuffer(maximumLength, 4);
        occludedCountList[0] = PipelineBaseBuffer.CLUSTERVERTEXCOUNT;
        occludedCountList[1] = 0;
        occludedCountList[2] = 0;
        baseBuffer.reCheckCount.SetData(occludedCountList);
        occludedCountList.Dispose();
    }
    /// <summary>
    /// Get Frustum Corners
    /// </summary>
    /// <param name="distance"></param> target distance range
    /// <param name="shadMap"></param> shadowmap component
    /// <param name="mask"></param> shadowmask component
    public static void GetfrustumCorners(Vector2 distance, ref ShadowMapComponent shadMap, Camera targetCamera)
    {
        //bottom left
        shadMap.frustumCorners[0] = targetCamera.ViewportToWorldPoint(new Vector3(0, 0, distance.x));
        // bottom right
        shadMap.frustumCorners[1] = targetCamera.ViewportToWorldPoint(new Vector3(1, 0, distance.x));
        // top left
        shadMap.frustumCorners[2] = targetCamera.ViewportToWorldPoint(new Vector3(0, 1, distance.x));
        // top right
        shadMap.frustumCorners[3] = targetCamera.ViewportToWorldPoint(new Vector3(1, 1, distance.x));
        //bottom left
        shadMap.frustumCorners[4] = targetCamera.ViewportToWorldPoint(new Vector3(0, 0, distance.y));
        // bottom right
        shadMap.frustumCorners[5] = targetCamera.ViewportToWorldPoint(new Vector3(1, 0, distance.y));
        // top left
        shadMap.frustumCorners[6] = targetCamera.ViewportToWorldPoint(new Vector3(0, 1, distance.y));
        // top right
        shadMap.frustumCorners[7] = targetCamera.ViewportToWorldPoint(new Vector3(1, 1, distance.y));

    }

    public static bool FrustumCulling(ref Matrix4x4 ObjectToWorld, Vector3 extent, Vector4* frustumPlanes)
    {
        Vector3 right = new Vector3(ObjectToWorld.m00, ObjectToWorld.m10, ObjectToWorld.m20);
        Vector3 up = new Vector3(ObjectToWorld.m01, ObjectToWorld.m11, ObjectToWorld.m21);
        Vector3 forward = new Vector3(ObjectToWorld.m02, ObjectToWorld.m12, ObjectToWorld.m22);
        Vector3 position = new Vector3(ObjectToWorld.m03, ObjectToWorld.m13, ObjectToWorld.m23);
        for (int i = 0; i < 6; ++i)
        {
            ref Vector4 plane = ref frustumPlanes[i];
            Vector3 normal = new Vector3(plane.x, plane.y, plane.z);
            float distance = plane.w;
            float r = Vector3.Dot(position, normal);
            Vector3 absNormal = new Vector3(Mathf.Abs(Vector3.Dot(normal, right)), Mathf.Abs(Vector3.Dot(normal, up)), Mathf.Abs(Vector3.Dot(normal, forward)));
            float f = Vector3.Dot(absNormal, extent);
            if ((r - f) >= -distance)
                return false;
        }
        return true;
    }

    public static bool FrustumCulling(Vector3 position, float range, Vector4* frustumPlanes)
    {
        for (int i = 0; i < 5; ++i)
        {
            ref Vector4 plane = ref frustumPlanes[i];
            Vector3 normal = new Vector3(plane.x, plane.y, plane.z);
            float rayDist = Vector3.Dot(normal, position);
            rayDist += plane.w;
            if (rayDist > range)
            {
                return false;
            }
        }
        return true;
    }

    public static void SetShadowCameraPositionStaticFit(ref StaticFit fit, ref OrthoCam shadCam, int pass, Matrix4x4[] vpMatrices, out Matrix4x4 invShadowVP)
    {
        float range = 0;
        Vector3 averagePos = Vector3.zero;
        foreach (var i in fit.frustumCorners)
        {
            averagePos += i;
        }
        averagePos /= fit.frustumCorners.Length;
        foreach (var i in fit.frustumCorners)
        {
            float dist = Vector3.Distance(averagePos, i);
            if (range < dist)
            {
                range = dist;
            }
        }
        shadCam.size = range;
        float farClipPlane = fit.mainCamTrans.farClipPlane;
        Vector3 targetPosition = averagePos - shadCam.forward * farClipPlane * 0.5f;
        shadCam.nearClipPlane = 0;
        shadCam.farClipPlane = farClipPlane;
        ref Matrix4x4 shadowVP = ref vpMatrices[pass];
        invShadowVP = shadowVP.inverse;
        Vector3 ndcPos = shadowVP.MultiplyPoint(targetPosition);
        Vector2 uv = new Vector2(ndcPos.x, ndcPos.y) * 0.5f + new Vector2(0.5f, 0.5f);
        uv.x = (int)(uv.x * fit.resolution + 0.5);
        uv.y = (int)(uv.y * fit.resolution + 0.5);
        uv /= fit.resolution;
        uv = uv * 2f - Vector2.one;
        ndcPos = new Vector3(uv.x, uv.y, ndcPos.z);
        targetPosition = invShadowVP.MultiplyPoint(ndcPos);
        shadCam.position = targetPosition;
        shadCam.UpdateProjectionMatrix();
        shadCam.UpdateTRSMatrix();
        shadowVP = GL.GetGPUProjectionMatrix(shadCam.projectionMatrix, false) * shadCam.worldToCameraMatrix;
        invShadowVP = shadowVP.inverse;
    }
    /// <summary>
    /// Initialize per cascade shadowmap buffers
    /// </summary>
    public static void UpdateCascadeState(ref ShadowMapComponent comp, CommandBuffer buffer, float bias, int pass, out Matrix4x4 rtVp)
    {
        Vector4 shadowcamDir = comp.shadCam.forward;
        shadowcamDir.w = bias;
        buffer.SetRenderTarget(comp.shadowmapTexture, 0, CubemapFace.Unknown, depthSlice: pass);
        buffer.ClearRenderTarget(true, true, Color.white);
        buffer.SetGlobalVector(ShaderIDs._ShadowCamDirection, shadowcamDir);
        rtVp = GL.GetGPUProjectionMatrix(comp.shadCam.projectionMatrix, true) * comp.shadCam.worldToCameraMatrix;
        buffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, rtVp);
    }
    /// <summary>
    /// Initialize shadowmask per frame buffers
    /// </summary>


    public static void Dispose(PipelineBaseBuffer baseBuffer)
    {
        baseBuffer.verticesBuffer.Dispose();
        baseBuffer.clusterBuffer.Dispose();
        baseBuffer.instanceCountBuffer.Dispose();
        baseBuffer.resultBuffer.Dispose();
        baseBuffer.dispatchBuffer.Dispose();
        baseBuffer.reCheckCount.Dispose();
        if (baseBuffer.reCheckResult != null)
        {
            baseBuffer.reCheckResult.Dispose();
            baseBuffer.reCheckResult = null;
        }
    }
    /// <summary>
    /// Set Basement buffers
    /// </summary>
    public static void SetBaseBuffer(PipelineBaseBuffer baseBuffer, ComputeShader gpuFrustumShader, Vector4[] frustumCullingPlanes, CommandBuffer buffer)
    {
        var compute = gpuFrustumShader;
        buffer.SetComputeVectorArrayParam(compute, ShaderIDs.planes, frustumCullingPlanes);
        buffer.SetComputeBufferParam(compute, 0, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
        buffer.SetComputeBufferParam(compute, 0, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(compute, 1, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(compute, 0, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
    }

    public static void DrawLastFrameCullResult(
        PipelineBaseBuffer baseBuffer,
        CommandBuffer buffer, Material mat)
    {
        buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
        buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
        buffer.DrawProceduralIndirect(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DrawRecheckCullResult(
        PipelineBaseBuffer occBuffer,
        Material indirectMaterial, CommandBuffer buffer)
    {
        buffer.DrawProceduralIndirect(Matrix4x4.identity, indirectMaterial, 0, MeshTopology.Triangles, occBuffer.reCheckCount, 0);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RunCullDispatching(PipelineBaseBuffer baseBuffer, ComputeShader computeShader, bool isOrtho, CommandBuffer buffer)
    {
        buffer.SetComputeIntParam(computeShader, ShaderIDs._CullingPlaneCount, isOrtho ? 6 : 5);
        ComputeShaderUtility.Dispatch(computeShader, buffer, 0, baseBuffer.clusterCount, 64);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RenderProceduralCommand(PipelineBaseBuffer buffer, Material material, CommandBuffer cb)
    {
        cb.SetGlobalBuffer(ShaderIDs.resultBuffer, buffer.resultBuffer);
        cb.SetGlobalBuffer(ShaderIDs.verticesBuffer, buffer.verticesBuffer);
        cb.DrawProceduralIndirect(Matrix4x4.identity, material, 0, MeshTopology.Triangles, buffer.instanceCountBuffer, 0);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetViewProjectMatrix(Camera currentCam, out Matrix4x4 vp, out Matrix4x4 invVP)
    {
        vp = GL.GetGPUProjectionMatrix(currentCam.projectionMatrix, false) * currentCam.worldToCameraMatrix;
        invVP = vp.inverse;
    }

    public static void InitRenderTarget(ref RenderTargets tar, Camera tarcam, List<RenderTexture> collectRT, CommandBuffer buffer)
    {
        tar.gbufferIdentifier[0] = GetTemporary(tarcam.pixelWidth, tarcam.pixelHeight, 0, RenderTextureFormat.ARGB32, FilterMode.Point, collectRT);
        tar.gbufferIdentifier[1] = GetTemporary(tarcam.pixelWidth, tarcam.pixelHeight, 0, RenderTextureFormat.ARGB32, FilterMode.Point, collectRT);
        tar.gbufferIdentifier[2] = GetTemporary(tarcam.pixelWidth, tarcam.pixelHeight, 0, RenderTextureFormat.ARGB2101010, FilterMode.Point, collectRT);
        tar.gbufferIdentifier[3] = GetTemporary(tarcam.pixelWidth, tarcam.pixelHeight, 24, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear, collectRT);
        tar.gbufferIdentifier[4] = GetTemporary(tarcam.pixelWidth, tarcam.pixelHeight, 0, RenderTextureFormat.RGHalf, FilterMode.Point, collectRT);
        tar.gbufferIdentifier[5] = GetTemporary(tarcam.pixelWidth, tarcam.pixelHeight, 0, RenderTextureFormat.RFloat, FilterMode.Point, collectRT);
        for (int i = 0; i < tar.gbufferIdentifier.Length; ++i)
        {
            buffer.SetGlobalTexture(tar.gbufferIndex[i], tar.gbufferIdentifier[i]);
        }
        RenderTargetIdentifier renderTarget = tar.gbufferIdentifier[3];
        tar.backupTarget = GetTemporary(tarcam.pixelWidth, tarcam.pixelHeight, 0, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear, collectRT);
        tar.backupTarget.filterMode = FilterMode.Bilinear;
        tar.renderTargetIdentifier = renderTarget;
        tar.backupIdentifier = new RenderTargetIdentifier(tar.backupTarget);
        tar.depthIdentifier = renderTarget;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderTexture GetTemporary(RenderTextureDescriptor descriptor, List<RenderTexture> collectList)
    {
        RenderTexture rt = RenderTexture.GetTemporary(descriptor);
        collectList.Add(rt);
        return rt;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderTexture GetTemporary(int width, int height, int depth, RenderTextureFormat format, FilterMode filterMode, List<RenderTexture> collectList)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, depth, format, RenderTextureReadWrite.Linear);
        rt.filterMode = filterMode;
        collectList.Add(rt);
        return rt;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderTexture GetTemporary(int width, int height, int depth, RenderTextureFormat format, RenderTextureReadWrite readWrite, FilterMode filterMode, List<RenderTexture> collectList)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, depth, format, readWrite);
        rt.filterMode = filterMode;
        collectList.Add(rt);
        return rt;
    }
    public static void ReleaseRenderTarget(List<RenderTexture> tar)
    {
        foreach (var i in tar)
        {
            RenderTexture.ReleaseTemporary(i);
        }
        tar.Clear();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExecuteCommandBuffer(ref this PipelineCommandData data)
    {
        data.context.ExecuteCommandBuffer(data.buffer);
        data.buffer.Clear();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExecuteCommandBufferAsync(ref this PipelineCommandData data, CommandBuffer asyncBuffer, ComputeQueueType queueType)
    {
        data.context.ExecuteCommandBufferAsync(asyncBuffer, queueType);
        asyncBuffer.Clear();
    }

    public static void InsertTo<T>(this List<T> targetArray, T value, Func<T, T, int> compareResult)
    {
        Vector2Int range = new Vector2Int(0, targetArray.Count);
        while (true)
        {
            if (targetArray.Count == 0)
            {
                targetArray.Add(value);
                return;
            }
            else if (Math.Abs(range.x - range.y) == 1)
            {
                int compareX = compareResult(targetArray[range.x], value);
                if (compareX < 0)
                {
                    targetArray.Insert(range.x, value);
                    return;
                }
                else if (compareX > 0)
                {
                    if (range.y < targetArray.Count && compareResult(targetArray[range.y], value) == 0)
                    {
                        return;
                    }
                    else
                    {
                        targetArray.Insert(range.y, value);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                int currentIndex = (int)((range.x + range.y) / 2f);
                int compare = compareResult(targetArray[currentIndex], value);
                if (compare == 0)
                {
                    return;
                }
                else
                {
                    if (compare < 0)
                    {
                        range.y = currentIndex;
                    }
                    else if (compare > 0)
                    {
                        range.x = currentIndex;
                    }
                }
            }
        }
    }
    public static void UpdateOcclusionBuffer(
        PipelineBaseBuffer basebuffer
        , ComputeShader coreShader
        , CommandBuffer buffer
        , HizOcclusionData occlusionData
        , Vector4[] frustumCullingPlanes
        , bool isOrtho)
    {
        buffer.SetComputeIntParam(coreShader, ShaderIDs._CullingPlaneCount, isOrtho ? 6 : 5);
        buffer.SetComputeVectorArrayParam(coreShader, ShaderIDs.planes, frustumCullingPlanes);
        buffer.SetComputeVectorParam(coreShader, ShaderIDs._CameraUpVector, occlusionData.lastFrameCameraUp);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.FrustumFilter, ShaderIDs.clusterBuffer, basebuffer.clusterBuffer);
        buffer.SetComputeTextureParam(coreShader, OcclusionBuffers.FrustumFilter, ShaderIDs._HizDepthTex, occlusionData.historyDepth);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.FrustumFilter, ShaderIDs.dispatchBuffer, basebuffer.dispatchBuffer);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.FrustumFilter, ShaderIDs.resultBuffer, basebuffer.resultBuffer);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.FrustumFilter, ShaderIDs.instanceCountBuffer, basebuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.FrustumFilter, ShaderIDs.reCheckResult, basebuffer.reCheckResult);
        ComputeShaderUtility.Dispatch(coreShader, buffer, OcclusionBuffers.FrustumFilter, basebuffer.clusterCount, 64);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearOcclusionData(
        PipelineBaseBuffer baseBuffer, CommandBuffer buffer
        , ComputeShader coreShader)
    {
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.ClearOcclusionData, ShaderIDs.dispatchBuffer, baseBuffer.dispatchBuffer);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.ClearOcclusionData, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.ClearOcclusionData, ShaderIDs.reCheckCount, baseBuffer.reCheckCount);
        buffer.DispatchCompute(coreShader, OcclusionBuffers.ClearOcclusionData, 1, 1, 1);
    }
    public static void OcclusionRecheck(
        PipelineBaseBuffer baseBuffer
        , ComputeShader coreShader, CommandBuffer buffer
        , HizOcclusionData hizData)
    {
        buffer.SetComputeVectorParam(coreShader, ShaderIDs._CameraUpVector, hizData.lastFrameCameraUp);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.OcclusionRecheck, ShaderIDs.dispatchBuffer, baseBuffer.dispatchBuffer);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.OcclusionRecheck, ShaderIDs.reCheckResult, baseBuffer.reCheckResult);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.OcclusionRecheck, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
        buffer.SetComputeTextureParam(coreShader, OcclusionBuffers.OcclusionRecheck, ShaderIDs._HizDepthTex, hizData.historyDepth);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.OcclusionRecheck, ShaderIDs.reCheckCount, baseBuffer.reCheckCount);
        buffer.SetComputeBufferParam(coreShader, OcclusionBuffers.OcclusionRecheck, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
        buffer.DispatchCompute(coreShader, OcclusionBuffers.OcclusionRecheck, baseBuffer.dispatchBuffer, 0);
    }

    public static void CopyToCubeMap(RenderTexture cubemapArray, RenderTexture texArray, CommandBuffer buffer, int offset)
    {
        offset *= 6;
        for (int i = 0; i < 6; ++i)
        {
            buffer.CopyTexture(texArray, i, cubemapArray, offset + i);
        }
    }
}