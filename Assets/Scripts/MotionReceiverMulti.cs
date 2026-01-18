using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class MotionPacketData
{
    public double t;
    public string deviceId;
    public int playerSlot;

    public double qx, qy, qz, qw;
    public double gx, gy, gz;
    public double ax, ay, az;
}

public class MotionReceiverMulti : MonoBehaviour
{
    [Header("UDP")]
    public int listenPort = 9000;

    [Header("Rackets")]
    public Transform racketP1;
    public Transform racketP2;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float rotationLerp = 0.35f;

    [Header("Mapping Preset")]
    public MappingPreset preset = MappingPreset.PresetA;

    [Header("Fixes")]
    public bool flipFrontBack = true; // ✅ turn this on to fix forward/back reversed

    public enum MappingPreset
    {
        PresetA, // flip Z
        PresetB, // swap Y/Z + flip
        PresetC, // flip X
    }

    private readonly object locker = new object();
    private MotionPacketData latestP1;
    private MotionPacketData latestP2;
    private bool hasP1 = false;
    private bool hasP2 = false;

    private UdpClient udp;
    private Thread thread;
    private volatile bool running;

    void Start()
    {
        udp = new UdpClient(listenPort);
        udp.Client.ReceiveTimeout = 1000;

        running = true;
        thread = new Thread(ReceiveLoop) { IsBackground = true };
        thread.Start();

        Debug.Log($"[MotionReceiverMulti] Listening UDP on port {listenPort}");
    }

    void OnDestroy()
    {
        running = false;
        try { udp?.Close(); } catch { }
        try { thread?.Join(200); } catch { }
    }

    void Update()
    {
        if (racketP1 != null && hasP1)
        {
            MotionPacketData p;
            lock (locker) { p = latestP1; }
            ApplyRotation(racketP1, p);
        }

        if (racketP2 != null && hasP2)
        {
            MotionPacketData p;
            lock (locker) { p = latestP2; }
            ApplyRotation(racketP2, p);
        }
    }

    private void ApplyRotation(Transform target, MotionPacketData p)
    {
        Quaternion q = MapQuaternion(p);
        float alpha = 1f - Mathf.Pow(1f - rotationLerp, Time.deltaTime * 60f);
        target.rotation = Quaternion.Slerp(target.rotation, q, alpha);
    }

    private Quaternion MapQuaternion(MotionPacketData p)
    {
        Quaternion raw = new Quaternion((float)p.qx, (float)p.qy, (float)p.qz, (float)p.qw);

        // 1) Base mapping (KEEP what already makes L/R correct)
        Quaternion mapped;
        switch (preset)
        {
            case MappingPreset.PresetA:
                mapped = new Quaternion(raw.x, raw.y, -raw.z, raw.w);
                break;
            case MappingPreset.PresetB:
                mapped = new Quaternion(raw.x, -raw.z, raw.y, raw.w);
                break;
            case MappingPreset.PresetC:
                mapped = new Quaternion(-raw.x, raw.y, raw.z, raw.w);
                break;
            default:
                mapped = raw;
                break;
        }

        // 2) Fix front/back reversed by flipping "forward" (yaw 180)
        // Multiply on the left to rotate the whole frame.
        if (flipFrontBack)
        {
            mapped = Quaternion.Euler(0f, 180f, 0f) * mapped;
        }

        return mapped;
    }

    private void ReceiveLoop()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref any);
                string json = Encoding.UTF8.GetString(data);

                MotionPacketData p = JsonUtility.FromJson<MotionPacketData>(json);
                if (p == null || string.IsNullOrEmpty(p.deviceId)) continue;

                lock (locker)
                {
                    if (p.playerSlot == 1)
                    {
                        latestP1 = p;
                        hasP1 = true;
                    }
                    else if (p.playerSlot == 2)
                    {
                        latestP2 = p;
                        hasP2 = true;
                    }
                }
            }
            catch (SocketException)
            {
                // timeout, ignore
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MotionReceiverMulti] UDP error: {e.Message}");
            }
        }
    }
}
