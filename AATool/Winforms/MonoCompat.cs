using System;
using System.Drawing;
using System.Windows.Forms;

namespace AATool.Winforms
{
    public static class MonoCompat
    {
        //font dimensions the forms were designed with (8.25pt Microsoft Sans Serif)
        private static readonly SizeF DesignerDimensions = new (6F, 13F);

        /// <summary>
        /// Mono's fonts are larger than the designer's, so forms get auto-scaled. Unlike .NET Framework, mono moves
        /// nested user controls and scales their contents but leaves the user controls themselves at their original
        /// size, which clips their contents. Call after InitializeComponent to scale them too.
        /// </summary>
        public static void FixNestedScaling(ContainerControl root)
        {
            if (Platform.IsWindows || root.AutoScaleMode is not AutoScaleMode.Font)
                return;

            //mono replaces AutoScaleDimensions with the current dimensions once it has scaled the form
            SizeF current = root.CurrentAutoScaleDimensions;
            var factor = new SizeF(current.Width / DesignerDimensions.Width, current.Height / DesignerDimensions.Height);
            if (factor == new SizeF(1, 1))
                return;

            foreach (Control child in root.Controls)
                ScaleNested(child, factor);
        }

        private static void ScaleNested(Control control, SizeF factor)
        {
            if (control is ContainerControl)
            {
                control.Size = new Size(
                    (int)Math.Round(control.Width * factor.Width),
                    (int)Math.Round(control.Height * factor.Height));
            }
            foreach (Control child in control.Controls)
                ScaleNested(child, factor);
        }
    }
}
