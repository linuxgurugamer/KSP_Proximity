using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.IO;
using Proximity.Extensions;

namespace Proximity
{
    class Proximity : PartModule
    {
        private static Rect windowPos = new Rect();

        // beep characteristics
        private static string[] beepType = { "Square", "Saw", "Sine", "None" };
        private static int beepIndex = 0;

        private static string[] pitchType = { "Variable", "440 Hz", "880 Hz", "1760 Hz"};
        private static int pitchIndex = 0;

        // visual display
        private static string[] visualType = { "Distance", "Speed", "Dist >%m, Speed <%m", "No visuals"};
        private static int visualIndex = 0;

        // expand window - showsettings is toggled by the button on the prox window itself. It is not used
        // if settingsMode (controlled from the toolbar button) is true, in which case settings are always shown and 
        // the button on the prox window is not shown
        private static bool GUIShowSettings = false;
        private static bool toolbarShowSettings = false;
        public static bool ToolbarShowSettings
        {
            get { return toolbarShowSettings; }
            set 
            { 
                toolbarShowSettings = value;
                sizechange = true;
            }
        }

        private static bool UseToolbar = false;
        private static StockToolbar stockToolbar = null;

        private static int ActivationHeight = 3000; // for proximity as a whole
        private static int DSThreshold = 300; // threshold height for both (a) switching between the two visual modes,

        private static ToolbarButtonWrapper toolbarButton = null;

        private int altitude = 0;

        private bool newInstance = true;

        // resize window? - prevents blinking when buttons clicked
        private static bool sizechange = true;

        // rate of visual and audio output
        private int skip = 0;
        private int audioskip = 0;
        private int audioSkipValue = 10;
        private bool newBeepAllowed = true;
        private static bool hideUI = false;
        public static bool HideUI
        {
            get { return hideUI; }
            set { hideUI = value; }
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

        private static bool useStockToolBar = true;
        public static bool UseStockToolBar
        {
            get { return useStockToolBar; }
            set { useStockToolBar = value; }
        }
        
        private static bool systemOn = true;
        public static bool SystemOn
        {
            get { return systemOn; }
            set { systemOn = value; }
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

                    if (!useStockToolBar) // blizzy
                    {
                        InitBlizzyButton();
                    }
                    else // stock
                    {
                        UseToolbar = true;
                    }
                }
                ProximityDraw();
            }
        }

        private void InitBlizzyButton()
        {
            try
            {
                toolbarButton = new ToolbarButtonWrapper("Proximity", "toolbarButton");
                RefreshBlizzyButton();
                toolbarButton.ToolTip = "Proximity settings";
                toolbarButton.Visible = true;
                toolbarButton.AddButtonClickHandler((e) =>
                {
                    toolbarShowSettings = !toolbarShowSettings;
                    sizechange = true;
                    RefreshBlizzyButton();
                });
            }
            catch (Exception ex)
            {
                print("Proximity - InitBlizzyButton(): " + ex.Message);
            }

            UseToolbar = true;
        }

