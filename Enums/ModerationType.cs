using System.ComponentModel;

namespace Blog.Enums
{
    public enum ModerationType
    {
        [Description("Political Propaganda")]
        Political,

        [Description("Offensive Language")]
        Language,

        [Description("Drug References")]
        Drugs,

        [Description("Threatening Speech")]
        Threats,

        [Description("Sexual content")]
        Sexual,

        [Description("Hate Speech")]
        HateSpeech,

        [Description("Targeted Shaming")]
        Shaming
    }
}