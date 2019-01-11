using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.IO;
using Proximity.Extensions;
using ClickThroughFix;

namespace Proximity
{
    class Proximity : PartModule
    {
        private static Rect windowPos = new Rect();

        // beep characteristics
        private static string[] beepType = { "Square", "Saw", "Sine", "None" };
        private static int beepIndex = 0;

        private static string[] pitchType = { "Variable", "440 Hz", "880 Hz", "1760 Hz" };
        private static int pitchIndex = 0;

        // visual display
        private static string[] visualType = { "Distance", "Speed", "Dist >%m, Speed <%m", "No visuals" };
        private static int visualIndex = 0;

        // expand window - showsettings is toggled by the button on the prox window itself. It is not used
        // if settingsMode (controlled from the toolbar button) is true, in which case settings are always shown and 
        // the button on the prox window is not shown
        private static bool GUIShowSettings = false;


        internal static bool toolbarShowSettings = false;
        public static bool ToolbarShowSettings
        {
            get { return toolbarShowSettings; }
            set
            {
                toolbarShowSettings = value;
                sizechange = true;
            }
        }

        // private static bool UseToolbar = false;
        private static StockToolbar stockToolbar = null;

        private static int ActivationHeight = 4000; // for proximity as a whole
        private static int DSThreshold = 300; // threshold height for both (a) switching between the two visual modes,

        //private static ToolbarButtonWrapper toolbarButton = null;

        private int altitude = 0;

        private bool newInstance = true;

        // resize window? - prevents blinking when buttons clicked
        private static bool sizechange = true;

        // rate of visual and audio output
        private int skip = 0;
        private int audioskip = 0;
        private int audioSkipValue = 10;
        private bool newBeepAllowed = true;

        public static bool HideUI
        {
            get;
            set;
        }

        // visual output 
        private static string warnstring = "------------------------------------------------";
        private static string warn = "";
        private int warnPos = 0;

        // sound generation
        private static AudioSource audioSource = null;

        private static float volume = 0.5f;
        private static float[] cachedBeep = null;

        private static bool deactivateIfRover = true;
        private static bool deactivateOnParachute = true;
        private bool parachutesOpen = false;

        // delay controls
        private double timeSinceLanding = 0;
        private double timeSinceDescending = 0;
        private double timeSinceOnGround = 0;
        private double gracePeriod = 3.0;

        // to resize window when settings shown / unshown
        private static bool prevConditionalShow = false;
        private static bool ConditionalShow = false;

        private static bool isPowered = true;

        public static bool SystemOn
        {
            get;
            set;
        }

        private const float fixedwidth = 255f;
        private const float margin = 20f;

        private GUIStyle styleTextArea = null;
        private GUIStyle styleButton = null;
        private GUIStyle styleValue = null;
        private GUIStyle styleToggle = null;

        bool lostToStaging = false;

        private int numParts = -1;
        private int stage = -1;

        private bool doneOnce = false;

        private void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

            if (!TechChecker.TechAvailable)
            {
                return;
            }

            if (FlightGlobals.ActiveVessel != null)
            {
                if (Event.current.type == EventType.Repaint || Event.current.isMouse)
                {
                    if (audioSource == null)
                    {
                        audioSource = GetComponent<AudioSource>();
                    }

                    timeSinceLanding = FlightGlobals.ActiveVessel.missionTime;
                }
                ProximityDraw();
            }
        }

