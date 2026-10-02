using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Linq;
using AATool.Configuration;
using AATool.Data.Categories;
using AATool.Data.Objectives;
using AATool.Data.Objectives.Complex;
using AATool.Net;
using AATool.Net.Requests;
using AATool.Saves;
using AATool.UI.Controls;
using AATool.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AATool.UI.Screens
{
    public sealed class UIOverlayScreen : UIScreen
    {
        private const int ScrollSpeedMultiplier = 15;
        private const int BaseScrollSpeed = 30;
        private const int HeaderSlideSpeed = 4;
        private const int HeaderHeight = 42;
        private const int MinimumWidth = 1260;
        private const int MaximumWidth = 4096;

        private const int HeaderProgress = 60;
        private const int HeaderCategory = 10;
        private const int HeaderToolVersion = 10;
        private const int HeaderPatreon = 10;
        private const int HeaderSlide = 1;

        private const int BottomRowPickups = 60 * 5;
        private const int BottomRowComplex = 30;
        private const int BottomRowSlide = 2;

        public bool FastForwarding { get; set; }

        private readonly SequenceTimer titleTimer;
        private readonly SequenceTimer pickupTimer;

        private UITextBlock text;
        private UITextBlock lastRefresh;
        private UITextBlock status;

        private UICarousel advancements;
        private UICarousel criteria;
        private UIPinnedRow pinned;
        private UIObjectiveTray tray;
        private UIControl runCompletePanel;
        private UIControl carouselPanel;
        private bool isResizing;
        private Point renderSize;
        private int minimumHeight;
        private Color frameBackColor;
        private Color frameBorderColor;
        private float titleY;

        private Utilities.Timer savingPinnedTimer = new (0.5, false);
        private readonly Utilities.Timer resizeEndTimer = new (0.25, false);

        public override Color FrameBackColor() => this.frameBackColor;
        public override Color FrameBorderColor() => this.frameBorderColor;

        public void PinnedObjectivesSaved() => this.savingPinnedTimer.Reset();

        public UIOverlayScreen(Main main) : base(main, new ScreenWindow("Stream Overlay", 360, 360))
        {
            //initialize window
            this.Host.Resizable    = true;
            this.Host.KeepRestored = true;
            this.Host.Resized     += this.OnResize;
            this.Host.Closing     += this.OnClosing;

            //cycle overlay header text
            this.titleTimer = new SequenceTimer(
                HeaderProgress, HeaderSlide,
                HeaderCategory, HeaderSlide,
                HeaderToolVersion, HeaderSlide,
                HeaderPatreon, HeaderSlide);

            //cycle pickup row
            this.pickupTimer = new SequenceTimer(
                BottomRowPickups, BottomRowSlide, 
                BottomRowComplex, BottomRowSlide);

            //load layout
            this.ReloadView();

            //enforce minimum size
            this.minimumHeight = this.Height;
            this.Host.SetMinimumSize(MinimumWidth, this.minimumHeight);
            this.Host.SetMaximumSize(MaximumWidth, this.minimumHeight);
        }

        public override void Prepare()
        {
            base.Prepare();
            this.GraphicsDevice.Clear(Config.Overlay.GreenScreen);
        }

        public override string GetCurrentView()
        {
            return Path.Combine(Paths.System.ViewsFolder, Tracker.Category.ViewName, "overlay.xml");
        }

        public override void ReloadView()
        {
            this.ClearControls();
            if (!this.TryLoadXml(this.GetCurrentView()))
                Main.QuitBecause("Error loading overlay layout!");

            this.InitializeRecursive(this);
            this.ResizeRecursive(new Rectangle(0, 0, Config.Overlay.Width, this.Height));
        }

        protected override void ConstrainWindow()
        {
            int width  = Config.Overlay.Width;
            int height = this.Height;

            //enforce minimum size
            width = Math.Max(width, MinimumWidth);
            height = Math.Max(height, this.minimumHeight);

            if (width is 0 || height is 0)
                return;

            //resize window and layout to proper size
            if (this.renderSize.X != width || this.renderSize.Y != height)
            {
                this.renderSize = new Point(width, height);
                this.Host.ClientSize = this.renderSize;
                this.ResizeRecursive(new Rectangle(0, 0, width, height));
                this.UpdateCarouselLocations();
            }

            //snap window to user's preferred location
            if (!this.Positioned || Config.Overlay.StartupArrangement.Changed || Config.Overlay.StartupDisplay.Changed)
            {
                if (this.Positioned && Config.Overlay.StartupArrangement == WindowSnap.Remember)
                    return;

                this.PositionWindow(Config.Overlay.StartupArrangement,
                    Config.Overlay.StartupDisplay, 
                    Config.Overlay.LastWindowPosition);
                this.Positioned = true;
            }
        }

        public override void InitializeThis(UIScreen screen)
        {
            this.runCompletePanel = this.First("panel_congrats");
            this.carouselPanel = this.First("panel_carousel");

            this.text = new UITextBlock("minecraft", 24) {
                FlexHeight = new (42),
                Padding             = new Margin(16, 16, 8, 0),
                HorizontalTextAlign = HorizontalAlign.Center,
                VerticalTextAlign   = VerticalAlign.Top
            };
            this.AddControl(this.text);

            this.lastRefresh = new UITextBlock("minecraft", 24) {
                Padding             = new Margin(16, 16, 8, 0),
                VerticalTextAlign   = VerticalAlign.Top,
            };
            this.lastRefresh.SetVisibility(Config.Overlay.ShowLastRefresh);
            this.AddControl(this.lastRefresh);

            //initialize main objective carousel
            this.advancements = this.First<UICarousel>("advancements");
            this.advancements?.SetScrollDirection(Config.Overlay.RightToLeft);

            //initialize criteria carousel
            this.criteria = this.First<UICarousel>("criteria");
            this.criteria?.SetScrollDirection(Config.Overlay.RightToLeft);

            //initialize pinned objectives
            this.pinned = this.First<UIPinnedRow>();
            if (this.pinned is not null)
            {
                //status label
                this.status = new UITextBlock("minecraft", 24) {
                    FlexWidth = new Size(180),
                    FlexHeight = new Size(72),
                    VerticalTextAlign = VerticalAlign.Bottom
                };
                this.status.HorizontalTextAlign = Config.Overlay.RightToLeft
                    ? HorizontalAlign.Right
                    : HorizontalAlign.Left;
                this.pinned.SetStatusLabel(this.status);
                this.status.ParentTo(this);
                this.AddControl(this.pinned);

                this.tray = new UIObjectiveTray(this.pinned);
                this.AddControl(this.tray);
            }

            this.UpdateDirection();
            this.UpdateSpeed();
        }

        public void ShowObjectiveTray()
        {
            this.pinned?.Collapse();
            this.tray?.Populate();
            this.tray?.Expand();
            this.tray?.ResizeRecursive(this.Bounds);
        }

        public void HideObjectiveTray()
        {
            this.pinned?.Expand();
            this.tray?.Collapse();
        }

        protected override void UpdateThis(Time time)
        {
            //update enabled state
            if (!this.Host.IsDisposed)
                this.Host.Visible = Config.Overlay.Enabled;
            if (!this.Host.Visible)
                return;

            //sdl doesn't report when the user stops resizing, so wait for resize events to settle
            if (this.isResizing)
            {
                this.resizeEndTimer.Update(time);
                if (this.resizeEndTimer.IsExpired)
                    this.OnResizeEnd();
            }

            if (Tracker.ObjectivesChanged || Config.Overlay.Enabled.Changed)
                this.ReloadView();

            this.ConstrainWindow();
            this.UpdateContentVisibility(time);
            this.UpdateCarouselLocations();
            this.UpdateSpeed();

            if (Config.Overlay.AppearanceChanged)
                this.UpdateTheme();

            if (Config.Overlay.ArrangementChanged)
                this.UpdateDirection();

            //top center header text
            this.text?.SetText(this.PrepareHeaderText(time));
            //text next to pickup counts
            this.status?.SetText(this.PrepareStatusText());

            //corner refresh text
            this.savingPinnedTimer.Update(time);
            if (this.savingPinnedTimer.IsRunning)
                this.lastRefresh?.SetText("Saved Overlay Layout");
            else
                this.lastRefresh?.SetText(Tracker.GetLastRefresh(time));
        }

        private void UpdateTheme()
        {
            if (Config.Overlay.FrameStyle == "Custom Theme")
            {
                this.frameBackColor = Config.Overlay.CustomBackColor;
                this.frameBorderColor = Config.Overlay.CustomBorderColor;
            }
            else if (Config.MainConfig.Themes.TryGetValue(Config.Overlay.FrameStyle,
                out (Color back, Color text, Color border) theme))
            {
                this.frameBackColor = theme.back;
                this.frameBorderColor = theme.border;
            }
        }

        private void UpdateDirection()
        {
            this.advancements?.SetScrollDirection(Config.Overlay.RightToLeft);
            this.criteria?.SetScrollDirection(Config.Overlay.RightToLeft);

            if (this.pinned is not null)
            {
                this.pinned.HorizontalAlign = Config.Overlay.RightToLeft ^ Config.Overlay.PickupsOpposite
                    ? HorizontalAlign.Right
                    : HorizontalAlign.Left;
            }

            if (this.lastRefresh is not null)
            {
                this.lastRefresh.HorizontalTextAlign = Config.Overlay.RightToLeft ^ Config.Overlay.LastRefreshOpposite
                    ? HorizontalAlign.Right
                    : HorizontalAlign.Left;
            }

            if (this.status is not null)
            {
                this.status.HorizontalTextAlign = Config.Overlay.RightToLeft
                    ? HorizontalAlign.Right
                    : HorizontalAlign.Left;
            }
        }

        private string PrepareHeaderText(Time time)
        {
            if (Tracker.Category.IsComplete())
                return string.Empty;

            //slide header in/out from top of screen
            int targetY = this.titleTimer.Index % 2 is 0 ? 0 : -48;
            this.titleY = MathHelper.Lerp(this.titleY, targetY, (float)(HeaderSlideSpeed * time.Delta));
            this.text.MoveTo(new Point(0, (int)this.titleY));

            this.titleTimer.Update(time);
            if (this.titleTimer.IsExpired)
                this.titleTimer.Continue();

            //cycle header text
            return this.titleTimer.Index switch {
                2 or 3 => $"Minecraft JE: {Tracker.Category.Name} ({Tracker.Category.CurrentVersion})",
                4 or 5 => GetVersionText(),
                6 or 7 => "Support AATool @ " + Paths.Web.PatreonShort,
                _ => Tracker.Category.GetStatus()
            };
        }

        private string GetVersionText()
        {
            string text = Main.ShortTitle;
            if (UpdateRequest.UpdatesAreAvailable())
                text += " (Outdated)";
            else if (Config.Main.Layout != Config.RelaxedLayout)
                text += $" ({Config.Main.Layout.Value.ToLower()} layout)";
            return text;
        }

        private string PrepareStatusText()
        {
            if (Client.TryGet(out Client client))
            {
                //use smaller font
                this.status?.SetFont("minecraft", 24);
                return client.GetShortStatusText();
            }
            else if (MinecraftServer.IsEnabled)
            {
                //use smaller font
                this.status?.SetFont("minecraft", 24);
                return MinecraftServer.GetShortStatusText();
            }
            else
            {
                //use larger font
                this.status?.SetFont("minecraft", 48);
                if (Config.Overlay.ShowIgt && Tracker.InGameTime != default)
                    return Tracker.GetFullIgt();
            }
            return string.Empty;
        }

        private void UpdateSpeed()
        {
            float speed = BaseScrollSpeed + (Config.Overlay.Speed * ScrollSpeedMultiplier);
            if (this.FastForwarding)
                speed *= 60;
            this.advancements?.SetSpeed(speed);
            this.criteria?.SetSpeed(speed);
        }

        private void UpdateCarouselLocations()
        {
            //update vertical position of rows based on what is or isn't enabled
            int criteria = this.criteria is null || this.criteria.IsCollapsed 
                ? 0 : 64;

            int advancement = this.advancements is null || this.advancements.IsCollapsed 
                ? 0 : Config.Overlay.ShowLabels ? 160 : 110;

            if (this.criteria?.Top != HeaderHeight)
                this.criteria?.MoveTo(new Point(0, HeaderHeight));

            if (this.advancements is not null && this.advancements.Top != HeaderHeight + criteria)
                this.advancements?.MoveTo(new Point(0, HeaderHeight + criteria));

            if (this.pinned?.Top != HeaderHeight + criteria + advancement)
                this.pinned?.MoveTo(new Point(this.pinned.X, HeaderHeight + criteria + advancement));
        }

        private void UpdateContentVisibility(Time time)
        {
            //handle completion screen popup
            if (Tracker.Category.IsComplete())
            {
                this.carouselPanel?.Collapse();  
                this.lastRefresh?.Collapse();
                this.pinned?.Collapse();
                this.status?.Collapse();
                if (this.runCompletePanel?.IsCollapsed is true || Tracker.WorldChanged)
                {
                    this.runCompletePanel.Expand();
                    this.runCompletePanel.First<UIRunComplete>()?.Show();
                }
            }
            else
            {
                this.carouselPanel?.Expand();
                this.lastRefresh?.SetVisibility(Config.Overlay.ShowLastRefresh);
                this.pinned?.Expand();
                this.status?.Expand();
                this.runCompletePanel?.Collapse();

                //update criteria visibility
                this.criteria?.SetVisibility(Config.Overlay.ShowCriteria);

                //update item count visibility

                if (this.pinned is not null && (this.tray is null || this.tray.IsCollapsed))
                {
                    this.pinned.SetVisibility(Config.Overlay.ShowPickups);
                    if (this.pinned.IsCollapsed == Config.Overlay.ShowPickups)
                    {
                        if (Config.Overlay.ShowPickups)
                            this.pinned.Expand();
                        else
                            this.pinned.Collapse();

                        this.pinned.RefreshList();
                    }

                    if (this.pinned.IsCollapsed)
                        this.pinned.PositionStatusLabel(time);
                }
            }
        }

        public void RememberWindowPosition()
        {
            if (this.Host.IsDisposed || !this.Host.Visible)
                return;
            Config.Overlay.LastWindowPosition.Set(this.Host.Location);
            Config.Overlay.TrySave();
        }

        private void OnResizeBegin()
        {
            this.isResizing = true;
            this.advancements?.Break();
            this.criteria?.Break();
        }

        private void OnResize(object sender, EventArgs e)
        {
            //ignore resizes caused by the overlay matching its configured width
            Point size = this.Host.ClientSize;
            if (size == this.renderSize)
                return;

            if (!this.isResizing)
                this.OnResizeBegin();
            this.resizeEndTimer.Reset();
            Config.Overlay.Width.Set(size.X);
        }

        private void OnResizeEnd()
        {
            this.isResizing = false;
            this.advancements?.Continue();
            this.criteria?.Continue();
            Config.Overlay.Width.Set(this.Host.ClientSize.X);
            Config.Overlay.TrySave();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            //window managers always offer a close button, so treat closing as turning the overlay off
            this.RememberWindowPosition();
            e.Cancel = true;
            Config.Overlay.Enabled.Set(false);
            Config.Overlay.TrySave();
        }
    }
}
