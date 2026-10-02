using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AATool.Configuration;
using AATool.Graphics;
using AATool.Net.Requests;
using AATool.UI.Controls;
using AATool.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AATool.UI.Screens
{
    public class UIUpdateScreen : UIScreen
    {
        private UITextBlock versionLabel;
        private UITextBlock titleLabel;
        private UITextBlock hasLatestLabel;

        private UIButton nowButton;
        private UIButton laterButton;
        private UIButton githubButton;
        private UIButton patreonButton;
        private UIButton closeButton;

        private UIFlowPanel upgrades;
        private UIFlowPanel fixes;

        private UIControl thumbnailBounds;

        private List<UIPicture> textTinted;

        private bool postInstall;
        private Point renderSize;

        public override Color FrameBackColor() => Config.Main.BackColor;
        public override Color FrameBorderColor() => Config.Main.BorderColor;

        public UIUpdateScreen(Main main, bool postInstall) : base(main, new ScreenWindow("AATool Update", 700, 360))
        {
            this.Host.Resizable = false;
            this.postInstall = postInstall;

            this.ReloadView();
            this.ConstrainWindow();
            ScreenWindow primary = Main.PrimaryScreen.Host;
            int x = primary.Location.X + (primary.ClientSize.X / 2) - (this.Host.ClientSize.X / 2);
            int y = primary.Location.Y + (primary.ClientSize.Y / 2) - (this.Host.ClientSize.Y / 2);
            this.Host.Location = new Point(x, y);
            this.SetIconFile(Paths.System.UpdateIcon);
            this.Host.TopMost = Config.Main.AlwaysOnTop;
            this.Host.Show();
        }

        public override string GetCurrentView()
        {
            return Path.Combine(Paths.System.TemplatesFolder, "screen_update.xml");
        }

        public override void ReloadView()
        {
            //clear and load layout if window just opened or game version changed
            this.ClearControls();
            if (this.TryLoadXml(this.GetCurrentView()))
            {
                this.InitializeRecursive(this);
                this.ResizeRecursive(new Rectangle(0, 0, this.Width, this.Height));
            }
        }

        protected override void ConstrainWindow()
        {
            int width  = this.Host.ClientSize.X;
            int height = this.Host.ClientSize.Y;
            if (width is 0 || height is 0)
                return;

            //resize layout to fit window
            if (this.renderSize.X != width || this.renderSize.Y != height)
            {
                this.renderSize = new Point(width, height);
                this.ResizeRecursive(new Rectangle(0, 0, width, height));
            }
        }

        public override void InitializeThis(UIScreen screen)
        {
            this.textTinted = new List<UIPicture>();

            this.versionLabel = this.First<UITextBlock>("version");
            this.versionLabel?.SetText("AATool " + UpdateRequest.LatestVersion.ToString());
            this.titleLabel = this.First<UITextBlock>("title");
            this.titleLabel?.SetText(UpdateRequest.LatestTitle);

            this.hasLatestLabel = this.First<UITextBlock>("has_latest");
            this.thumbnailBounds = this.First("thumbnail");

            //install now button
            this.nowButton = this.First<UIButton>("now");
            if (this.nowButton is not null)
            { 
                this.nowButton.OnClick += this.OnClick;
                this.textTinted.Add(this.nowButton.First<UIPicture>());
            }

            //remind me later button
            this.laterButton = this.First<UIButton>("later");
            if (this.laterButton is not null)
            { 
                this.laterButton.OnClick += this.OnClick;
                this.textTinted.Add(this.laterButton.First<UIPicture>());  
            }

            //github link button
            this.githubButton = this.First<UIButton>("github");
            if (this.githubButton is not null)
            { 
                this.githubButton.OnClick += this.OnClick;
                this.textTinted.Add(this.githubButton.First<UIPicture>());
            }

            //patreon link button
            this.patreonButton = this.First<UIButton>("patreon");
            if (this.patreonButton is not null)
            {
                this.patreonButton.OnClick += this.OnClick;
                this.textTinted.Add(this.patreonButton.First<UIPicture>());
            }
            
            //close button
            this.closeButton = this.First<UIButton>("close");
            if (this.closeButton is not null)
            { 
                this.closeButton.OnClick += this.OnClick;
                this.textTinted.Add(this.closeButton.First<UIPicture>());
            }
            
            this.upgrades = this.First<UIFlowPanel>("upgrades");
            this.fixes = this.First<UIFlowPanel>("fixes");

            foreach ((string text, string icon) in UpdateRequest.LatestUpgrades)
                this.upgrades?.AddControl(this.ConstructChangelistItem(text, icon));
            foreach ((string text, string icon) in UpdateRequest.LatestFixes)
                this.fixes?.AddControl(this.ConstructChangelistItem(text, icon));

            //update button visibility
            if (UpdateRequest.UpdatesAreAvailable())
            {
                this.Host.Title = $"Updates are available!";
            }
            else if (this.postInstall && !Main.IsBeta)
            {
                this.Host.Title = $"Welcome to AATool {Main.Version}!";
                this.closeButton?.Expand();
                this.nowButton?.Collapse();
                this.laterButton?.Collapse();
            }
            else
            {
                this.hasLatestLabel?.Expand();
                this.nowButton?.Collapse();
                this.laterButton?.Collapse();

                //update text
                if (Main.IsModded)
                {
                    this.Host.Title = $"You are running {Main.ShortTitle}";
                    this.hasLatestLabel?.SetText("You're on an unofficial build");
                }
                if (Main.IsBeta)
                {
                    this.Host.Title = $"You are running {Main.ShortTitle}";
                    this.hasLatestLabel?.SetText("You're ahead of the latest release!");
                }
                else if (Main.Version > UpdateRequest.LatestVersion)
                {
                    this.Host.Title = $"You are on a preview version of AATool ({Main.Version})";
                    this.hasLatestLabel?.SetText("You're ahead of the latest release!");
                }
                else
                {
                    this.Host.Title = $"You are already on the latest version of AATool ({Main.Version})";
                    this.hasLatestLabel?.SetText("Latest version already installed!");
                }
            }
        }

        private UIControl ConstructChangelistItem(string text, string icon)
        {
            var panel = new UIPanel() {
                FlexWidth = new Size(200),
                FlexHeight = new Size(16),
                Padding = new Margin(4, 0, 0, 0),
                DrawMode = DrawMode.ChildrenOnly,
            };
            var picture = new UIPicture() {
                FlexWidth = new Size(16),
                FlexHeight = new Size(16),
                Margin = new Margin(0, 0, -1, 0),
                HorizontalAlign = HorizontalAlign.Left,
                VerticalAlign = VerticalAlign.Top,
            };
            picture.SetTexture(icon);
            var label = new UITextBlock() {
                HorizontalTextAlign = HorizontalAlign.Left,
                VerticalTextAlign = VerticalAlign.Top,
                FlexHeight = new Size(16),
                Padding = new Margin(20, 0, 0, 0),
            };

            if (icon.StartsWith("bullet_"))
                this.textTinted.Add(picture);

            label.SetText(text);
            panel.AddControl(picture);
            panel.AddControl(label);
            return panel;
        }

        public override void Prepare()
        {
            base.Prepare();
            this.GraphicsDevice.Clear(Config.Main.BackColor);
        }

        public override void DrawThis(Canvas canvas)
        {
            if (UpdateRequest.LatestThumb is not null && this.thumbnailBounds is not null)
            {
                var centered = new Rectangle(
                    this.thumbnailBounds.Left,
                    this.thumbnailBounds.Top,
                    UpdateRequest.LatestThumb.Width,
                    UpdateRequest.LatestThumb.Height);
                canvas.Draw(UpdateRequest.LatestThumb, centered, Color.White, Layer.Fore);
            }
            base.DrawThis(canvas);
        }

        protected override void UpdateThis(Time time)
        {
            if (this.Host.IsDisposed)
                return;

            //make sure certain icons match text color
            foreach (UIPicture picture in this.textTinted)
                picture.SetTint(Config.Main.TextColor);

            if (Config.Main.AlwaysOnTop.Changed)
                this.Host.TopMost = Config.Main.AlwaysOnTop;

            base.UpdateThis(time);
        }

        private void OnClick(UIControl sender)
        {
            if (sender == this.nowButton)
                UpdateHelper.RunAAUpdate(0);
            else if (sender == this.laterButton || sender == this.closeButton)
                this.Host.Close();
            else if (sender == this.githubButton)
                _ = Process.Start(Paths.Web.LatestRelease);
            else if (sender == this.patreonButton)
                _ = Process.Start(Paths.Web.PatreonFull);
        }
    }
}