        void OnAudioRead(float[] data)
        {
            if (FlightGlobals.ActiveVessel == null)
            {
                return;
            }

            try
            {
                if (!newBeepAllowed)
                {
                    data = cachedBeep;
                    return;
                }

                float actfrequency;

                switch (pitchIndex)
                {
                    case 0:
                        double absspeed = FlightGlobals.ActiveVessel.verticalSpeed;
                        if (absspeed > 0)
                        {
                            absspeed = 0;
                        }
                        float velocity = Mathf.Min((float)(-1.0 * absspeed), 250f);
                        actfrequency = 180 + (velocity * 15f);
                        break;
                    case 1:
                        actfrequency = 440;
                        break;
                    case 2:
                        actfrequency = 880;
                        break;
                    case 3:
                        actfrequency = 1760;
                        break;
                    default:
                        actfrequency = 880;
                        break;
                }

                float gain = 0.6f;
                float increment;
                float phase = 0;
                float sampling_frequency = 44100;

                switch (beepIndex)
                {
                    case 0: // square
                        {
                            increment = actfrequency * 2 * Mathf.PI / sampling_frequency;

                            for (var i = 0; i < data.Length; i++)
                            {
                                phase = phase + increment;

                                data[i] = Mathf.Sign(Mathf.Sin(phase));

                                if (phase > 2 * Math.PI) phase = 0;
                            }
                        }
                        break;
                    case 1: // saw
                        {
                            increment = actfrequency * 2 * Mathf.PI / sampling_frequency;

                            for (var i = 0; i < data.Length; i++)
                            {
                                phase = phase + increment;

                                data[i] = Mathf.PingPong(gain * Mathf.Sin(phase), 1);

                                if (phase > 2 * Math.PI) phase = 0;
                            }
                        }
                        break;
                    case 2: // sine
                        {
                            increment = actfrequency * 2 * Mathf.PI / sampling_frequency;

                            for (var i = 0; i < data.Length; i++)
                            {
                                phase = phase + increment;

                                data[i] = (float)(gain * Math.Sin(phase));

                                if (phase > 2 * Math.PI) phase = 0;
                            }
                        }
                        break;
                }
                cachedBeep = data;
            }
            catch (Exception ex)
            {
                print("Proximity - OnAudioRead(): " + ex.Message);
            }
        }

        void OnAudioSetPosition(int newPosition)
        {
            return;
        }

       // public override void OnSave(ConfigNode node)
       public void Save()
        {
            ConfigNode file = new ConfigNode();
            ConfigNode node1 = new ConfigNode();

            file.AddNode("Proximity", node1);

            node1.AddValue("posXmin", windowPos.xMin);
            node1.AddValue("posXmax", windowPos.xMax);
            node1.AddValue("posYmin", windowPos.yMin);
            node1.AddValue("posYmax", windowPos.yMax);

            node1.AddValue("beepIndex", beepIndex);
            node1.AddValue("visualIndex", visualIndex);
            node1.AddValue("ActivationHeight", ActivationHeight);
            node1.AddValue("DSThreshold", DSThreshold);
            node1.AddValue("pitchIndex", pitchIndex);
            node1.AddValue("deactivateOnParachute", deactivateOnParachute);
            node1.AddValue("deactivateIfRover", deactivateIfRover);
            node1.AddValue("volume", (int)(volume * 100));


            file.Save(StockToolbar.dataPath + "config.cfg");
        }

        public override void OnLoad(ConfigNode node)
        {
            if (System.IO.File.Exists(StockToolbar.dataPath + "config.cfg"))
            {
                ConfigNode file = ConfigNode.Load(StockToolbar.dataPath + "config.cfg");
                if (file != null)
                {
                    ConfigNode node1 = file.GetNode("Proximity");
                    if (node1 != null)
                    {
                        {
                            windowPos = new Rect();
                            float f;
                            float.TryParse(node1.GetValue("posXmin"), out f);
                            windowPos.xMin = f;
                            float.TryParse(node1.GetValue("posXmax"), out f);
                            windowPos.xMax = f;
                            float.TryParse(node1.GetValue("posYmin"), out f);
                            windowPos.yMin = f;
                            float.TryParse(node1.GetValue("posYmax"), out f);
                            windowPos.yMax = f;
                        }
                        int.TryParse(node1.GetValue("beepIndex"), out beepIndex);

                        int.TryParse(node1.GetValue("visualIndex"), out visualIndex);
                        int.TryParse(node1.GetValue("ActivationHeight"), out ActivationHeight);
                        int.TryParse(node1.GetValue("DSThreshold"), out DSThreshold);
                        Boolean.TryParse(node1.GetValue("GUIShowSettings"), out GUIShowSettings);

                        int.TryParse(node1.GetValue("pitchIndex"), out pitchIndex);
                        Boolean.TryParse(node1.GetValue("deactivateOnParachute"), out deactivateOnParachute);
                        Boolean.TryParse(node1.GetValue("deactivateIfRover"), out deactivateIfRover);

                        int vol = 50;
                        int.TryParse(node1.GetValue("volume"), out vol);
                        volume = ((float)vol) / 100f;
                    }
                }
            }
            else
            {
                // no config file, set defaults
                ActivationHeight = 4000;
                DSThreshold = 500;
                beepIndex = 1;
                pitchIndex = 0;
                visualIndex = 1;
                volume = 0.5f;
                deactivateIfRover = true;
            }
            windowPos.width = fixedwidth;

            if (ActivationHeight < 500) ActivationHeight = 500;
            if (ActivationHeight > 10000) ActivationHeight = 10000;

            if (DSThreshold < 200) DSThreshold = 200;
            if (DSThreshold > 2000) DSThreshold = 2000;

            if (beepIndex < 0 || beepIndex > 3) beepIndex = 1;
            if (pitchIndex < 0 || pitchIndex > 3) pitchIndex = 0;
            if (visualIndex < 0 || visualIndex > 3) visualIndex = 1;

            if (volume > 1.0f) volume = 1.0f;
            if (volume <= 0) volume = 0.5f;

        }

