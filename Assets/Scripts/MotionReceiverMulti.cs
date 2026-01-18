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
    public bool flipFrontBack = true;

    [Header("Debug")]
    public bool verboseLogs = true;

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

    // debug stats
    private int pktCount = 0;
    private float lastStatTime = 0f;
    private string lastSender = "";
    private int lastSlot = -999;
    private string lastDevice = "";

    void Start()
    {
        try
        {
            udp = new UdpClient(listenPort);
            udp.Client.ReceiveTimeout = 1000;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MotionReceiverMulti] FAILED to bind UDP port {listenPort}. " +
                           $"Is the port in use? Error: {e.Message}");
            enabled = false;
            return;
        }

        running = true;
        thread = new Thread(ReceiveLoop) { IsBackground = true };
        thread.Start();

        lastStatTime = Time.realtimeSinceStartup;
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
        // packet rate logging
        float now = Time.realtimeSinceStartup;
        if (now - lastStatTime >= 1.0f)
        {
            if (pktCount > 0 || verboseLogs)
            {
                Debug.Log($"[MotionReceiverMulti] ~{pktCount} pkt/s | lastSender={lastSender} | lastSlot={lastSlot} | lastDevice={lastDevice}");
            }
            pktCount = 0;
            lastStatTime = now;
        }

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

        if (flipFrontBack)
            mapped = Quaternion.Euler(0f, 180f, 0f) * mapped;

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

                // debug sender
                lastSender = $"{any.Address}:{any.Port}";
                pktCount++;

                MotionPacketData p;
                try
                {
                    p = JsonUtility.FromJson<MotionPacketData>(json);
                }
                catch (Exception je)
                {
                    if (verboseLogs)
                        Debug.LogWarning($"[MotionReceiverMulti] JSON parse failed: {je.Message} | raw={json}");
                    continue;
                }

                if (p == null)
                {
                    if (verboseLogs)
                        Debug.LogWarning($"[MotionReceiverMulti] Parsed null packet | raw={json}");
                    continue;
                }

                lastSlot = p.playerSlot;
                lastDevice = p.deviceId ?? "";

                if (string.IsNullOrEmpty(p.deviceId))
                {
                    if (verboseLogs)
                        Debug.LogWarning($"[MotionReceiverMulti] Missing deviceId | slot={p.playerSlot} | raw={json}");
                    continue;
                }

                // IMPORTANT: if playerSlot isn't 1 or 2, nothing will move.
                if (p.playerSlot != 1 && p.playerSlot != 2)
                {
                    if (verboseLogs)
                        Debug.LogWarning($"[MotionReceiverMulti] playerSlot={p.playerSlot} (expected 1 or 2). Nothing will update.");
                    continue;
                }

                lock (locker)
                {
                    if (p.playerSlot == 1)
                    {
                        latestP1 = p;
                        hasP1 = true;
                    }
                    else
                    {
                        latestP2 = p;
                        hasP2 = true;
                    }
                }
            }
            catch (SocketException)
            {
                // timeout
            }
            catch (ObjectDisposedException)
            {
                // closing
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MotionReceiverMulti] UDP error: {e.Message}");
            }
        }
    }
}
