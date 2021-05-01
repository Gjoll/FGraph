﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace FGraph
{
    public class SEText
    {
        public String Class { get; set; }
        public String Text { get; set; }
        public String HRef { get; set; }
        public String Title { get; set; }

        public SEText(String text, String hRef = null, String title = null)
        {
            this.Text = text;
            this.HRef = hRef;
            this.Title = title;
        }

        public SEText()
        {
        }

        public float GetWidthOfString()
        {
            using Bitmap objBitmap = new Bitmap(200, 100);
            using Graphics objGraphics = Graphics.FromImage(objBitmap);
            SizeF stringSize = objGraphics.MeasureString(this.Text, new Font("Arial", 12));
            objBitmap.Dispose();
            objGraphics.Dispose();
            return stringSize.Width;
        }
    }
}