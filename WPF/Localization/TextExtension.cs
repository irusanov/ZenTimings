using System;
using System.Windows.Markup;

namespace ZenTimings.Localization
{
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class TextExtension : MarkupExtension
    {
        public TextExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; }

        public override object ProvideValue(IServiceProvider serviceProvider) => UiText.Get(Key);
    }
}
