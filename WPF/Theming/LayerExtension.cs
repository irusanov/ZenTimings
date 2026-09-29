using System.Windows;

namespace ZenTimings.Theming
{
    /// <summary>
    /// Tracks which layer an element is on, so control styles can pick colors that stand out from what is
    /// behind them. Content of a window is on layer 1 by default. <see cref="SetLayer"/> puts an element on a
    /// given layer (and its content one layer above); <see cref="SetIncreaseLayer"/> puts the content of an
    /// element one layer above the element itself. Styles read <see cref="GetComputedLayer"/>.
    /// </summary>
    public static class LayerExtension
    {
        public static readonly DependencyProperty LayerProperty = DependencyProperty.RegisterAttached(
            "Layer", typeof(int?), typeof(LayerExtension), new PropertyMetadata(null, OnLayerChanged));

        public static readonly DependencyProperty IncreaseLayerProperty = DependencyProperty.RegisterAttached(
            "IncreaseLayer", typeof(bool), typeof(LayerExtension), new PropertyMetadata(false, OnIncreaseLayerChanged));

        private static readonly DependencyPropertyKey ComputedLayerPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
            "ComputedLayer", typeof(int), typeof(LayerExtension), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty ComputedLayerProperty = ComputedLayerPropertyKey.DependencyProperty;

        public static int? GetLayer(DependencyObject obj) => (int?)obj.GetValue(LayerProperty);

        public static void SetLayer(DependencyObject obj, int? value) => obj.SetValue(LayerProperty, value);

        public static bool GetIncreaseLayer(DependencyObject obj) => (bool)obj.GetValue(IncreaseLayerProperty);

        public static void SetIncreaseLayer(DependencyObject obj, bool value) => obj.SetValue(IncreaseLayerProperty, value);

        public static int GetComputedLayer(DependencyObject obj) => (int)obj.GetValue(ComputedLayerProperty);

        private static void SetComputedLayer(DependencyObject obj, int value) => obj.SetValue(ComputedLayerPropertyKey, value);

        private static void OnLayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == null)
                return;

            int layer = (int)e.NewValue;
            SetComputedLayer(d, layer);
            WhenLoaded(d, element => SetComputedLayerOfChildren(element, layer + 1));
        }

        private static void OnIncreaseLayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
                return;

            WhenLoaded(d, element => SetComputedLayerOfChildren(element, GetComputedLayer(element) + 1));
        }

        private static void WhenLoaded(DependencyObject d, System.Action<FrameworkElement> action)
        {
            if (!(d is FrameworkElement element))
                return;

            if (element.IsLoaded)
                action(element);

            // Applied again on every load: an element that is unloaded and loaded again (a tab page, an
            // item container) may have new logical children by then.
            element.Loaded += (s, e) => action(element);
        }

        // The logical children get the new layer; the value is inherited from there down, so the whole
        // subtree follows unless an element sets its own Layer.
        private static void SetComputedLayerOfChildren(FrameworkElement element, int value)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is DependencyObject childObject && GetLayer(childObject) == null)
                    SetComputedLayer(childObject, value);
            }
        }
    }
}
