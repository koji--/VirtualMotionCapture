﻿//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;
using VRM;
using System.Reflection;

using sh_akira;
using sh_akira.OVRTracking;

[RequireComponent(typeof(uOSC.uOscClient))]
public class ExternalSender : MonoBehaviour
{
    uOSC.uOscClient uClient = null;
    GameObject CurrentModel = null;
    ControlWPFWindow window = null;
    Animator animator = null;
    VRIK vrik = null;
    VRMBlendShapeProxy blendShapeProxy = null;
    Camera currentCamera = null;

    public SteamVR2Input steamVR2Input;
    public MidiCCWrapper midiCCWrapper;

    //フレーム周期
    public int periodStatus = 1;
    public int periodRoot = 1;
    public int periodBone = 1;
    public int periodBlendShape = 1;
    public int periodCamera = 1;
    public int periodDevices = 1;

    //フレーム数カウント用
    private int frameOfStatus = 1;
    private int frameOfRoot = 1;
    private int frameOfBone = 1;
    private int frameOfBlendShape = 1;
    private int frameOfCamera = 1;
    private int frameOfDevices = 1;

    //パケット分割数
    const int PACKET_DIV_BONE = 12;

    GameObject handTrackerRoot;
    TrackerHandler trackerHandler;

    void Start()
    {
        uClient = GetComponent<uOSC.uOscClient>();
        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
        handTrackerRoot = GameObject.Find("HandTrackerRoot");

        trackerHandler = handTrackerRoot.GetComponent<TrackerHandler>();

        window.ModelLoadedAction += (GameObject CurrentModel) =>
        {
            if (CurrentModel != null)
            {
                this.CurrentModel = CurrentModel;
                animator = CurrentModel.GetComponent<Animator>();
                vrik = CurrentModel.GetComponent<VRIK>();
                blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
            }
        };

        window.CameraChangedAction += (Camera currentCamera) =>
        {
            this.currentCamera = currentCamera;
        };

        steamVR2Input.KeyDownEvent += (object sender, OVRKeyEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: ConDown");
                try
                {
                    uClient?.Send("/VMC/Ext/Con", 1, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };

        steamVR2Input.KeyUpEvent += (object sender, OVRKeyEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: ConUp");
                try
                {
                    uClient?.Send("/VMC/Ext/Con", 0, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };

        steamVR2Input.AxisChangedEvent += (object sender, OVRKeyEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: ConAxis");
                try
                {
                    if (e.IsAxis)
                    {
                        uClient?.Send("/VMC/Ext/Con", 2, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };

        KeyboardAction.KeyDownEvent += (object sender, KeyboardEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: KeyDown");
                try
                {
                    uClient?.Send("/VMC/Ext/Key", 1, e.KeyName, e.KeyCode);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };
        KeyboardAction.KeyUpEvent += (object sender, KeyboardEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: KeyUp");
                try
                {
                    uClient?.Send("/VMC/Ext/Key", 0, e.KeyName, e.KeyCode);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };

        midiCCWrapper.noteOnDelegateProxy += (MidiJack.MidiChannel channel, int note, float velocity) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: KeyDown");
                try
                {
                    uClient?.Send("/VMC/Ext/Midi/Note", 1, (int)channel, note, velocity);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };
        midiCCWrapper.noteOffDelegateProxy += (MidiJack.MidiChannel channel, int note) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: KeyDown");
                try
                {
                    uClient?.Send("/VMC/Ext/Midi/Note", 0, (int)channel, note, (float)0f);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };
        midiCCWrapper.knobUpdateFloatDelegate += (int knobNo, float value) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: KeyDown");
                try
                {
                    uClient?.Send("/VMC/Ext/Midi/CC/Val", knobNo, value);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };
        midiCCWrapper.knobUpdateBoolDelegate += (int knobNo, bool value) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: KeyDown");
                try
                {
                    uClient?.Send("/VMC/Ext/Midi/CC/Bit", knobNo, (int)(value?1:0));
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };
    }

    // Update is called once per frame
    void Update()
    {
        uOSC.Bundle rootBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);

        if (CurrentModel != null && animator != null)
        {
            //Root
            if (vrik == null)
            {
                vrik = CurrentModel.GetComponent<VRIK>();
                Debug.Log("ExternalSender: VRIK Updated");
            }

            if (frameOfRoot > periodRoot && periodRoot != 0)
            {
                frameOfRoot = 1;
                if (vrik != null)
                {
                    var RootTransform = vrik.references.root;
                    var offset = handTrackerRoot.transform;
                    if (RootTransform != null && offset != null)
                    {
                        rootBundle.Add(new uOSC.Message("/VMC/Ext/Root/Pos",
                            "root",
                            RootTransform.position.x, RootTransform.position.y, RootTransform.position.z,
                            RootTransform.rotation.x, RootTransform.rotation.y, RootTransform.rotation.z, RootTransform.rotation.w,
                            offset.localScale.x, offset.localScale.y, offset.localScale.z,
                            offset.position.x, offset.position.y, offset.position.z));
                    }
                }
            }
            frameOfRoot++;

            //Bones
            if (frameOfBone > periodBone && periodBone != 0)
            {
                frameOfBone = 1;

                uOSC.Bundle boneBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);
                int cnt = 0;//パケット分割カウンタ

                foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    var Transform = animator.GetBoneTransform(bone);
                    if (Transform != null)
                    {
                        boneBundle.Add(new uOSC.Message("/VMC/Ext/Bone/Pos",
                            bone.ToString(),
                            Transform.localPosition.x, Transform.localPosition.y, Transform.localPosition.z,
                            Transform.localRotation.x, Transform.localRotation.y, Transform.localRotation.z, Transform.localRotation.w));

                        cnt++;
                        //1200バイトを超えない程度に分割する
                        if (cnt > PACKET_DIV_BONE) {
                            uClient?.Send(boneBundle);
                            boneBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);
                            cnt = 0;

                        }
                    }
                }
                //余ったボーンは雑多な情報と共に送る
                rootBundle.Add(boneBundle);
            }
            frameOfBone++;

            //Blendsharp
            if (blendShapeProxy == null)
            {
                blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
                Debug.Log("ExternalSender: VRMBlendShapeProxy Updated");
            }

            if (frameOfBlendShape > periodBlendShape && periodBlendShape != 0)
            {
                frameOfBlendShape = 1;

                uOSC.Bundle blendShapeBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);
                if (blendShapeProxy != null)
                {
                    foreach (var b in blendShapeProxy.GetValues())
                    {
                        blendShapeBundle.Add(new uOSC.Message("/VMC/Ext/Blend/Val",
                            b.Key.ToString(),
                            (float)b.Value
                            ));
                    }
                    blendShapeBundle.Add(new uOSC.Message("/VMC/Ext/Blend/Apply"));
                }
                uClient?.Send(blendShapeBundle);
            }
            frameOfBlendShape++;
        }

        //Camera
        if (frameOfCamera > periodCamera && periodCamera != 0)
        {
            frameOfCamera = 1;
            if (currentCamera != null)
            {
                rootBundle.Add(new uOSC.Message("/VMC/Ext/Cam",
                    "Camera",
                    currentCamera.transform.position.x, currentCamera.transform.position.y, currentCamera.transform.position.z,
                    currentCamera.transform.rotation.x, currentCamera.transform.rotation.y, currentCamera.transform.rotation.z, currentCamera.transform.rotation.w,
                    currentCamera.fieldOfView));
            }
        }
        frameOfCamera++;

        //TrackerSend
        if (frameOfDevices > periodDevices && periodDevices != 0)
        {
            frameOfDevices = 1;

            rootBundle.Add(new uOSC.Message("/VMC/Ext/Hmd/Pos",
                    trackerHandler.HMDObject.name,
                    trackerHandler.HMDObject.transform.position.x, trackerHandler.HMDObject.transform.position.y, trackerHandler.HMDObject.transform.position.z,
                    trackerHandler.HMDObject.transform.rotation.x, trackerHandler.HMDObject.transform.rotation.y, trackerHandler.HMDObject.transform.rotation.z, trackerHandler.HMDObject.transform.rotation.w));
            rootBundle.Add(new uOSC.Message("/VMC/Ext/Hmd/Pos/Local",
                    trackerHandler.HMDObject.name,
                    trackerHandler.HMDObject.transform.localPosition.x, trackerHandler.HMDObject.transform.localPosition.y, trackerHandler.HMDObject.transform.localPosition.z,
                    trackerHandler.HMDObject.transform.localRotation.x, trackerHandler.HMDObject.transform.localRotation.y, trackerHandler.HMDObject.transform.localRotation.z, trackerHandler.HMDObject.transform.localRotation.w));
            
            foreach (var c in trackerHandler.Controllers)
            {
                rootBundle.Add(new uOSC.Message("/VMC/Ext/Con/Pos",
                        c.name,
                        c.transform.position.x, c.transform.position.y, c.transform.position.z,
                        c.transform.rotation.x, c.transform.rotation.y, c.transform.rotation.z, c.transform.rotation.w));
                rootBundle.Add(new uOSC.Message("/VMC/Ext/Con/Pos/Local",
                        c.name,
                        c.transform.localPosition.x, c.transform.localPosition.y, c.transform.localPosition.z,
                        c.transform.localRotation.x, c.transform.localRotation.y, c.transform.localRotation.z, c.transform.localRotation.w));
            }
            foreach (var c in trackerHandler.Trackers)
            {
                rootBundle.Add(new uOSC.Message("/VMC/Ext/Tra/Pos",
                        c.name,
                        c.transform.position.x, c.transform.position.y, c.transform.position.z,
                        c.transform.rotation.x, c.transform.rotation.y, c.transform.rotation.z, c.transform.rotation.w));
                rootBundle.Add(new uOSC.Message("/VMC/Ext/Tra/Pos/Local",
                        c.name,
                        c.transform.localPosition.x, c.transform.localPosition.y, c.transform.localPosition.z,
                        c.transform.localRotation.x, c.transform.localRotation.y, c.transform.localRotation.z, c.transform.localRotation.w));
            }
        }
        frameOfDevices++;



        //Status
        if (frameOfStatus > periodStatus && periodStatus != 0)
        {
            frameOfStatus = 1;
            if (CurrentModel != null && animator != null)
            {
                //Available
                rootBundle.Add(new uOSC.Message("/VMC/Ext/OK", 1));
            }
            else
            {
                rootBundle.Add(new uOSC.Message("/VMC/Ext/OK", 0));
            }
            rootBundle.Add(new uOSC.Message("/VMC/Ext/T", Time.time));

        }
        frameOfStatus++;

        uClient?.Send(rootBundle);

        //---End of frame---
    }

    public void ChangeOSCAddress(string address, int port)
    {
        if (uClient == null) uClient = GetComponent<uOSC.uOscClient>();
        uClient.enabled = false;
        var type = typeof(uOSC.uOscClient);
        var addressfield = type.GetField("address", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
        addressfield.SetValue(uClient, address);
        var portfield = type.GetField("port", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
        portfield.SetValue(uClient, port);
        uClient.enabled = true;
    }
}

