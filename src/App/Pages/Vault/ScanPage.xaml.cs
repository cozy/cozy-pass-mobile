﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class ScanPage : BaseContentPage
    {
        private ScanPageViewModel ViewModel => BindingContext as ScanPageViewModel;
        private readonly Action<string> _callback;
        private CancellationTokenSource _autofocusCts;
        private Task _continuousAutofocusTask;
        private readonly Color _greenColor;
        private readonly SKColor _blueSKColor;
        private readonly SKColor _greenSKColor;
        private readonly Stopwatch _stopwatch;
        private bool _pageIsActive;
        private bool _qrcodeFound;
        private float _scale;

        private readonly LazyResolve<ILogger> _logger = new LazyResolve<ILogger>("logger");

        public ScanPage(Action<string> callback)
        {
            _callback = callback;
            InitializeComponent();
            _zxing.Options = new ZXing.Mobile.MobileBarcodeScanningOptions
            {
                UseNativeScanning = true,
                PossibleFormats = new List<ZXing.BarcodeFormat> { ZXing.BarcodeFormat.QR_CODE },
                AutoRotate = false,
                TryInverted = true
            };
            if (Device.RuntimePlatform == Device.Android)
            {
                ToolbarItems.RemoveAt(0);
            }

            _greenColor = ThemeManager.GetResourceColor("SuccessColor");
            _greenSKColor = _greenColor.ToSKColor();
            _blueSKColor = ThemeManager.GetResourceColor("PrimaryColor").ToSKColor();
            _stopwatch = new Stopwatch();
            _qrcodeFound = false;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _zxing.IsScanning = true;

            // Fix for Autofocus, now it's done every 2 seconds so that the user does't have to do it
            // https://github.com/Redth/ZXing.Net.Mobile/issues/414
            _autofocusCts?.Cancel();
            _autofocusCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

            var autofocusCts = _autofocusCts;
            // this task is needed to be awaited OnDisappearing to avoid some crashes
            // when changing the value of _zxing.IsScanning
            _continuousAutofocusTask = Task.Run(async () =>
            {
                try
                {
                    while (!autofocusCts.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), autofocusCts.Token);
                        await Device.InvokeOnMainThreadAsync(() =>
                        {
                            if (!autofocusCts.IsCancellationRequested)
                            {
                                try
                                {
                                    _zxing.AutoFocus();
                                }
                                catch (Exception ex)
                                {
                                    _logger.Value.Exception(ex);
                                }
                            }
                        });
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    _logger.Value.Exception(ex);
                }
            }, autofocusCts.Token);
            _pageIsActive = true;
            AnimationLoopAsync();
        }

        protected override async void OnDisappearing()
        {
            _autofocusCts?.Cancel();
            if (_continuousAutofocusTask != null)
            {
                await _continuousAutofocusTask;
            }
            _zxing.IsScanning = false;
            _pageIsActive = false;
            base.OnDisappearing();
        }

        private async void OnScanResult(ZXing.Result result)
        {
            try
            {
                // Stop analysis until we navigate away so we don't keep reading barcodes
                _zxing.IsAnalyzing = false;
                var text = result?.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (text.StartsWith("otpauth://totp"))
                    {
                        await QrCodeFoundAsync();
                        _callback(text);
                        return;
                    }
                    else if (Uri.TryCreate(text, UriKind.Absolute, out Uri uri) &&
                        !string.IsNullOrWhiteSpace(uri?.Query))
                    {
                        var queryParts = uri.Query.Substring(1).ToLowerInvariant().Split('&');
                        foreach (var part in queryParts)
                        {
                            if (part.StartsWith("secret="))
                            {
                                await QrCodeFoundAsync();
                                var subResult = part.Substring(7);
                                if (!string.IsNullOrEmpty(subResult))
                                {
                                    _callback(subResult.ToUpperInvariant());
                                }
                                return;
                            }
                        }
                    }
                }
                _callback(null);
            }
            catch (Exception ex)
            {
                _logger?.Value?.Exception(ex);
            }
        }

        private async Task QrCodeFoundAsync()
        {
            _qrcodeFound = true;
            Vibration.Vibrate();
            await Task.Delay(1000);
            _zxing.IsScanning = false;
        }

        private async void Close_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await Navigation.PopModalAsync();
            }
        }

        private void AddAuthenticationKey_OnClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.TotpAuthenticationKey))
            {
                _callback(ViewModel.TotpAuthenticationKey);
                return;
            }
            _callback(null);
        }

        private void ToggleScanMode_OnTapped(object sender, EventArgs e)
        {
            ViewModel.ToggleScanModeCommand.Execute(null);
            if (!ViewModel.ShowScanner)
            {
                _authenticationKeyEntry.Focus();
            }
        }

        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            var info = args.Info;
            var surface = args.Surface;
            var canvas = surface.Canvas;
            var margins = 20;
            var maxSquareSize = (Math.Min(info.Height, info.Width) * 0.9f - margins) * _scale;
            var squareSize = maxSquareSize;
            var lineSize = squareSize * 0.15f;
            var startXPoint = (info.Width / 2) - (squareSize / 2);
            var startYPoint = (info.Height / 2) - (squareSize / 2);
            canvas.Clear(SKColors.Transparent);

            using (var strokePaint = new SKPaint
            {
                Color = _qrcodeFound ? _greenSKColor : _blueSKColor,
                StrokeWidth = 9 * _scale,
                StrokeCap = SKStrokeCap.Round,
            })
            {
                canvas.Scale(1, 1);
                //top left
                canvas.DrawLine(startXPoint, startYPoint, startXPoint, startYPoint + lineSize, strokePaint);
                canvas.DrawLine(startXPoint, startYPoint, startXPoint + lineSize, startYPoint, strokePaint);
                //bot left
                canvas.DrawLine(startXPoint, startYPoint + squareSize, startXPoint, startYPoint + squareSize - lineSize, strokePaint);
                canvas.DrawLine(startXPoint, startYPoint + squareSize, startXPoint + lineSize, startYPoint + squareSize, strokePaint);
                //top right
                canvas.DrawLine(startXPoint + squareSize, startYPoint, startXPoint + squareSize - lineSize, startYPoint, strokePaint);
                canvas.DrawLine(startXPoint + squareSize, startYPoint, startXPoint + squareSize, startYPoint + lineSize, strokePaint);
                //bot right
                canvas.DrawLine(startXPoint + squareSize, startYPoint + squareSize, startXPoint + squareSize - lineSize, startYPoint + squareSize, strokePaint);
                canvas.DrawLine(startXPoint + squareSize, startYPoint + squareSize, startXPoint + squareSize, startYPoint + squareSize - lineSize, strokePaint);
            }
        }

        async Task AnimationLoopAsync()
        {
            try
            {
                _stopwatch.Start();
                while (_pageIsActive)
                {
                    var t = _stopwatch.Elapsed.TotalSeconds % 2 / 2;
                    _scale = (20 - (1 - (float)Math.Sin(4 * Math.PI * t))) / 20;
                    SkCanvasView.InvalidateSurface();
                    await Task.Delay(TimeSpan.FromSeconds(1.0 / 30));
                    if (_qrcodeFound && _scale > 0.98f)
                    {
                        _checkIcon.TextColor = _greenColor;
                        SkCanvasView.InvalidateSurface();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Value?.Exception(ex);
            }
            finally
            {
                _stopwatch?.Stop();
            }
        }
    }
}