        private bool RefreshBlizzyButton()
        {
            toolbarButton.Visible = IsRelevant();

            if (toolbarButton.Visible)
            {
                string path = "Proximity/ToolbarIcons/ProxS";

                path += toolbarShowSettings ? "G" : "C";

                if (!SystemOn)
                {
                    path += "X";
                }

                toolbarButton.TexturePath = path;
            }
            else
            {
                lostToStaging = true;
            }

            return toolbarButton.Visible;
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

        public override void OnSave(ConfigNode node)
        {
            try
            {
                PluginConfiguration config = PluginConfiguration.CreateForType<Proximity>();

                config.SetValue("Window Position", windowPos);
                config.SetValue("Beep type", beepIndex);
                config.SetValue("Visual type", visualIndex);
                config.SetValue("Activation height", ActivationHeight);
                config.SetValue("Distance Speed threshold", DSThreshold);
                config.SetValue("Show settings", UseToolbar ? toolbarShowSettings : GUIShowSettings);
                config.SetValue("Pitch type", pitchIndex);
                config.SetValue("Off if parachute", deactivateOnParachute);
                config.SetValue("Off if rover", deactivateIfRover);
                config.SetValue("Volume", (int)(volume * 100));
                config.SetValue("Toolbar", useStockToolBar ? "stock" : "blizzy");

                config.save();
            }
            catch (Exception ex)
            {
                print("Proximity - OnSave(): " + ex.Message);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                PluginConfiguration config = PluginConfiguration.CreateForType<Proximity>();

                config.load();

                try
                {
                    windowPos = config.GetValue<Rect>("Window Position");
                    beepIndex = config.GetValue<int>("Beep type");
                    visualIndex = config.GetValue<int>("Visual type");
                    ActivationHeight = config.GetValue<int>("Activation height");
                    DSThreshold = config.GetValue<int>("Distance Speed threshold");
                    GUIShowSettings = config.GetValue<bool>("Show settings");
                    toolbarShowSettings = GUIShowSettings;
                    pitchIndex = config.GetValue<int>("Pitch type");
                    deactivateOnParachute = config.GetValue<bool>("Off if parachute");
                    deactivateIfRover = config.GetValue<bool>("Off if rover");
                    int vol = config.GetValue<int>("Volume");
                    volume = ((float)vol) / 100f;
                    string s = config.GetValue<string>("Toolbar");
                    s = s.ToLower();
                    useStockToolBar = !(s.Contains("blizzy"));
                }
                catch (Exception ex)
                {
                    print("Proximity - OnLoad(): (inner) " + ex.Message);
                }

                windowPos.width = fixedwidth;

                if (volume < 0.01f)
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
                else
                {
                    if (ActivationHeight < 500) ActivationHeight = 500;
                    if (ActivationHeight > 10000) ActivationHeight = 10000;

                    if (DSThreshold < 200) DSThreshold = 200;
                    if (DSThreshold > 2000) DSThreshold = 2000;

                    if (beepIndex < 0 || beepIndex > 3) beepIndex = 1;
                    if (pitchIndex < 0 || pitchIndex > 3) pitchIndex = 0;
                    if (visualIndex < 0 || visualIndex > 3) visualIndex = 1;

                    if (volume > 1.0f) volume = 1.0f;
                }
            }
            catch (Exception ex)
            {
                print("Proximity - OnLoad(): (outer) " + ex.Message);
            }
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
                    if (newInstance || (useStockToolBar && (FlightGlobals.ActiveVessel.parts.Count != numParts || FlightGlobals.ActiveVessel.currentStage != stage)))
                    {
                        numParts = FlightGlobals.ActiveVessel.parts.Count;
                        stage = FlightGlobals.ActiveVessel.currentStage;

                        newInstance = false;
                        lostToStaging = false;
                        if (useStockToolBar)
                        {
                            if (!RefreshStockButton())
                            {
                                return;
                            }
                        }
                        else
                        {
                            if (!RefreshBlizzyButton())
                            {
                                return;
                            }
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
                        if (visualIndex == 3 && !((!UseToolbar && GUIShowSettings && ConditionalShow) || (UseToolbar && toolbarShowSettings)))
                        {
                            return;
                        }

                        if (!hideUI)
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
                            windowPos = GUILayout.Window(this.ClassID, windowPos, OnWindow, ConditionalShow ? "Proximity" : "Proximity settings", opts);
                            //windowPos = GUILayout.Window(123124, windowPos, OnWindow, ConditionalShow ? "Proximity" : "Proximity settings", GUILayout.Width(fixedwidth));
                            windowPos.width = fixedwidth;

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
            return ConditionalShow || (UseToolbar && toolbarShowSettings);
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
            try
            {
                if (UseToolbar && toolbarShowSettings)
                {
                    // Activation height
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    GUILayout.Label("Active below:");

                    if (GUILayout.Button("-", styleButton, GUILayout.ExpandWidth(true)))
                    {
                        ActivationHeight -= 500;
                        if (ActivationHeight < 500) ActivationHeight = 500;
                    }

                    GUILayout.Label(ActivationHeight.ToString() + " m", styleValue);

                    if (GUILayout.Button("+", styleButton, GUILayout.ExpandWidth(true)))
                    {
                        ActivationHeight += 500;
                        if (ActivationHeight > 10000) ActivationHeight = 10000;
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
                        ThresholdHeight(styleValue);
                    }

                    // Sound type
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    if (GUILayout.Button("Sound: " + beepType[beepIndex], styleButton, GUILayout.ExpandWidth(true)))
                    {
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
                        audioSource.volume = volume;
                    }

                    // parachutes
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    deactivateOnParachute = GUILayout.Toggle(deactivateOnParachute, " Off if parachuting", styleToggle, null);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    deactivateIfRover = GUILayout.Toggle(deactivateIfRover, " Off if vessel is rover", styleToggle, null);
                    GUILayout.EndHorizontal();

                    styleButton.normal.textColor = styleButton.focused.textColor = styleButton.hover.textColor = styleButton.active.textColor = SystemOn ? Color.red : Color.green;
                    styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.white;

                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    GUILayout.Label("Proximity", styleValue);
                    styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = SystemOn ? Color.green : Color.red;
                    GUILayout.Label(SystemOn ? "ON " : "OFF ", styleValue);
                    if (GUILayout.Button(SystemOn ? "Switch off" : "Switch on", styleButton, GUILayout.ExpandWidth(true)))
                    {
                        SystemOn = !SystemOn;
                        sizechange = true;

                        if (!useStockToolBar)
                        {
                            RefreshBlizzyButton();
                        }
                        else
                        {
                            StockToolbar stb = (StockToolbar)StockToolbar.FindObjectOfType(typeof(StockToolbar));
                            if (stb != null)
                            {
                                stb.RefreshButtonTexture();
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            catch (Exception ex)
            {
                print("Proximity - ShowSettings(): " + ex.Message);
            }
        }

        private void ThresholdHeight(GUIStyle style)
        {
            GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
            GUILayout.Label("Threshold: ");
            if (GUILayout.Button("-", styleButton, GUILayout.ExpandWidth(true)))
            {
                DSThreshold -= 50;
                if (DSThreshold < 50)
                {
                    DSThreshold = 50;
                }
            }

            GUILayout.Label(DSThreshold.ToString() + " m", style);

            if (GUILayout.Button("+", styleButton, GUILayout.ExpandWidth(true)))
            {
                DSThreshold += 50;
                if (DSThreshold > 2000)
                {
                    DSThreshold = 2000;
                }
            }
            GUILayout.EndHorizontal();
        }

        private AudioClip MakeBeep()
        {
            return AudioClip.Create("beepx", 4096, 1, 44100, false, false, OnAudioRead, OnAudioSetPosition);
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
/*
                if (!parachutesOpen && TechChecker.RealChutes)
                {
                    try
                    {
                        List<RealChute.RealChuteModule> lrc = FlightGlobals.ActiveVessel.FindPartModulesImplementing<RealChute.RealChuteModule>();
                        List<RealChute.RealChuteModule>.Enumerator rce = lrc.GetEnumerator();
                        while (rce.MoveNext())
                        {
                            if (rce.Current.AnyDeployed)
                            {
                                parachutesOpen = true;
                                break;
                            }
                        }
                        rce.Dispose();
                    }
                    catch (Exception e)
                    {
                        print(Proximity - checking realChutes - " + e.Message);
                    }
                }
*/
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
                stockToolbar = (StockToolbar)StockToolbar.FindObjectOfType(typeof(StockToolbar));
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