        private void ProximityDraw()
        {
            try
            {
                if (FlightGlobals.ActiveVessel != null)
                {
                    altitude = GetAltitude();

                    isPowered = IsPowered();

                    // this takes account of vessels splitting (when undocking), Kerbals going on EVA, etc.
                    if (newInstance || (FlightGlobals.ActiveVessel.parts.Count != numParts || FlightGlobals.ActiveVessel.currentStage != stage))
                    {
                        numParts = FlightGlobals.ActiveVessel.parts.Count;
                        stage = FlightGlobals.ActiveVessel.currentStage;

                        newInstance = false;
                        lostToStaging = false;

                        if (!RefreshStockButton())
                        {
                            return;
                        }
                    }

                    if (RightConditionsToDraw())
                    {
                        if (ConditionalShow != prevConditionalShow)
                        {
                            sizechange = true;
                            skip = 0;
                        }

                        // no window if no visuals && no settings
                        if (visualIndex == 3 && /* !((!UseToolbar && GUIShowSettings && ConditionalShow) || (UseToolbar &&*/  toolbarShowSettings) //))
                        {
                            return;
                        }

                        if (!HideUI)
                        {
                            styleTextArea = new GUIStyle(GUI.skin.textArea);
                            styleTextArea.normal.textColor = styleTextArea.focused.textColor = styleTextArea.hover.textColor = styleTextArea.active.textColor = Color.green;
                            styleTextArea.alignment = TextAnchor.MiddleCenter;
                            styleTextArea.stretchHeight = false;
                            styleTextArea.stretchWidth = false;
                            styleTextArea.fixedWidth = fixedwidth - margin;

                            styleButton = new GUIStyle(GUI.skin.button);
                            styleButton.normal.textColor = styleButton.focused.textColor = styleButton.hover.textColor = styleButton.active.textColor = Color.white;
                            styleButton.padding = new RectOffset(0, 0, 0, 0);

                            styleToggle = new GUIStyle(GUI.skin.toggle);

                            styleValue = new GUIStyle(GUI.skin.label);
                            styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.green;
                            styleValue.alignment = TextAnchor.MiddleCenter;

                            if (sizechange)
                            {
                                windowPos.yMax = windowPos.yMin + 20;
                                sizechange = false;
                            }

                            GUILayoutOption[] opts = { GUILayout.Width(fixedwidth), GUILayout.ExpandHeight(true) };
                            Rect oldRect = new Rect(windowPos);
                            windowPos = ClickThruBlocker.GUILayoutWindow(this.ClassID, windowPos, OnWindow, ConditionalShow ? "Proximity" : "Proximity settings", opts);
                            //windowPos = GUILayout.Window(123124, windowPos, OnWindow, ConditionalShow ? "Proximity" : "Proximity settings", GUILayout.Width(fixedwidth));
                            windowPos.width = fixedwidth;
                            if (oldRect != windowPos)
                                Save();
                            if (windowPos.x == 0 && windowPos.y == 0)
                            {
                                windowPos = windowPos.CentreScreen();
                            }
                        }
                        else
                        {
                            DoProximityContent();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                print("Proximity - ProximityDraw(): " + ex.Message);
            }
        }

        private int GetAltitude()
        {
            float distance = 0f;

            int intAlt = 0;

            try
            {
                // who knows what all the different XaltitudeY methods and fields are supposed to be, but many have unexpected values 
                // in certain situations. These seem to all be appropriate to the situation.
                if (FlightGlobals.ActiveVessel != null)
                {
                    if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.FLYING)
                    {
                        if (FlightGlobals.ActiveVessel.heightFromTerrain >= 0)
                        {
                            distance = Mathf.Min(FlightGlobals.ActiveVessel.heightFromTerrain, (float)FlightGlobals.ActiveVessel.altitude);
                        }
                        else
                        {
                            distance = Convert.ToInt32(FlightGlobals.ActiveVessel.altitude);
                        }
                    }
                    else
                    {
                        distance = Mathf.Max(FlightGlobals.ActiveVessel.GetHeightFromTerrain(), FlightGlobals.ActiveVessel.heightFromTerrain);
                    }
                }

                intAlt = Convert.ToInt32(distance);
            }
            catch (Exception ex)
            {
                print("Proximity - GetAltitude(): " + ex.Message);
            }

            return intAlt;
        }

        private bool RightConditionsToDraw()
        {
            bool retval = true;

            try
            {
                if (FlightGlobals.ActiveVessel == null)
                {
                    //print("@@@Not processing - no vessel");
                    return false;
                }

                if (!part.IsPrimary(FlightGlobals.ActiveVessel.parts, ClassID))
                {
                    //print("@@@Not processing - multiple part, clsID = " + this.ClassID);
                    return false; // this is such a hack
                }

                if (lostToStaging)
                {
                    //print("@@@Not processing - lost to staging");
                    prevConditionalShow = ConditionalShow = false;
                    return false;
                }

                if (timeSinceDescending + gracePeriod < FlightGlobals.ActiveVessel.missionTime && FlightGlobals.ActiveVessel.verticalSpeed >= 0.1)
                {
                    //print("@@@Not processing - not descended recently");
                    retval = false;
                }
                else if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.LANDED ||
                    FlightGlobals.ActiveVessel.situation == Vessel.Situations.SPLASHED)
                {
                    //print("@@@Not processing - Landed/Splashed");
                    retval = false;
                }
                else if (TimeWarp.CurrentRateIndex != 0)
                {
                    //print("@@@Not processing - Timewarp");
                    retval = false;
                }
                else if (!(timeSinceLanding + gracePeriod > FlightGlobals.ActiveVessel.missionTime || FlightGlobals.ActiveVessel.situation == Vessel.Situations.FLYING ||
                        FlightGlobals.ActiveVessel.situation == Vessel.Situations.SUB_ORBITAL))
                {
                    //print("@@@Not processing - Not flying or suborbital");
                    retval = false;
                }
                else if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
                {
                    //print("@@@Not processing - prelaunch");
                    retval = false;
                }
                else if (altitude > ActivationHeight || altitude < 1)
                {
                    //print("@@@Not processing - Not in alt range");
                    retval = false;
                }
                else if (FlightDriver.Pause || PauseMenu.isOpen)
                {
                    //print("@@@Not processing - paused");
                    retval = false;
                }
                else if (parachutesOpen && deactivateOnParachute)
                {
                    //print("@@@Not processing - parachute");
                    retval = false;
                }
                else if (deactivateIfRover && FlightGlobals.ActiveVessel.vesselType == VesselType.Rover)
                {
                    //print("@@@Not processing - rover");
                    retval = false;
                }
                else if (timeSinceOnGround + gracePeriod > FlightGlobals.ActiveVessel.missionTime && FlightGlobals.ActiveVessel.verticalSpeed >= 0.1)
                {
                    //print("@@@Not processing - just launched");
                    retval = false;
                }
                else if (!SystemOn)
                {
                    //print("@@@Not processing - manually deactivated");
                    retval = false;
                }
            }
            catch (Exception ex)
            {
                print("Proximity - RightConditionsToDraw(): " + ex.Message);
            }

            prevConditionalShow = ConditionalShow;
            ConditionalShow = retval;
            return ConditionalShow || (/* UseToolbar && */ toolbarShowSettings);
        }

        private void OnWindow(int windowID)
        {
            DoProximityContent();
            GUI.DragWindow();
        }

        private void DoProximityContent()
        {
            CheckLanded();
            if (Event.current.type == EventType.repaint && ConditionalShow)
            {
                CheckChutes();

                int count = GetBeepInterval();

                skip--;

                if (skip <= 0)
                {
                    skip = count;

                    if (isPowered && SystemOn)
                    {
                        if (!doneOnce)
                        {
                            doneOnce = true;
                        }

                        if (warnPos < 0)
                        {
                            warnPos = 0;
                        }

                        if (visualIndex == 1 || (visualIndex == 2 && altitude <= DSThreshold)) // visualType = speed
                        {
                            warn = GetWarnStringSpeed();
                        }
                        else if (visualIndex == 0 || (visualIndex == 2 && altitude > DSThreshold)) // visualType = distance
                        {
                            warn = GetWarnStringDistance();
                        }

                        DoSound();
                    }
                    else
                    {
                        warn = "--------------------unpowered---------------------";
                    }
                }
            }

            if (!HideUI)
            {
                ShowGraphicalIndicator();
                ShowSettings();
            }
        }

        private void ShowGraphicalIndicator()
        {
            if (SystemOn && ConditionalShow && visualIndex != 3)
            {
                styleTextArea.normal.textColor = styleTextArea.focused.textColor = styleTextArea.hover.textColor = styleTextArea.active.textColor = GetColour(altitude);

                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                GUILayout.Label(warn, styleTextArea);
                GUILayout.EndHorizontal();
            }
        }

        private int GetBeepInterval()
        {
            newBeepAllowed = true;
            return 1 + (20 * altitude / ActivationHeight);
        }

        private void CheckLanded()
        {
            if (FlightGlobals.ActiveVessel != null)
            {
                // gives 5 second delay before deactivating after landing
                if (!FlightGlobals.ActiveVessel.LandedOrSplashed)
                {
                    timeSinceLanding = FlightGlobals.ActiveVessel.missionTime;
                }

                // allows us to switch off if ascending for > 5 seconds (ie launch)
                if (FlightGlobals.ActiveVessel.verticalSpeed < 0)
                {
                    timeSinceDescending = FlightGlobals.ActiveVessel.missionTime;
                }

                if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.LANDED || FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
                {
                    timeSinceOnGround = FlightGlobals.ActiveVessel.missionTime;
                }
            }
        }

        // colour for visual display bar - cyan if distance mode, else by prox / speed ratio
        private Color GetColour(int alt)
        {
            Color colour = Color.green;

            if (!isPowered)
            {
                colour = Color.grey;
            }
            else if (FlightGlobals.ActiveVessel.verticalSpeed >= 0)
            {
                // going up
                colour = Color.white;
            }
            else if (visualIndex == 0 || (visualIndex == 2 && altitude > DSThreshold))
            {
                // distance mode
                colour = Color.cyan;
            }
            else
            {
                // speed mode - colour for danger
                float danger = (float)FlightGlobals.ActiveVessel.verticalSpeed * -2.5f / (altitude + 4);
                if (danger < 0.0)
                {
                    danger = 0.0f;
                }
                else if (danger > 1.0)
                {
                    danger = 1.0f;
                }

                if (danger <= 0.5f)
                {
                    colour = Color.Lerp(Color.green, Color.yellow, danger * 2.0f);
                }
                else
                {
                    colour = Color.Lerp(Color.yellow, Color.red, (danger - 0.5f) * 2.0f);
                }
            }
            return colour;
        }

        private void ShowSettings()
        {
            bool changed = false;
            try
            {
                if (toolbarShowSettings)
                {
                    // Activation height
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    GUILayout.Label("Active below:");

                    if (GUILayout.Button("-", styleButton, GUILayout.ExpandWidth(true)))
                    {
                        ActivationHeight -= 500;
                        if (ActivationHeight < 500) ActivationHeight = 500;
                        changed = true;
                    }

                    GUILayout.Label(ActivationHeight.ToString() + " m", styleValue);

                    if (GUILayout.Button("+", styleButton, GUILayout.ExpandWidth(true)))
                    {
                        ActivationHeight += 500;
                        if (ActivationHeight > 10000) ActivationHeight = 10000;
                        changed = true;
                    }

                    GUILayout.EndHorizontal();

                    // Visual type
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    string cap = "Visual: " + visualType[visualIndex];
                    cap = cap.Replace("%", DSThreshold.ToString());
                    if (GUILayout.Button(cap, styleButton, GUILayout.ExpandWidth(true)))
                    {
                        visualIndex++;
                        if (visualIndex == visualType.Length)
                        {
                            visualIndex = 0;
                        }
                        if (visualIndex == 2 || visualIndex == 3) // change size due to threshold field appearing / disappearing
                        {
                            sizechange = true;
                        }
                    }
                    GUILayout.EndHorizontal();

                    // Visual threshold subtype
                    if (visualIndex == 2)
                    {
                        if (ThresholdHeight(styleValue))
                            changed = true;
                    }

                    // Sound type
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    if (GUILayout.Button("Sound: " + beepType[beepIndex], styleButton, GUILayout.ExpandWidth(true)))
                    {
                        changed = true;
                        beepIndex++;
                        if (beepIndex == beepType.Length)
                        {
                            beepIndex = 0;
                        }
                    }
                    GUILayout.EndHorizontal();

                    // beep pitch
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    if (GUILayout.Button("Pitch: " + pitchType[pitchIndex], styleButton, GUILayout.ExpandWidth(true)))
                    {
                        changed = true;
                        pitchIndex++;
                        if (pitchIndex == pitchType.Length)
                        {
                            pitchIndex = 0;
                        }
                    }
                    GUILayout.EndHorizontal();

                    // beep volume 
                    float oldvol = volume;
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    GUILayout.Label("Volume:", GUILayout.ExpandWidth(false));
                    volume = GUILayout.HorizontalSlider(volume, 0.01f, 1f, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();

                    if (volume != oldvol)
                    {
                        changed = true;
                        audioSource.volume = volume;
                    }

                    // parachutes
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    bool old = deactivateOnParachute;
                    deactivateOnParachute = GUILayout.Toggle(deactivateOnParachute, " Off if parachuting", styleToggle, null);
                    changed = changed || (old != deactivateOnParachute);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    old = deactivateIfRover;
                    deactivateIfRover = GUILayout.Toggle(deactivateIfRover, " Off if vessel is rover", styleToggle, null);
                    changed = changed || (old != deactivateOnParachute);                    
                    GUILayout.EndHorizontal();

                    styleButton.normal.textColor = styleButton.focused.textColor = styleButton.hover.textColor = styleButton.active.textColor = SystemOn ? Color.red : Color.green;
                    styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.white;

                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    GUILayout.Label("Proximity", styleValue);
                    styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = SystemOn ? Color.green : Color.red;
                    GUILayout.Label(SystemOn ? "ON " : "OFF ", styleValue);
                    if (GUILayout.Button(SystemOn ? "Switch off" : "Switch on", styleButton, GUILayout.ExpandWidth(true)))
                    {
                        changed = true;
                        ToggleSystemOn();
                    }
                    GUILayout.EndHorizontal();
                    if (changed)
                        Save();
                }
            }
            catch (Exception ex)
            {
                print("Proximity - ShowSettings(): " + ex.Message);
            }
        }

        internal void ToggleSystemOn()
        {
            SystemOn = !SystemOn;
            sizechange = true;

            {
                StockToolbar stb = (StockToolbar)StockToolbar.FindObjectOfType(typeof(StockToolbar));
                if (stb != null)
                {
                    stb.RefreshButtonTexture();
                }
            }
        }

        private bool ThresholdHeight(GUIStyle style)
        {
            bool b = false;
            GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
            GUILayout.Label("Threshold: ");
            if (GUILayout.Button("-", styleButton, GUILayout.ExpandWidth(true)))
            {
                b = true;
                DSThreshold -= 50;
                if (DSThreshold < 50)
                {
                    DSThreshold = 50;
                }
            }

            GUILayout.Label(DSThreshold.ToString() + " m", style);

            if (GUILayout.Button("+", styleButton, GUILayout.ExpandWidth(true)))
            {
                b = true;
                DSThreshold += 50;
                if (DSThreshold > 2000)
                {
                    DSThreshold = 2000;
                }
            }
            GUILayout.EndHorizontal();
            return b;
        }

        private AudioClip MakeBeep()
        {
            //eturn AudioClip.Create("beepx", 4096, 1, 44100, false, false, OnAudioRead, OnAudioSetPosition);
            return AudioClip.Create("beepx", 4096, 1, 44100, false, OnAudioRead, OnAudioSetPosition);
        }

        private string InitialiseWarnString()
        {
            return warnstring;
        }

        // make visual display string in Speed mode
        private string GetWarnStringSpeed()
        {
            string warn = InitialiseWarnString();

            warn = warn.Insert(warnstring.Length - warnPos, "O");
            warn = warn.Insert(warnPos, "O");

            if (FlightGlobals.ActiveVessel.verticalSpeed <= 0) // falling
            {
                if (++warnPos > warnstring.Length / 2)
                {
                    warnPos = 0;
                }
            }
            else // rising
            {
                if (--warnPos < 1)
                {
                    warnPos = warnstring.Length / 2;
                }
            }

            return warn;
        }

        // make visual display string in Distance mode
        private string GetWarnStringDistance()
        {
            warnPos = warnstring.Length / 2 - ((warnstring.Length / 2) * altitude / ActivationHeight);

            string warn = InitialiseWarnString();
            warn = warn.Insert(warnstring.Length - (warnPos + 1), "O");
            warn = warn.Insert(warnPos + 1, "O");
            return warn;
        }

        private void DoSound()
        {
            if (!ShouldBeep())
            {
                return;
            }

            audioskip--;
            if (audioskip <= 0)
            {
                audioskip = audioSkipValue;

                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                audioSource.PlayOneShot(MakeBeep(), volume);
            }
        }

        private bool ShouldBeep()
        {
            if (FlightGlobals.ActiveVessel == null)
            {
                return false;
            }

            if (FlightGlobals.ActiveVessel.verticalSpeed > 0)
            {
                return false;
            }

            if (FlightDriver.Pause || PauseMenu.isOpen)
            {
                return false;
            }

            if (beepIndex == 3)
            {
                return false;
            }

            return true;
        }

        void CheckRealChutes()
        {
            List<RealChute.RealChuteModule> lrc = FlightGlobals.ActiveVessel.FindPartModulesImplementing<RealChute.RealChuteModule>();
            if (lrc != null)
            {
                RealChute.RealChuteModule rc = lrc.FirstOrDefault();

                if (rc != null && rc.AnyDeployed)
                {
                    parachutesOpen = true;
                    return;
                }
            }
        }

        private void CheckChutes()
        {
            if (deactivateOnParachute && !parachutesOpen)
            {
                List<ModuleParachute> lpara = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleParachute>();
                List<ModuleParachute>.Enumerator en = lpara.GetEnumerator();
                while (en.MoveNext())
                {
                    if (en.Current.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED ||
                        en.Current.deploymentState == ModuleParachute.deploymentStates.DEPLOYED)
                    {
                        parachutesOpen = true;
                        break;
                    }
                }
                en.Dispose();

                if (!parachutesOpen && TechChecker.RealChutes)
                {
                    // Put the RealChutes code into a seperate function to avoid issues at runtime
                    // if RealChute is not installed
                    CheckRealChutes();
                }

            }
        }

        private bool IsPowered()
        {
            double electricCharge = 0;

            if (FlightGlobals.ActiveVessel != null)
            {
                foreach (Part p in FlightGlobals.ActiveVessel.parts)
                {
                    foreach (PartResource pr in p.Resources)
                    {
                        if (pr.resourceName.Equals("ElectricCharge") && pr.flowState)
                        {
                            electricCharge += pr.amount;

                            if (electricCharge > 0.04f)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool RefreshStockButton()
        {
            bool result = false;
            try
            {
                stockToolbar = StockToolbar.Instance;
                if (stockToolbar != null)
                {
                    result = true;
                    stockToolbar.ButtonNeeded = true;
                    stockToolbar.CreateButton();

                    if (!stockToolbar.ButtonNeeded)
                    {
                        result = false;
                        windowPos.height = 20;
                        lostToStaging = true;
                    }
                }
            }
            catch (Exception ex)
            {
                print("Proximity - RefreshStockButton(): " + ex.Message);
            }
            return result;

        }

        public static bool IsRelevant()
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.PSYSTEM)
            {
                if (FlightGlobals.ActiveVessel != null)
                {
                    List<Proximity> prox = FlightGlobals.ActiveVessel.FindPartModulesImplementing<Proximity>();

                    if (prox != null && prox.Count > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
