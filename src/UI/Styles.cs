using UnityEngine;

namespace AudioMate.UI
{
    public static class Styles
    {
        public const string Default = "Default";
        public const string Enabled = "Enabled";
        public const string Disabled = "Disabled";
        public const string Success = "Success";
        public const string Danger = "Danger";
        public const string Input = "Input";
        public const string Embedded = "Embedded";


        public static readonly Color DefaultText = Color.white;
        public static readonly Color DefaultBg = new Color(0.29f, 0.34f, 0.41f);
        public static readonly Color SuccessText = Color.white;
        public static readonly Color SuccessBg = new Color(0.2f, 0.53f, 0.33f);
        public static readonly Color DangerText = Color.white;
        public static readonly Color DangerBg = new Color(0.39f, 0.07f, 0.16f);
        public static readonly Color EnabledText = Color.white;
        public static readonly Color EnabledBg = new Color(0.46f, 0.6f, 0.51f);
        public static readonly Color DisabledText = new Color(0.64f, 0.64f, 0.64f);
        public static readonly Color DisabledBg = new Color(0.21f, 0.21f, 0.21f);
        public static readonly Color InputText = Color.white;
        public static readonly Color InputBg = new Color(0.41f, 0.48f, 0.58f);
        public static readonly Color EmbeddedText = Color.white;
        public static readonly Color EmbeddedBg = DefaultBg;
        public static readonly Color CursorEnabledText = SuccessText;
        public static readonly Color CursorEnabledBg = new Color(0.31f, 0.8f, 0.59f);
        public static readonly Color CursorDisabledText = SuccessText;
        public static readonly Color CursorDisabledBg = SuccessBg;


        public static Color Text(string category)
        {
            switch (category)
            {
                case Enabled:
                    return EnabledText;
                case Disabled:
                    return DisabledText;
                case Success:
                    return SuccessText;
                case Danger:
                    return DangerText;
                case Input:
                    return InputText;
                case Embedded:
                    return EmbeddedText;
            }

            return DefaultText;
        }

        public static Color Bg(string category)
        {
            switch (category)
            {
                case Enabled:
                    return EnabledBg;
                case Disabled:
                    return DisabledBg;
                case Success:
                    return SuccessBg;
                case Danger:
                    return DangerBg;
                case Input:
                    return InputBg;
                case Embedded:
                    return EmbeddedBg;
            }

            return DefaultBg;
        }
    }
}
