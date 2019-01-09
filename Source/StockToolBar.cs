using System.IO;
using System.Reflection;
using UnityEngine;
using KSP.UI.Screens;

namespace Proximity
{
    [KSPAddon(KSPAddon.Startup.Flight, true)]
    public class StockToolbar : MonoBehaviour
    {
        private static Texture2D shownOnButton;
        private static Texture2D shownOffButton;
        private static Texture2D hiddenOnButton;
        private static Texture2D hiddenOffButton;
        private static Texture2D unavailableButton;

        private ApplicationLauncherButton stockToolbarBtn;

        private bool buttonNeeded = false;
        public bool ButtonNeeded
        {
            get { return buttonNeeded; }
            set { buttonNeeded = value; }
        }

        void Start()
        {
            if (Proximity.UseStockToolBar)
            {
                Load(ref shownOnButton, "ProximityGreyOn.png");
                Load(ref shownOffButton, "ProximityGreyOff.png");
                Load(ref hiddenOnButton, "ProximityColourOn.png");
                Load(ref hiddenOffButton, "ProximityColourOff.png");
                Load(ref unavailableButton, "ProximityUnavailable.png");

                GameEvents.onGUIApplicationLauncherReady.Add(CreateButton);
            }
            DontDestroyOnLoad(this); 
        }

        private void Load(ref Texture2D tex, string file)
        { 
            if (tex == null)
            {
                tex = new Texture2D(36, 36, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), file)));
            }
        }

        public void CreateButton()
        {
            buttonNeeded = Proximity.IsRelevant();
            if (buttonNeeded)
            {
                MakeButton();
            }
            else if (stockToolbarBtn != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
            }
        }

        private void MakeButton()
        { 
            if (stockToolbarBtn != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
            }

            stockToolbarBtn = ApplicationLauncher.Instance.AddModApplication(
                ProximityHide, ProximityShow, null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW, GetTexture());
            
            if (!Proximity.ToolbarShowSettings)
            {
                stockToolbarBtn.SetTrue(false);
            }
            else
            {
                stockToolbarBtn.SetFalse(false);
            }
        }

        public void RefreshButtonTexture()
        {
            if (stockToolbarBtn != null)
            {
                stockToolbarBtn.SetTexture(GetTexture());
            }
        }

        private void ProximityHide()
        {
            if (Proximity.ToolbarShowSettings)
            {
                Proximity.ToolbarShowSettings = false;
                RefreshButtonTexture();
            }
        }

        private void ProximityShow()
        {
            if (!Proximity.ToolbarShowSettings)
            {
                Proximity.ToolbarShowSettings = true;
                RefreshButtonTexture();
            }
        }

        private Texture2D GetTexture()
        { 
            Texture2D tex;

            if (TechChecker.TechAvailable)
            {
                if (Proximity.SystemOn)
                {
                    tex = (Proximity.ToolbarShowSettings ? shownOnButton : hiddenOnButton);
                }
                else
                {
                    tex = (Proximity.ToolbarShowSettings ? shownOffButton : hiddenOffButton);
                }
            }
            else
            {
                tex = unavailableButton;
            }

            return tex;
        }

        private void OnDestroy()
        {
            if (stockToolbarBtn != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
            }
        }
    }
}
