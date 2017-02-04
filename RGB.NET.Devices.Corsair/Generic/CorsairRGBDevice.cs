﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using RGB.NET.Core;
using RGB.NET.Core.Layout;
using RGB.NET.Devices.Corsair.Native;

namespace RGB.NET.Devices.Corsair
{
    /// <summary>
    /// Represents a generic CUE-device. (keyboard, mouse, headset, mousmat).
    /// </summary>
    public abstract class CorsairRGBDevice : AbstractRGBDevice
    {
        #region Properties & Fields

        /// <summary>
        /// Gets information about the <see cref="CorsairRGBDevice"/>.
        /// </summary>
        public override IRGBDeviceInfo DeviceInfo { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CorsairRGBDevice"/> class.
        /// </summary>
        /// <param name="info">The generic information provided by CUE for the device.</param>
        protected CorsairRGBDevice(IRGBDeviceInfo info)
        {
            this.DeviceInfo = info;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the device.
        /// </summary>
        internal void Initialize()
        {
            InitializeLayout();

            if (InternalSize == null)
            {
                Rectangle ledRectangle = new Rectangle(this.Select(x => x.LedRectangle));
                InternalSize = ledRectangle.Size + new Size(ledRectangle.Location.X, ledRectangle.Location.Y);
            }
        }

        /// <summary>
        /// Initializes the <see cref="Led"/> and <see cref="Size"/> of the device.
        /// </summary>
        protected abstract void InitializeLayout();

        /// <summary>
        /// Applies the given layout.
        /// </summary>
        /// <param name="layoutPath">The file containing the layout.</param>
        protected void ApplyLayoutFromFile(string layoutPath)
        {
            DeviceLayout layout = DeviceLayout.Load(layoutPath);
            if (layout != null)
            {
                InternalSize = new Size(layout.Width, layout.Height);

                if (layout.Leds != null)
                    foreach (LedLayout layoutLed in layout.Leds)
                    {
                        CorsairLedIds ledId;
                        if (Enum.TryParse(layoutLed.Id, true, out ledId))
                        {
                            Led led;
                            if (LedMapping.TryGetValue(new CorsairLedId(this, ledId), out led))
                            {
                                led.LedRectangle.Location.X = layoutLed.X;
                                led.LedRectangle.Location.Y = layoutLed.Y;
                                led.LedRectangle.Size.Width = layoutLed.Width;
                                led.LedRectangle.Size.Height = layoutLed.Height;

                                led.Shape = layoutLed.Shape;
                            }
                        }
                    }
            }
        }

        /// <inheritdoc />
        protected override void UpdateLeds(IEnumerable<Led> ledsToUpdate)
        {
            List<Led> leds = ledsToUpdate.Where(x => x.Color.A > 0).ToList();

            if (leds.Count > 0) // CUE seems to crash if 'CorsairSetLedsColors' is called with a zero length array
            {
                int structSize = Marshal.SizeOf(typeof(_CorsairLedColor));
                IntPtr ptr = Marshal.AllocHGlobal(structSize * leds.Count);
                IntPtr addPtr = new IntPtr(ptr.ToInt64());
                foreach (Led led in leds)
                {
                    _CorsairLedColor color = new _CorsairLedColor
                    {
                        ledId = (int)((CorsairLedId)led.Id).LedId,
                        r = led.Color.R,
                        g = led.Color.G,
                        b = led.Color.B
                    };

                    Marshal.StructureToPtr(color, addPtr, false);
                    addPtr = new IntPtr(addPtr.ToInt64() + structSize);
                }
                _CUESDK.CorsairSetLedsColors(leds.Count, ptr);
                Marshal.FreeHGlobal(ptr);
            }
        }

        #endregion
    }
}