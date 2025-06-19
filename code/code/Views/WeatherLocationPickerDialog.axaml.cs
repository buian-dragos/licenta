using Avalonia.Controls;
using Avalonia.Interactivity;
using Mapsui;                            // MPoint, IFeature, PointFeature
using Mapsui.Projections;                 // SphericalMercator
using Mapsui.UI;                        // MapInfoEventArgs
using Mapsui.Layers;                    // MemoryLayer
using Mapsui.Styles;                    // SymbolStyle, SymbolType, Brush, Pen, Color
using System.Collections.Generic;
using Mapsui.Extensions;                // Added for Navigator extension methods like NavigateTo

namespace code.Views
{
    public partial class WeatherLocationPickerDialog : Window
    {
        private MPoint? _picked;
        private MemoryLayer _markerLayer = new MemoryLayer
        {
            Name = "ClickMarker",
            IsMapInfoLayer = true,
            Features = new List<IFeature>()
        };

        public WeatherLocationPickerDialog()
        {
            InitializeComponent();

            var (lon, lat) = (24.875, 45.8); 
            var (centerX, centerY) = SphericalMercator.FromLonLat(lon, lat);
            var initialCenter = new MPoint(centerX, centerY);
            var initialResolution = 7500.0; 

            var map = new Map(); 
            
            map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
            map.Layers.Add(_markerLayer);

            MapControl.Map = map;
            

            map.Navigator.FlyTo(center: initialCenter, maxResolution: initialResolution);
            
            map.Home = navigator => navigator.FlyTo(center: initialCenter, maxResolution: initialResolution);
            
            MapControl.Refresh();
    
            MapControl.Info += MapControl_Info;
        }

        private void MapControl_Info(object? sender, MapInfoEventArgs e)
        {
            if (e.MapInfo?.WorldPosition != null)
            {
                _picked = e.MapInfo.WorldPosition;
                ShowMarker(_picked);
            }
        }

        private void ShowMarker(MPoint location)
        {
            var featuresList = (List<IFeature>)_markerLayer.Features;
            featuresList.Clear();

            var feature = new PointFeature(location);
            feature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                Fill = new Brush(Mapsui.Styles.Color.Red),
                Outline = new Pen(Mapsui.Styles.Color.White, 2),
            });

            featuresList.Add(feature);
            MapControl.Map.Refresh();
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            if (_picked == null)
            {
                Close(null);
                return;
            }

            var (lon, lat) = SphericalMercator.ToLonLat(_picked.X, _picked.Y);

            Close((lat, lon));
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
